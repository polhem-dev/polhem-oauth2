using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;

namespace Polhem.OAuth2.AspNet
{
    /// <summary>
    /// Registers OAuth2 clients and runs their sign-in for ASP.NET applications on System.Web.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each sign-in keeps its state, PKCE code verifier and redirect URI in a cookie of its own, protected with
    /// <see cref="MachineKey"/>, so no session state is needed and sign-ins started in several tabs do not replace each other.
    /// Every server that can receive the callback must use the same machine key.
    /// </para>
    /// <para>The cookie is marked Secure, so the sign-in must start and end on HTTPS pages. It expires after 10 minutes.</para>
    /// </remarks>
    public static class OAuth2Manager
    {
        private static readonly ConcurrentDictionary<string, OAuth2Client> s_clients = new ConcurrentDictionary<string, OAuth2Client>(StringComparer.Ordinal);

        /// <summary>
        /// Registers an OAuth2 client under a name. Register every client when the application starts, for example in
        /// <c>Application_Start</c>.
        /// </summary>
        /// <param name="clientName">The name that identifies the client, such as <c>Google</c>.</param>
        /// <param name="options">The OAuth2 options. Their type selects the provider, and they are copied.</param>
        /// <param name="httpClient">
        /// The HTTP client for requests to the provider, or null to use a shared instance. The client does not dispose it.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="clientName"/> is null, empty or white space, or <paramref name="options"/> is not valid.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="clientName"/>.</exception>
        public static void RegisterClient(string clientName, OAuth2Options options, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("The client name cannot be null, empty or white space.", nameof(clientName));

            var client = new OAuth2Client(options, httpClient);
            if (!s_clients.TryAdd(clientName, client))
                throw new InvalidOperationException($"An OAuth2 client is already registered under the name '{clientName}'.");
        }

        /// <summary>
        /// Gets the client registered under a name, for example to refresh tokens.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The client, or null if no client is registered under that name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null.</exception>
        public static OAuth2Client? GetClient(string clientName)
        {
            if (clientName is null)
                throw new ArgumentNullException(nameof(clientName));

            return s_clients.TryGetValue(clientName, out var client) ? client : null;
        }

        /// <summary>
        /// Starts a sign-in in the current request: keeps its pending values in a cookie on the response, and returns the
        /// authorization URL.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <returns>The URL to redirect the user to.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// There is no current HTTP context, or no client is registered under <paramref name="clientName"/>.
        /// </exception>
        public static string CreateAuthorizationUrl(string clientName)
        {
            return CreateAuthorizationUrl(GetCurrentContext(), clientName);
        }

        /// <summary>
        /// Starts a sign-in: keeps its pending values in a cookie on the response, and returns the authorization URL.
        /// </summary>
        /// <param name="context">The HTTP context of the request that starts the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <returns>The URL to redirect the user to.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public static string CreateAuthorizationUrl(HttpContextBase context, string clientName)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var client = GetClient(clientName)
                ?? throw new InvalidOperationException($"No OAuth2 client is registered under the name '{clientName}'.");

            var request = client.CreateAuthorizationRequest();
            byte[] payload = PendingAuthorizationCookie.Serialize(clientName, request.Pending, DateTimeOffset.UtcNow);
            string value = HttpServerUtility.UrlTokenEncode(MachineKey.Protect(payload, PendingAuthorizationCookie.ProtectionPurpose));
            string cookieName = PendingAuthorizationCookie.NamePrefix + request.Pending.State;
            context.Response.Cookies.Add(CreateCookie(cookieName, value, DateTime.UtcNow.Add(PendingAuthorizationCookie.Lifetime)));
            return request.Url;
        }

        /// <summary>
        /// Starts a sign-in in the current request and redirects the response to the authorization URL.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <remarks>
        /// The request is completed without ending the thread, so return from the page or action after calling this method.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// There is no current HTTP context, or no client is registered under <paramref name="clientName"/>.
        /// </exception>
        public static void RedirectToAuthorization(string clientName)
        {
            RedirectToAuthorization(GetCurrentContext(), clientName);
        }

        /// <summary>
        /// Starts a sign-in and redirects the response to the authorization URL.
        /// </summary>
        /// <param name="context">The HTTP context of the request that starts the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <remarks>
        /// The request is completed without ending the thread, so return from the page or action after calling this method.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="clientName"/> is null.</exception>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public static void RedirectToAuthorization(HttpContextBase context, string clientName)
        {
            string url = CreateAuthorizationUrl(context, clientName);

            // Redirect(url) ends the response by throwing ThreadAbortException. Completing the request lets the caller return normally.
            context.Response.Redirect(url, endResponse: false);
            context.ApplicationInstance?.CompleteRequest();
        }

