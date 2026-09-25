using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace Polhem.OAuth2.AspNetCore
{
    // The back-end relay of ADR-006: a sign-in started by a mobile application, returned to it with a single-use code,
    // and redeemed with the application's code verifier.
    public sealed partial class OAuth2Manager
    {
        private const string RelayCacheKeyPrefix = "Polhem.OAuth2.AppRelay.";

        // Keeps a protected relay entry apart from other data protected with the same keys. The version changes when the
        // format changes in a way that an earlier version cannot read.
        private const string RelayProtectionPurpose = "Polhem.OAuth2.AppRelayEntry.v1";

        // RFC 7636: an S256 challenge of 32 random bytes is 43 base64url characters, and a verifier has 43 to 128 characters.
        private const int CodeChallengeLength = 43;
        private const int MinCodeVerifierLength = 43;
        private const int MaxCodeVerifierLength = 128;

        // A relay code is this many random bytes in base64url, which is always 43 characters.
        private const int RelayCodeBytes = 32;
        private const int RelayCodeLength = 43;

        // The cache ends the lifetime of a code, on its own clock. The time in the entry is checked as well, for a cache that
        // returns an entry it should have dropped, and that check tolerates a server whose clock runs ahead of the server
        // that issued the code, which would otherwise end the lifetime early.
        private static readonly TimeSpan s_clockSkew = TimeSpan.FromMinutes(1);

        private static readonly object s_appSignInKey = new();

        /// <summary>
        /// Starts a sign-in relayed to a mobile application (ADR-006) and redirects the response to the authorization URL.
        /// The provider returns to the client's web redirect URI, so no other redirect URI is registered with the provider.
        /// </summary>
        /// <param name="context">The HTTP context of the request that the application opened to start the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="appRedirectUri">
        /// The application redirect URI to return to after the sign-in. It must equal one of
        /// <see cref="OAuth2AppRelayOptions.AppRedirectUris"/>.
        /// </param>
        /// <param name="codeChallenge">
        /// The base64url SHA-256 hash of a code verifier that the application created and keeps, as in PKCE (RFC 7636). The
        /// application presents the verifier to <see cref="RedeemAppCodeAsync"/>.
        /// </param>
        /// <remarks>
        /// The application redirect URI and the code challenge are kept in the protected sign-in cookie, so the application
        /// must open this request in the browser session that later follows the redirect to the provider and back, as
        /// <c>WebAuthenticator</c> does. When the values come from the request, as they do in an endpoint that an application
        /// opens, call <see cref="TryRedirectToAppAuthorization"/>, which reports a value that is not valid without an exception.
        /// </remarks>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="appRedirectUri"/> is not registered, or <paramref name="codeChallenge"/> is not 43 base64url characters.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The relay is not registered, or no client is registered under <paramref name="clientName"/>.
        /// </exception>
        public void RedirectToAppAuthorization(HttpContext context, string clientName, string appRedirectUri, string codeChallenge)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            context.Response.Redirect(CreateAppAuthorizationUrl(context, clientName, appRedirectUri, codeChallenge));
        }

        /// <summary>
        /// Starts a sign-in relayed to a mobile application, as <see cref="RedirectToAppAuthorization"/> does, and returns the
        /// authorization URL instead of redirecting the response, for a caller that redirects in its own way.
        /// </summary>
        /// <param name="context">The HTTP context of the request that the application opened to start the sign-in.</param>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="appRedirectUri">The application redirect URI to return to, one of <see cref="OAuth2AppRelayOptions.AppRedirectUris"/>.</param>
        /// <param name="codeChallenge">The S256 code challenge of the application, as for <see cref="RedirectToAppAuthorization"/>.</param>
        /// <returns>The URL to redirect the user to. The response carries the sign-in cookie.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="appRedirectUri"/> is not registered, or <paramref name="codeChallenge"/> is not 43 base64url characters.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The relay is not registered, or no client is registered under <paramref name="clientName"/>.
        /// </exception>
        public string CreateAppAuthorizationUrl(HttpContext context, string clientName, string appRedirectUri, string codeChallenge)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (appRedirectUri is null)
                throw new ArgumentNullException(nameof(appRedirectUri));
            if (codeChallenge is null)
                throw new ArgumentNullException(nameof(codeChallenge));

            var relay = RequireRelay();
            if (!relay.IsRegistered(appRedirectUri))
                throw new ArgumentException("The application redirect URI is not registered with the relay.", nameof(appRedirectUri));
            if (!IsCodeChallenge(codeChallenge))
                throw new ArgumentException($"The code challenge must be {CodeChallengeLength} base64url characters.", nameof(codeChallenge));

            return StartSignIn(context, clientName, appRedirectUri, codeChallenge);
        }

        /// <summary>
        /// Starts a sign-in relayed to a mobile application, as <see cref="RedirectToAppAuthorization"/> does, with values that
        /// come from the request: it returns false for a value that is not valid, instead of throwing.
        /// </summary>
        /// <param name="context">The HTTP context of the request that the application opened to start the sign-in.</param>
        /// <param name="clientName">The requested client name, or null if the request has none.</param>
        /// <param name="appRedirectUri">The requested application redirect URI, or null if the request has none.</param>
        /// <param name="codeChallenge">The requested code challenge, or null if the request has none.</param>
        /// <returns>
        /// True if the response now redirects to the authorization URL. False, with the response left unchanged, if no client
        /// is registered under <paramref name="clientName"/>, <paramref name="appRedirectUri"/> is not one of
        /// <see cref="OAuth2AppRelayOptions.AppRedirectUris"/>, <paramref name="codeChallenge"/> is not 43 base64url
        /// characters, or one of them is null. Answer such a request with status 400.
        /// </returns>
        /// <remarks>
        /// Anyone can open the endpoint with any values, so a value that is not valid is an ordinary request, not an error of
        /// the application. A relay that is not registered is one, and throws.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The relay is not registered.</exception>
        public bool TryRedirectToAppAuthorization(HttpContext context, string? clientName, string? appRedirectUri, string? codeChallenge)
        {
            if (!TryCreateAppAuthorizationUrl(context, clientName, appRedirectUri, codeChallenge, out string? url))
                return false;

            context.Response.Redirect(url);
            return true;
        }

        /// <summary>
        /// Starts a sign-in relayed to a mobile application with values that come from the request, as
        /// <see cref="TryRedirectToAppAuthorization"/> does, and returns the authorization URL instead of redirecting the
        /// response.
        /// </summary>
        /// <param name="context">The HTTP context of the request that the application opened to start the sign-in.</param>
        /// <param name="clientName">The requested client name, or null if the request has none.</param>
        /// <param name="appRedirectUri">The requested application redirect URI, or null if the request has none.</param>
        /// <param name="codeChallenge">The requested code challenge, or null if the request has none.</param>
        /// <param name="url">The URL to redirect the user to, or null when the method returns false.</param>
        /// <returns>
        /// True if the sign-in started, with its cookie on the response. False, with the response left unchanged, for the
        /// values that <see cref="TryRedirectToAppAuthorization"/> refuses.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The relay is not registered.</exception>
        public bool TryCreateAppAuthorizationUrl(
            HttpContext context, string? clientName, string? appRedirectUri, string? codeChallenge, [NotNullWhen(true)] out string? url)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var relay = RequireRelay();
            url = null;
            if (clientName is null || appRedirectUri is null || codeChallenge is null
                || GetClient(clientName) is null || !relay.IsRegistered(appRedirectUri) || !IsCodeChallenge(codeChallenge))
            {
                return false;
            }

            url = StartSignIn(context, clientName, appRedirectUri, codeChallenge);
            return true;
        }

        /// <summary>
        /// Returns a relayed sign-in to the application: stores the user information under a new single-use code and
        /// redirects to the application redirect URI with that code, or with an error when the sign-in failed.
        /// </summary>
        /// <param name="context">The HTTP context of the callback request, after <see cref="CompleteAuthorizationAsync"/>.</param>
        /// <param name="result">The result that <see cref="CompleteAuthorizationAsync"/> returned for this request.</param>
        /// <param name="cancellationToken">Cancels storing the code. It is also canceled when the callback request is aborted.</param>
        /// <returns>
        /// True if the sign-in was relayed and the response now redirects to the application; false for a web sign-in, whose
        /// response is left unchanged.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The redirect carries only the code, or the error code of a failed sign-in, never a provider token. The code can be
        /// redeemed once, within <see cref="OAuth2AppRelayOptions.CodeLifetime"/>, with <see cref="RedeemAppCodeAsync"/>.
        /// </para>
        /// <para>
        /// A callback whose sign-in cookie is missing or altered cannot be recognized as relayed, so it is handled as a web
        /// sign-in and this method returns false.
        /// </para>
        /// <para>
        /// When this method returns true, return from the callback without signing the user in to the web application. The
        /// sign-in ran in a browser session that the application opened, which can share its cookies with the browser of the
        /// device, so a session created there would sign the user in to the web application as well.
        /// </para>
        /// <para>
        /// The user information is kept in the cache protected with ASP.NET Core data protection, so reading the cache does
        /// not reveal it, and an entry written by anyone without the keys is not redeemed.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="result"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The relay is not registered, or the sign-in names an application redirect URI that is no longer registered.
        /// </exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled, or the request was aborted.</exception>
        public async Task<bool> RedirectToAppAsync(HttpContext context, AuthorizationResult result, CancellationToken cancellationToken = default)
        {
            string? url = await CreateAppRedirectUrlAsync(context, result, cancellationToken).ConfigureAwait(false);
            if (url is null)
                return false;

            context.Response.Redirect(url);
            return true;
        }

        /// <summary>
        /// Returns a relayed sign-in to the application, as <see cref="RedirectToAppAsync"/> does, and returns the URL of the
        /// application to redirect to instead of redirecting the response.
        /// </summary>
        /// <param name="context">The HTTP context of the callback request, after <see cref="CompleteAuthorizationAsync"/>.</param>
        /// <param name="result">The result that <see cref="CompleteAuthorizationAsync"/> returned for this request.</param>
        /// <param name="cancellationToken">Cancels storing the code. It is also canceled when the callback request is aborted.</param>
        /// <returns>
        /// The application redirect URI with the code, or with the error of a failed sign-in, or null for a web sign-in.
        /// </returns>
        /// <remarks>The remarks of <see cref="RedirectToAppAsync"/> apply.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="result"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The relay is not registered, or the sign-in names an application redirect URI that is no longer registered.
        /// </exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled, or the request was aborted.</exception>
        public async Task<string?> CreateAppRedirectUrlAsync(HttpContext context, AuthorizationResult result, CancellationToken cancellationToken = default)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (result is null)
                throw new ArgumentNullException(nameof(result));
            if (!context.Items.TryGetValue(s_appSignInKey, out object? item) || item is not AppSignIn signIn)
                return null;

            context.Items.Remove(s_appSignInKey);
            var relay = RequireRelay();
            // The cookie is protected, so the URI was registered when the sign-in started. It may have been removed since.
            if (!relay.IsRegistered(signIn.AppRedirectUri))
                throw new InvalidOperationException("The sign-in was started with an application redirect URI that is no longer registered with the relay.");

            string separator = signIn.AppRedirectUri.IndexOf('?') >= 0 ? "&" : "?";

            if (!result.IsSuccess)
            {
                // Only the error code of the provider is passed on; any other failure is reported without its details.
                string error = result.Exception is OAuth2Exception { Error: { Length: > 0 } providerError } ? providerError : "sign_in_failed";
                return signIn.AppRedirectUri + separator + "error=" + Uri.EscapeDataString(error);
            }

            string code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(RelayCodeBytes));
            byte[] entry = _relayProtector.Protect(SerializeRelayEntry(signIn, result.UserInfo, _timeProvider.GetUtcNow() + relay.CodeLifetime));
            var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = relay.CodeLifetime };
            using (var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.RequestAborted))
            {
                await _relayCache!.SetAsync(GetRelayCacheKey(code), entry, entryOptions, requestCancellation.Token).ConfigureAwait(false);
            }

            return signIn.AppRedirectUri + separator + "code=" + code;
        }

        /// <summary>
        /// Redeems the code that a relayed sign-in returned to the application, and returns the user information.
        /// </summary>
        /// <param name="clientName">The name of the client the sign-in used.</param>
        /// <param name="code">The code from the redirect to the application.</param>
        /// <param name="codeVerifier">The verifier whose challenge the application passed to <see cref="RedirectToAppAuthorization"/>.</param>
        /// <param name="cancellationToken">Cancels the cache requests.</param>
        /// <returns>
        /// The user information, or null when the code is unknown, expired or already redeemed, belongs to another client,
        /// the verifier does not match, or the entry in the cache was not protected with the keys of this application.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Call this from an endpoint that the application reaches over HTTPS, then issue the application's own session for
        /// the user. The result carries no provider token.
        /// </para>
        /// <para>
        /// The code is removed on the first attempt, whether or not the verifier matches. <see cref="IDistributedCache"/> has
        /// no atomic read-and-remove, so two attempts that arrive at the same moment could both read it; both still need the
        /// verifier.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The relay is not registered.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public async Task<UserInfo?> RedeemAppCodeAsync(string clientName, string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            if (clientName is null)
                throw new ArgumentNullException(nameof(clientName));
            if (code is null)
                throw new ArgumentNullException(nameof(code));
            if (codeVerifier is null)
                throw new ArgumentNullException(nameof(codeVerifier));
            RequireRelay();

            if (code.Length != RelayCodeLength || !Base64UrlText.IsBase64Url(code)
                || codeVerifier.Length < MinCodeVerifierLength || codeVerifier.Length > MaxCodeVerifierLength || !IsCodeVerifier(codeVerifier))
            {
                return null;
            }

            string key = GetRelayCacheKey(code);
            byte[]? entry = await _relayCache!.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is null)
                return null;
            await _relayCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

            try
            {
                return ReadRelayEntry(_relayProtector.Unprotect(entry), clientName, codeVerifier, _timeProvider.GetUtcNow());
            }
            catch (CryptographicException)
            {
                // Not written by this application with the current keys, so not an entry that can be redeemed.
                return null;
            }
        }

        private AppRelaySettings RequireRelay()
        {
            return _relay is not null && _relayCache is not null
                ? _relay
                : throw new InvalidOperationException("The application relay is not registered. Call AddOAuth2AppRelay.");
        }

        // The cache holds a hash of the code, so a reader of the cache cannot learn a code that can be redeemed. The entry
        // itself is protected, which keeps the user information from that reader.
        private static string GetRelayCacheKey(string code)
        {
            return RelayCacheKeyPrefix + WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(code)));
        }

        private static byte[] SerializeRelayEntry(AppSignIn signIn, UserInfo user, DateTimeOffset expiresAt)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("client", signIn.ClientName);
                    writer.WriteString("challenge", signIn.CodeChallenge);
                    writer.WriteNumber("expiresAt", expiresAt.ToUnixTimeMilliseconds());
                    writer.WriteString("userId", user.UserId);
                    writer.WriteString("userName", user.UserName);
                    writer.WriteString("email", user.Email);
                    writer.WriteString("raw", user.RawJson);
                    writer.WriteEndObject();
                }
                return stream.ToArray();
            }
        }

        private static UserInfo? ReadRelayEntry(byte[] entry, string clientName, string codeVerifier, DateTimeOffset now)
        {
            try
            {
                using (var document = JsonDocument.Parse(entry))
                {
                    var root = document.RootElement;
                    string? challenge = root.GetStringProperty("challenge");
                    if (!string.Equals(root.GetStringProperty("client"), clientName, StringComparison.Ordinal)
                        || challenge is null
                        || !root.TryGetProperty("expiresAt", out var expires) || !expires.TryGetInt64(out long expiresAt)
                        || now.ToUnixTimeMilliseconds() >= expiresAt + (long)s_clockSkew.TotalMilliseconds
                        || !MatchesChallenge(codeVerifier, challenge)
                        || root.GetStringProperty("raw") is not { } raw)
                    {
                        return null;
                    }
                    return new UserInfo(root.GetStringProperty("userId"), root.GetStringProperty("userName"), root.GetStringProperty("email"), raw);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool MatchesChallenge(string codeVerifier, string challenge)
        {
            byte[] computed = Encoding.ASCII.GetBytes(WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier))));
            return CryptographicOperations.FixedTimeEquals(computed, Encoding.ASCII.GetBytes(challenge));
        }

        private static bool IsCodeChallenge(string value)
        {
            return value.Length == CodeChallengeLength && Base64UrlText.IsBase64Url(value);
        }

        // RFC 7636, section 4.1: a code verifier uses the unreserved characters, which are the base64url alphabet with a
        // period and a tilde.
        private static bool IsCodeVerifier(string value)
        {
            foreach (char c in value)
            {
                if (!(Base64UrlText.IsBase64UrlCharacter(c) || c == '.' || c == '~'))
                    return false;
            }
            return true;
        }

        private sealed class AppSignIn
        {
            public AppSignIn(string clientName, string appRedirectUri, string codeChallenge)
            {
                ClientName = clientName;
                AppRedirectUri = appRedirectUri;
                CodeChallenge = codeChallenge;
            }

            public string ClientName { get; }

            public string AppRedirectUri { get; }

            public string CodeChallenge { get; }
        }
    }
}
