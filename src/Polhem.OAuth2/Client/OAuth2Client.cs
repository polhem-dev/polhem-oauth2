using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Runs the OAuth2 authorization code flow for one provider without depending on an HTTP framework. The application
    /// sends the user to the authorization URL, keeps the pending values, and completes the sign-in when the provider
    /// redirects back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The options are copied and validated when the client is created, so later changes to them have no effect.
    /// The client keeps no state between calls, so one instance per provider can serve every sign-in.
    /// </para>
    /// <para>
    /// This client is a confidential client: it sends the client secret to the token endpoint whenever one is set.
    /// Desktop and console applications use <see cref="LoopbackOAuth2Client"/> instead, and mobile applications use
    /// <see cref="AppOAuth2Client"/>.
    /// </para>
    /// </remarks>
    public sealed class OAuth2Client
    {
        private readonly OAuth2Provider _provider;
        private readonly bool _isPublicClient;
        private readonly bool _usePkce;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <param name="httpClient">
        /// The HTTP client for requests to the provider, or null to use a shared instance. The client does not dispose it.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The client ID is empty, the redirect URI is not an absolute http or https URI, a scope is empty, or an endpoint is
        /// not an absolute https URI without a fragment.
        /// </exception>
        public OAuth2Client(OAuth2Options options, HttpClient? httpClient = null) : this(options, httpClient, publicClient: false)
        {
        }

        internal OAuth2Client(OAuth2Options options, HttpClient? httpClient, bool publicClient, bool appRedirectUri = false)
            : this(options, OAuth2Provider.HttpClientFactoryFor(httpClient), publicClient, appRedirectUri)
        {
        }

        private OAuth2Client(OAuth2Options options, Func<HttpClient>? httpClientFactory, bool publicClient, bool appRedirectUri)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var copy = options.Clone();
            if (copy.GetValidationError(appRedirectUri) is { } error)
                throw new ArgumentException(error, nameof(options));

            _provider = OAuth2Provider.Create(copy, httpClientFactory);
            _isPublicClient = publicClient;
            // RFC 8252 requires PKCE for native applications, because their client secret cannot be kept confidential.
            _usePkce = publicClient || copy.UsePkce;
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string ProviderName => _provider.ProviderName;

        /// <summary>
        /// Creates a client that asks for the HTTP client of each request to the provider, for an application that manages
        /// HTTP clients itself, for example with <c>IHttpClientFactory</c>. A constructor cannot take this parameter, because
        /// a call that passes null for the HTTP client would no longer compile.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <param name="httpClientFactory">
        /// Returns the HTTP client for a request to the provider. It is called for each request, so a client that the factory
        /// replaces from time to time is picked up. The client does not dispose what it returns.
        /// </param>
        /// <returns>The client.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="httpClientFactory"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The client ID is empty, the redirect URI is not an absolute http or https URI, a scope is empty, or an endpoint is
        /// not an absolute https URI without a fragment.
        /// </exception>
        public static OAuth2Client Create(OAuth2Options options, Func<HttpClient> httpClientFactory)
        {
            if (httpClientFactory is null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            return new OAuth2Client(options, httpClientFactory, publicClient: false, appRedirectUri: false);
        }

        /// <summary>
        /// Gets the redirect URI of the copied options, which were validated when the client was created.
        /// </summary>
        internal string RedirectUri => _provider.Options.RedirectUri;

        /// <summary>
        /// Creates the authorization request for a new sign-in, with a new random state and, when PKCE is used, a new code verifier.
        /// </summary>
        /// <returns>
        /// The authorization request. Send the user to its <see cref="AuthorizationRequest.Url"/>, and keep its
        /// <see cref="AuthorizationRequest.Pending"/> values for <see cref="CompleteAuthorizationAsync"/>.
        /// </returns>
        public AuthorizationRequest CreateAuthorizationRequest()
        {
            return CreateAuthorizationRequest(RedirectUri);
        }

        /// <summary>
        /// Creates the authorization request for a new sign-in with a specific redirect URI.
        /// </summary>
        /// <param name="redirectUri">The redirect URI to send with the authorization request and the token request.</param>
        /// <returns>The authorization request.</returns>
        internal AuthorizationRequest CreateAuthorizationRequest(string redirectUri)
        {
            string state = CreateState();
            string? codeVerifier = _usePkce ? Pkce.GenerateCodeVerifier() : null;
            string? codeChallenge = codeVerifier is null ? null : Pkce.GenerateCodeChallenge(codeVerifier);

            string url = _provider.GetAuthorizationUrl(state, redirectUri, codeChallenge);
            return new AuthorizationRequest(url, new PendingAuthorization(state, codeVerifier, redirectUri));
        }

        /// <summary>
        /// Completes a sign-in when the provider redirects back: checks the callback against the pending authorization,
        /// exchanges the authorization code for tokens, and retrieves the user information.
        /// </summary>
        /// <param name="callback">The query parameters of the redirect back to the application.</param>
        /// <param name="pending">The values kept from <see cref="CreateAuthorizationRequest()"/> for this sign-in.</param>
        /// <param name="cancellationToken">Cancels the requests to the provider.</param>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// <para>
        /// Only the failures a sign-in is expected to produce become a failed result: an <see cref="OAuth2Exception"/> when
        /// the state does not match, the provider returned an error, the authorization code or the PKCE code verifier is
        /// missing, or the token endpoint returned an error or a token type other than Bearer; an
        /// <see cref="HttpRequestException"/>; a
        /// <see cref="TaskCanceledException"/> when a request times out; and a <see cref="JsonException"/> for a response
        /// that is not valid JSON. Any other exception propagates.
        /// </para>
        /// <para>Remove the pending values from storage before calling this method, so that they cannot be used again.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> or <paramref name="pending"/> is null.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public async Task<AuthorizationResult> CompleteAuthorizationAsync(
            AuthorizationCallback callback, PendingAuthorization pending, CancellationToken cancellationToken = default)
        {
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));
            if (pending is null)
                throw new ArgumentNullException(nameof(pending));

            try
            {
                // The state is checked first: until it matches, the callback may come from a link that someone else crafted.
                if (!string.Equals(callback.State, pending.State, StringComparison.Ordinal))
                    throw new OAuth2Exception("The state does not match the state of the authorization request.");
                if (callback.Error is { Length: > 0 } providerError)
                    throw OAuth2Exception.FromProviderError(providerError, callback.ErrorDescription);
                if (callback.Code is not { } code || string.IsNullOrWhiteSpace(code))
                    throw new OAuth2Exception("The authorization code is missing.");
                if (_usePkce && pending.CodeVerifier is null)
                    throw new OAuth2Exception("The PKCE code verifier of the authorization request is missing.");

                TokenResponse token = await _provider
                    .ExchangeCodeAsync(code, pending.RedirectUri, _usePkce ? pending.CodeVerifier : null, _isPublicClient, cancellationToken)
                    .ConfigureAwait(false);
                UserInfo userInfo = await _provider.GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);

                return AuthorizationResult.Success(_provider.ProviderName, token, userInfo);
            }
            catch (OAuth2Exception ex)
            {
                return AuthorizationResult.Failure(ex);
            }
            catch (HttpRequestException ex)
            {
                return AuthorizationResult.Failure(ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return AuthorizationResult.Failure(ex);
            }
            catch (JsonException ex)
            {
                return AuthorizationResult.Failure(ex);
            }
        }

        /// <summary>
        /// Obtains new tokens with a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token from an earlier <see cref="TokenResponse"/>.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The new tokens. Keep the refresh token of the new response if it has one.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="refreshToken"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="refreshToken"/> is empty.</exception>
        /// <exception cref="NotSupportedException">The provider does not issue refresh tokens, as with Facebook.</exception>
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token or names a token type other than Bearer.</exception>
        /// <exception cref="HttpRequestException">
        /// The request failed, or the token endpoint returned an unsuccessful status code without an error code.
        /// </exception>
        /// <exception cref="JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (refreshToken is null)
                throw new ArgumentNullException(nameof(refreshToken));
            if (refreshToken.Length == 0)
                throw new ArgumentException("The refresh token cannot be empty.", nameof(refreshToken));

            return _provider.RefreshTokenAsync(refreshToken, _isPublicClient, cancellationToken);
        }

        // A state has the same requirements as a PKCE code verifier: enough cryptographically random bytes, encoded to be safe in a URL.
        private static string CreateState()
        {
            return Pkce.GenerateCodeVerifier();
        }
    }
}