        /// <summary>
        /// Completes the sign-in that the current callback request belongs to. The requests to the provider cannot be canceled.
        /// </summary>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// The failures that become a failed result are described on
        /// <see cref="CompleteAuthorizationAsync(HttpContextBase, CancellationToken)"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// There is no current HTTP context, or the sign-in names a client that is no longer registered.
        /// </exception>
        public static Task<AuthorizationResult> CompleteAuthorizationAsync()
        {
            return CompleteAuthorizationAsync(GetCurrentContext(), CancellationToken.None);
        }

        /// <summary>
        /// Completes the sign-in that the callback request belongs to: finds its cookie by the returned state, exchanges the
        /// authorization code for tokens, and retrieves the user information.
        /// </summary>
        /// <param name="context">The HTTP context of the callback request.</param>
        /// <param name="cancellationToken">Cancels the requests to the provider.</param>
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
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The sign-in names a client that is no longer registered.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public static async Task<AuthorizationResult> CompleteAuthorizationAsync(HttpContextBase context, CancellationToken cancellationToken = default)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            string? state = GetSingleValue(context.Request, "state");

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

            var client = GetClient(signIn.ClientName)
                ?? throw new InvalidOperationException($"The sign-in was started with the OAuth2 client '{signIn.ClientName}', which is no longer registered.");

            var callback = new AuthorizationCallback(
                GetSingleValue(context.Request, "code"), state, GetSingleValue(context.Request, "error"), GetSingleValue(context.Request, "error_description"));

            // WARNING: The HTTP context is not used after this await, which does not resume on the request's synchronization context.
            return await client.CompleteAuthorizationAsync(callback, signIn.Pending, cancellationToken).ConfigureAwait(false);
        }

        private static HttpContextBase GetCurrentContext()
        {
            return HttpContext.Current is { } current
                ? new HttpContextWrapper(current)
                : throw new InvalidOperationException("There is no current HTTP context.");
        }

        private static PendingSignIn TakePendingAuthorization(HttpContextBase context, string? state)
        {
            if (string.IsNullOrEmpty(state))
                throw new OAuth2Exception("The state is missing.");

            string cookieName = PendingAuthorizationCookie.GetName(state)
                ?? throw new OAuth2Exception("The state is not valid.");
            string cookieValue = context.Request.Cookies[cookieName]?.Value
                ?? throw new OAuth2Exception("No sign-in started in this browser matches the state. It may have expired.");

            context.Response.Cookies.Add(CreateCookie(cookieName, string.Empty, DateTime.UtcNow.AddDays(-1)));
            var signIn = PendingAuthorizationCookie.Deserialize(Unprotect(cookieValue), DateTimeOffset.UtcNow);
            // System.Web has no back-end relay (ADR-006), so a relayed sign-in is refused rather than completed as a web sign-in.
            if (signIn.AppRedirectUri is not null)
                throw new OAuth2Exception("The sign-in was relayed to an application, which this package does not support.");
            return signIn;
        }

        // A cookie that is not a valid URL token is reported like one that fails decryption, because both mean it was altered.
        private static byte[] Unprotect(string cookieValue)
        {
            byte[]? protectedPayload;
            try
            {
                protectedPayload = HttpServerUtility.UrlTokenDecode(cookieValue);
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("The sign-in cookie is not valid.", ex);
            }

            if (protectedPayload is null)
                throw new CryptographicException("The sign-in cookie is not valid.");

            return MachineKey.Unprotect(protectedPayload, PendingAuthorizationCookie.ProtectionPurpose)
                ?? throw new CryptographicException("The sign-in cookie is not valid.");
        }

        private static HttpCookie CreateCookie(string name, string value, DateTime expires)
        {
            return new HttpCookie(name, value)
            {
                // The constructor copies the domain of <httpCookies> in web.config, and a browser rejects a __Host- cookie
                // that names a domain, which would make every sign-in fail.
                Domain = null,
                HttpOnly = true,
                Secure = true,
                // The provider redirects back with a top-level GET request, which carries a SameSite=Lax cookie.
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expires
            };
        }

        // A repeated parameter is treated as missing: joining the values would produce a state that matches nothing.
        private static string? GetSingleValue(HttpRequestBase request, string name)
        {
            string[]? values = request.QueryString.GetValues(name);
            return values is { Length: 1 } ? values[0] : null;
        }
    }
}
