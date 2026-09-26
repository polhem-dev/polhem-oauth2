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
    /// With <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2AppRelay(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{OAuth2AppRelayOptions})"/>, the
    /// same clients also sign in mobile applications through the back-end relay of ADR-006:
    /// <see cref="RedirectToAppAuthorization"/>, <see cref="RedirectToAppAsync"/> and <see cref="RedeemAppCodeAsync"/>.
    /// </para>
    /// <para>
    /// The public members are virtual, so a controller that depends on the manager can be tested with a class derived through
    /// the protected constructor.
    /// </para>
    /// </remarks>
    public partial class OAuth2Manager
    {
        private readonly Dictionary<string, OAuth2Client> _clients;
        private readonly IDataProtector _protector;
        private readonly IDataProtector _relayProtector;
        private readonly AppRelaySettings? _relay;
        private readonly IDistributedCache? _relayCache;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a manager with no clients and no relay, as the base of a test double that overrides the members a test
        /// needs. <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2Client"/>
        /// creates the manager that an application uses.
        /// </summary>
        /// <remarks>
        /// A member that the double does not override behaves as it does in such a manager: <see cref="GetClient"/> returns
        /// null, a sign-in cannot start because no client is registered, and the relay members report that the relay is not
        /// registered.
        /// </remarks>
        protected OAuth2Manager()
            : this([], new EphemeralDataProtectionProvider())
        {
        }

        internal OAuth2Manager(
            IEnumerable<OAuth2ClientRegistration> registrations,
            IDataProtectionProvider dataProtectionProvider,
            AppRelaySettings? relay = null,
            IDistributedCache? relayCache = null,
            TimeProvider? timeProvider = null)
        {
            _clients = registrations.ToDictionary(registration => registration.Name, registration => registration.Client, StringComparer.Ordinal);
            _protector = dataProtectionProvider.CreateProtector(PendingAuthorizationCookie.ProtectionPurpose);
            _relayProtector = dataProtectionProvider.CreateProtector(RelayProtectionPurpose);
            _relay = relay;
            _relayCache = relayCache;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Gets the client registered under a name, for example to refresh tokens.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The client, or null if no client is registered under that name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null.</exception>
        public virtual OAuth2Client? GetClient(string clientName)
        {
            ArgumentNullException.ThrowIfNull(clientName);

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
        public virtual string CreateAuthorizationUrl(HttpContext context, string clientName)
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
        public virtual void RedirectToAuthorization(HttpContext context, string clientName)
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
        public virtual async Task<AuthorizationResult> CompleteAuthorizationAsync(HttpContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            // A relayed sign-in that an earlier call in this request left behind must not be returned for this one.
            context.Items.Remove(s_appSignInKey);

            var query = context.Request.Query;
            string? state = GetSingleValue(query, "state");

            PendingSignIn signIn;
            try
            {
                signIn = TakePendingAuthorization(context, state);
            }
            catch (OAuth2Exception ex)
            {
                return AuthorizationResult.Failure(ex);
            }
            catch (CryptographicException ex)
            {
                return AuthorizationResult.Failure(ex);
            }

            string clientName = signIn.ClientName;
            var client = GetClient(clientName)
                ?? throw new InvalidOperationException($"The sign-in was started with the OAuth2 client '{clientName}', which is no longer registered.");
            if (signIn.AppRedirectUri is { } appRedirectUri && signIn.AppCodeChallenge is { } appCodeChallenge)
                context.Items[s_appSignInKey] = new AppSignIn(clientName, appRedirectUri, appCodeChallenge);

            var callback = new AuthorizationCallback(
                GetSingleValue(query, "code"), state, GetSingleValue(query, "error"), GetSingleValue(query, "error_description"));

            using (var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.RequestAborted))
            {
                return await client.CompleteAuthorizationAsync(callback, signIn.Pending, requestCancellation.Token).ConfigureAwait(false);
            }
        }

        private string StartSignIn(HttpContext context, string clientName, string? appRedirectUri, string? appCodeChallenge)
        {
            ArgumentNullException.ThrowIfNull(context);

            var client = GetClient(clientName)
                ?? throw new InvalidOperationException($"No OAuth2 client is registered under the name '{clientName}'.");

            var request = client.CreateAuthorizationRequest();
            DateTimeOffset now = _timeProvider.GetUtcNow();
            byte[] payload = appRedirectUri is not null && appCodeChallenge is not null
                ? PendingAuthorizationCookie.SerializeRelayed(clientName, request.Pending, now, appRedirectUri, appCodeChallenge)
                : PendingAuthorizationCookie.Serialize(clientName, request.Pending, now);
            string cookieName = PendingAuthorizationCookie.NamePrefix + request.Pending.State;
            context.Response.Cookies.Append(cookieName, WebEncoders.Base64UrlEncode(_protector.Protect(payload)), CreateCookieOptions(PendingAuthorizationCookie.Lifetime));
            return request.Url;
        }

        private PendingSignIn TakePendingAuthorization(HttpContext context, string? state)
        {
            if (string.IsNullOrEmpty(state))
                throw new OAuth2Exception("The state is missing.");

            string cookieName = PendingAuthorizationCookie.GetName(state)
                ?? throw new OAuth2Exception("The state is not valid.");
            string cookieValue = context.Request.Cookies[cookieName]
                ?? throw new OAuth2Exception("No sign-in started in this browser matches the state. It may have expired.");

            context.Response.Cookies.Delete(cookieName, CreateCookieOptions(maxAge: null));
            return PendingAuthorizationCookie.Deserialize(Unprotect(cookieValue), _timeProvider.GetUtcNow());
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
