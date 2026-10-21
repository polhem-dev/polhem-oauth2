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
    /// The cookie is <c>SameSite=Lax</c>, and <c>SameSite=None</c> for a client whose provider returns with a form POST
    /// from its own site (<see cref="OAuth2Client.UsesFormPostResponse"/>), because a browser leaves a Lax cookie out of
    /// that request. A cookie policy whose <c>MinimumSameSitePolicy</c> is stricter than None raises that cookie to the
    /// minimum, and the sign-in of such a client then fails because the browser does not send the cookie. Some older
    /// browsers, such as Safari on iOS 12, treat <c>SameSite=None</c> as Strict, so the sign-in of such a client fails there.
    /// </para>
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
        /// sign-in started more than 10 minutes ago, or the form of a POST callback exceeds the form limits or cannot be read,
        /// for example because the body exceeds the request size limit or ends early; and a
        /// <see cref="CryptographicException"/> when the cookie cannot be decrypted. Any other exception propagates.
        /// </para>
        /// <para>
        /// The parameters come from the query of a GET request, and from the form of a POST request, which a provider sends
        /// when <see cref="OAuth2Client.UsesFormPostResponse"/> is true, as Sign in with Apple does. Map the callback endpoint
        /// for both methods, and leave it out of antiforgery validation: the provider's site cannot send a token, and the
        /// state protects the callback instead. Only a body of type <c>application/x-www-form-urlencoded</c> in UTF-8 is read;
        /// any other POST has no parameters, so its sign-in fails because the state is missing.
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

            using (var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.RequestAborted))
            {
                Func<string, string?> getValue;
                try
                {
                    getValue = await ReadCallbackParametersAsync(context.Request, requestCancellation.Token).ConfigureAwait(false);
                }
                catch (InvalidDataException ex)
                {
                    return AuthorizationResult.Failure(new OAuth2Exception("The form of the callback cannot be read.", ex));
                }
                catch (IOException ex) when (!requestCancellation.IsCancellationRequested)
                {
                    // A body over the request size limit, or one cut short, is what the sender posted, as a form over the
                    // form limits is. Kestrel reports both with BadHttpRequestException, an IOException.
                    return AuthorizationResult.Failure(new OAuth2Exception("The form of the callback cannot be read.", ex));
                }
                catch (IOException ex)
                {
                    // The request was aborted while its body was read, which ADR-003 reports as a cancellation.
                    throw new OperationCanceledException("The callback request was aborted.", ex, requestCancellation.Token);
                }

                string? state = getValue("state");

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
                    getValue("code"), state, getValue("error"), getValue("error_description"), getValue("user"));

                return await client.CompleteAuthorizationAsync(callback, signIn.Pending, requestCancellation.Token).ConfigureAwait(false);
            }
        }

        // A provider that returns with a form POST (response_mode=form_post) sends the parameters in the body, and any other in
        // the query of a GET request. A POST reads only its form, so a parameter cannot come from the query of a POST.
        private static async Task<Func<string, string?>> ReadCallbackParametersAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            if (!HttpMethods.IsPost(request.Method))
            {
                var query = request.Query;
                return name => GetSingleValue(query.TryGetValue(name, out StringValues values), values);
            }

            // A provider posts application/x-www-form-urlencoded in UTF-8. Any other body is not read, so that a request from
            // anyone cannot make the server buffer a multipart upload or fail on a charset it does not support.
            if (!IsUtf8FormContent(request.ContentType))
                return _ => null;

            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return name => GetSingleValue(form.TryGetValue(name, out StringValues values), values);
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
            var cookieOptions = CreateCookieOptions(PendingAuthorizationCookie.Lifetime, crossSitePost: client.UsesFormPostResponse);
            context.Response.Cookies.Append(cookieName, WebEncoders.Base64UrlEncode(_protector.Protect(payload)), cookieOptions);
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

            context.Response.Cookies.Delete(cookieName, CreateCookieOptions(maxAge: null, crossSitePost: HttpMethods.IsPost(context.Request.Method)));
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

        private static CookieOptions CreateCookieOptions(TimeSpan? maxAge, bool crossSitePost)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // Most providers redirect back with a top-level GET request, which carries a SameSite=Lax cookie. A browser
                // leaves a Lax cookie out of the form POST from the provider's site that response_mode=form_post makes, so only
                // such a client gets SameSite=None. The state still binds the callback to this browser (ADR-005).
                SameSite = crossSitePost ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/",
                MaxAge = maxAge,
                // Without this, a cookie consent policy would drop the cookie and every sign-in would fail.
                IsEssential = true
            };
        }

        private static bool IsUtf8FormContent(string? contentType)
        {
            return Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
                && string.Equals(mediaType.MediaType.Value, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && (!mediaType.Charset.HasValue
                    || Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(mediaType.Charset).Equals("utf-8", StringComparison.OrdinalIgnoreCase));
        }

        // A repeated parameter is treated as missing: joining the values would produce a state that matches nothing.
        private static string? GetSingleValue(bool found, StringValues values)
        {
            return found && values.Count == 1 ? values[0] : null;
        }
    }
}
