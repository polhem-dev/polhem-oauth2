using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Primitives;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// Runs the OAuth2 sign-in of an ASP.NET Core application for the clients registered with
    /// <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2Client"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each sign-in keeps its state, PKCE code verifier and redirect URI in a cookie of its own, protected with ASP.NET Core
    /// data protection, so no session state is needed and sign-ins started in several tabs do not replace each other.
    /// Every server that can receive the callback must share the data protection key ring.
    /// </para>
    /// <para>The cookie is marked Secure, so the sign-in must start and end on HTTPS pages. It expires after 10 minutes.</para>
    /// <para>
    /// With <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2AppRelay"/>, the
    /// same clients also sign in mobile applications through the back-end relay of ADR-006:
    /// <see cref="RedirectToAppAuthorization"/>, <see cref="RedirectToAppAsync"/> and <see cref="RedeemAppCodeAsync"/>.
    /// </para>
    /// </remarks>
    public sealed partial class OAuth2Manager
    {
        private readonly Dictionary<string, OAuth2Client> _clients;
        private readonly IDataProtector _protector;
        private readonly IDataProtector _relayProtector;
        private readonly AppRelaySettings? _relay;
        private readonly IDistributedCache? _relayCache;

        internal OAuth2Manager(
            IEnumerable<OAuth2ClientRegistration> registrations,
            IDataProtectionProvider dataProtectionProvider,
            AppRelaySettings? relay = null,
            IDistributedCache? relayCache = null)
        {
            _clients = registrations.ToDictionary(registration => registration.Name, registration => registration.Client, StringComparer.Ordinal);
            _protector = dataProtectionProvider.CreateProtector(PendingAuthorizationCookie.ProtectionPurpose);
            _relayProtector = dataProtectionProvider.CreateProtector(RelayProtectionPurpose);
            _relay = relay;
            _relayCache = relayCache;
        }

        /// <summary>
        /// Gets the client registered under a name, for example to refresh tokens.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The client, or null if no client is registered under that name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null.</exception>
        public OAuth2Client? GetClient(string clientName)
        {
            if (clientName is null)
                throw new ArgumentNullException(nameof(clientName));

            return _clients.TryGetValue(clientName, out var client) ? client : null;
        }

        /// <summary>
        /// Starts a sign-in: keeps its pending values in a cookie on the response, and returns the authorization URL.
        /// </summary>
        /// <param name="context">The HTTP context of the request that starts the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <returns>The URL to redirect the user to.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public string CreateAuthorizationUrl(HttpContext context, string clientName)
        {
            return StartSignIn(context, clientName, appRedirectUri: null, appCodeChallenge: null);
        }

        /// <summary>
        /// Starts a sign-in and redirects the response to the authorization URL.
        /// </summary>
        /// <param name="context">The HTTP context of the request that starts the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public void RedirectToAuthorization(HttpContext context, string clientName)
        {
            string url = CreateAuthorizationUrl(context, clientName);
            context.Response.Redirect(url);
        }

        /// <summary>
        /// Completes the sign-in that the callback request belongs to: finds its cookie by the returned state, exchanges the
        /// authorization code for tokens, and retrieves the user information.
        /// </summary>
        /// <param name="context">The HTTP context of the callback request.</param>
        /// <param name="cancellationToken">
        /// Cancels the requests to the provider. They are also canceled when the callback request is aborted.
        /// </param>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// <para>
        /// In addition to the failures described on <see cref="OAuth2Client.CompleteAuthorizationAsync"/>, these become a
        /// failed result: an <see cref="OAuth2Exception"/> when the state is missing or no sign-in cookie matches it, or the
        /// sign-in started more than 10 minutes ago; and a <see cref="CryptographicException"/> when the cookie cannot be
        /// decrypted. Any other exception propagates.
        /// </para>
        /// <para>
        /// The response removes the sign-in cookie before the code is exchanged, so the browser cannot complete the same
        /// sign-in twice.
        /// </para>
        /// <para>
        /// A relayed sign-in completes the same way. Call <see cref="RedirectToAppAsync"/> with the result afterwards to
        /// return it to the application.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The sign-in names a client that is no longer registered.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled, or the request was aborted.</exception>
        public async Task<AuthorizationResult> CompleteAuthorizationAsync(HttpContext context, CancellationToken cancellationToken = default)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var query = context.Request.Query;
            string? state = GetSingleValue(query, "state");

            string clientName;
            PendingAuthorization pending;
            try
            {
                (clientName, pending) = TakePendingAuthorization(context, state, out string? appRedirectUri, out string? appCodeChallenge);
                if (appRedirectUri is not null && appCodeChallenge is not null)
                    context.Items[s_appSignInKey] = new AppSignIn(clientName, appRedirectUri, appCodeChallenge);
            }
            catch (OAuth2Exception ex)
            {
                return AuthorizationResult.Failure(ex);
            }
            catch (CryptographicException ex)
            {
                return AuthorizationResult.Failure(ex);
            }

            var client = GetClient(clientName)
                ?? throw new InvalidOperationException($"The sign-in was started with the OAuth2 client '{clientName}', which is no longer registered.");

            var callback = new AuthorizationCallback(
                GetSingleValue(query, "code"), state, GetSingleValue(query, "error"), GetSingleValue(query, "error_description"));

            using (var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.RequestAborted))
            {
                return await client.CompleteAuthorizationAsync(callback, pending, requestCancellation.Token).ConfigureAwait(false);
            }
        }

        private string StartSignIn(HttpContext context, string clientName, string? appRedirectUri, string? appCodeChallenge)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var client = GetClient(clientName)
                ?? throw new InvalidOperationException($"No OAuth2 client is registered under the name '{clientName}'.");

            var request = client.CreateAuthorizationRequest();
            byte[] payload = PendingAuthorizationCookie.Serialize(clientName, request.Pending, DateTimeOffset.UtcNow, appRedirectUri, appCodeChallenge);
            string cookieName = PendingAuthorizationCookie.NamePrefix + request.Pending.State;
            context.Response.Cookies.Append(cookieName, WebEncoders.Base64UrlEncode(_protector.Protect(payload)), CreateCookieOptions(PendingAuthorizationCookie.Lifetime));
            return request.Url;
        }

        // Reads the cookie of the sign-in that the state names, and removes it on the response.
        private (string ClientName, PendingAuthorization Pending) TakePendingAuthorization(
            HttpContext context, string? state, out string? appRedirectUri, out string? appCodeChallenge)
        {
            appRedirectUri = null;
            appCodeChallenge = null;
            if (string.IsNullOrEmpty(state))
                throw new OAuth2Exception("The state is missing.");

            string cookieName = PendingAuthorizationCookie.GetName(state)
                ?? throw new OAuth2Exception("The state is not valid.");
            string cookieValue = context.Request.Cookies[cookieName]
                ?? throw new OAuth2Exception("No sign-in started in this browser matches the state. It may have expired.");

            context.Response.Cookies.Delete(cookieName, CreateCookieOptions(maxAge: null));
            return PendingAuthorizationCookie.Deserialize(Unprotect(cookieValue), DateTimeOffset.UtcNow, out appRedirectUri, out appCodeChallenge);
        }

        // A cookie that is not valid base64url is reported like one that fails decryption, because both mean it was altered.
        private byte[] Unprotect(string cookieValue)
        {
            byte[] protectedPayload;
            try
            {
                protectedPayload = WebEncoders.Base64UrlDecode(cookieValue);
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("The sign-in cookie is not valid.", ex);
            }
            return _protector.Unprotect(protectedPayload);
        }

        private static CookieOptions CreateCookieOptions(TimeSpan? maxAge)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // The provider redirects back with a top-level GET request, which carries a SameSite=Lax cookie.
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = maxAge,
                // Without this, a cookie consent policy would drop the cookie and every sign-in would fail.
                IsEssential = true
            };
        }

        // A repeated parameter is treated as missing: joining the values would produce a state that matches nothing.
        private static string? GetSingleValue(IQueryCollection query, string name)
        {
            return query.TryGetValue(name, out StringValues values) && values.Count == 1 ? values[0] : null;
        }
    }
}
