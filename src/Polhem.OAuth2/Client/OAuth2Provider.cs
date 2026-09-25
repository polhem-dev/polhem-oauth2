using System.Net.Http.Headers;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for the supported providers. It builds the authorization URL, requests tokens from the token endpoint,
    /// and retrieves user information.
    /// </summary>
    internal abstract class OAuth2Provider
    {
        private readonly Func<HttpClient> _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        protected OAuth2Provider(OAuth2Options options, Func<HttpClient>? httpClientFactory)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));
            if (options.GetEndpointError() is { } error)
                throw new ArgumentException(error, nameof(options));

            Options = options;
            _httpClientFactory = httpClientFactory ?? SharedHttpClientFactory;
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Gets the OAuth2 options.
        /// </summary>
        public OAuth2Options Options { get; }

        /// <summary>
        /// Gets a value indicating whether the client secret is sent to the token endpoint even from a public client.
        /// </summary>
        protected virtual bool RequiresClientSecret => false;

        /// <summary>
        /// Gets a value indicating whether the provider issues refresh tokens.
        /// </summary>
        protected virtual bool SupportsRefreshToken => true;

        /// <summary>
        /// Creates the provider that matches the type of the options.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        /// <returns>The provider.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        public static OAuth2Provider Create(OAuth2Options options, HttpClient? httpClient)
        {
            return Create(options, HttpClientFactoryFor(httpClient));
        }

        /// <summary>
        /// Creates the provider that matches the type of the options, asking for the HTTP client of each request.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <returns>The provider.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        public static OAuth2Provider Create(OAuth2Options options, Func<HttpClient>? httpClientFactory)
        {
            switch (options)
            {
                case null:
                    throw new ArgumentNullException(nameof(options));
                case GoogleOAuth2Options googleOptions:
                    return new GoogleOAuth2Provider(googleOptions, httpClientFactory);
                case LineOAuth2Options lineOptions:
                    return new LineOAuth2Provider(lineOptions, httpClientFactory);
                case AzureOAuth2Options azureOptions:
                    return new AzureOAuth2Provider(azureOptions, httpClientFactory);
                case FacebookOAuth2Options facebookOptions:
                    return new FacebookOAuth2Provider(facebookOptions, httpClientFactory);
                case Auth0OAuth2Options auth0Options:
                    return new Auth0OAuth2Provider(auth0Options, httpClientFactory);
                case OktaOAuth2Options oktaOptions:
                    return new OktaOAuth2Provider(oktaOptions, httpClientFactory);
                default:
                    throw new NotSupportedException("Unsupported OAuth provider.");
            }
        }

        /// <summary>
        /// Builds the URL that sends the user to the provider to sign in and authorize the application.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <param name="redirectUri">The URI the provider sends the user back to.</param>
        /// <param name="codeChallenge">The PKCE <c>code_challenge</c>, or null when PKCE is not used.</param>
        /// <returns>The authorization URL.</returns>
        public string GetAuthorizationUrl(string state, string redirectUri, string? codeChallenge)
        {
            var parameters = GetAuthorizationParameters(state, redirectUri, codeChallenge);
            string query = string.Join("&", parameters.Select(parameter => parameter.Key + "=" + Uri.EscapeDataString(parameter.Value)));
            return AppendQuery(Options.AuthorizationEndpoint, query);
        }

        /// <summary>
        /// Exchanges an authorization code for tokens.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <param name="redirectUri">The redirect URI sent with the authorization request.</param>
        /// <param name="codeVerifier">The PKCE <c>code_verifier</c>, or null when PKCE is not used.</param>
        /// <param name="publicClient">
        /// Whether the client is a public client, which cannot keep a client secret confidential and does not send it.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The tokens.</returns>
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the token endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> ExchangeCodeAsync(
            string authorizationCode, string redirectUri, string? codeVerifier, bool publicClient, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["redirect_uri"] = GetTokenRedirectUri(redirectUri),
                ["client_id"] = Options.ClientId
            };
            if (codeVerifier is { Length: > 0 } verifier)
                parameters["code_verifier"] = verifier;
            AddClientSecret(parameters, publicClient);
            return RequestTokenAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Obtains new tokens with a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="publicClient">
        /// Whether the client is a public client, which cannot keep a client secret confidential and does not send it.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The new tokens.</returns>
        /// <exception cref="NotSupportedException">The provider does not issue refresh tokens.</exception>
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the token endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, bool publicClient, CancellationToken cancellationToken)
        {
            if (!SupportsRefreshToken)
                throw new NotSupportedException($"{ProviderName} does not issue refresh tokens.");

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = Options.ClientId
            };
            AddClientSecret(parameters, publicClient);
            return RequestTokenAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Retrieves and parses the user information with the access token.
        /// </summary>
        /// <param name="token">The tokens returned by the token endpoint.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The user information.</returns>
        /// <exception cref="HttpRequestException">The request failed, or the endpoint returned an unsuccessful status code.</exception>
        /// <exception cref="OAuth2Exception">The response is empty.</exception>
        /// <exception cref="JsonException">The response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public async Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, GetUserInfoUrl()))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                using (var response = await _httpClientFactory().SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Failed to retrieve user information. Status code: {(int)response.StatusCode}.");

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                        throw new OAuth2Exception("The user information response is empty.");

                    return ParseUserJson(json, token);
                }
            }
        }

        /// <summary>
        /// Parses the JSON returned by the user information endpoint.
        /// </summary>
        /// <param name="json">The user information as a JSON string.</param>
        /// <param name="token">The tokens of the sign-in, for providers that put some user details in the ID token.</param>
        /// <returns>The parsed user information.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null or empty.</exception>
        /// <exception cref="JsonException"><paramref name="json"/> is not a JSON object.</exception>
        public UserInfo ParseUserJson(string json, TokenResponse? token = null)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty.");

            using (var document = OAuth2Json.ParseObject(json))
            {
                return CreateUserInfo(document.RootElement, json, token);
            }
        }

        /// <summary>
        /// Builds the query parameters of the authorization URL.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <param name="redirectUri">The URI the provider sends the user back to.</param>
        /// <param name="codeChallenge">The PKCE <c>code_challenge</c>, or null when PKCE is not used.</param>
        /// <returns>The query parameters keyed by name.</returns>
        protected virtual Dictionary<string, string> GetAuthorizationParameters(string state, string redirectUri, string? codeChallenge)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = Options.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(" ", Options.Scopes),
                ["state"] = state
            };
            if (codeChallenge is { Length: > 0 } challenge)
            {
                parameters["code_challenge"] = challenge;
                parameters["code_challenge_method"] = "S256";
            }
            return parameters;
        }

        /// <summary>
        /// Gets the redirect URI to send with the token request. The default is the redirect URI of the authorization request,
        /// as RFC 6749, section 4.1.3 requires.
        /// </summary>
        /// <param name="redirectUri">The redirect URI sent with the authorization request.</param>
        /// <returns>The redirect URI for the token request.</returns>
        protected virtual string GetTokenRedirectUri(string redirectUri)
        {
            return redirectUri;
        }

        /// <summary>
        /// Adds parameters to an endpoint, after the query that the endpoint already has. RFC 6749, section 3.1, lets an
        /// endpoint include a query, which must be retained.
        /// </summary>
        /// <param name="endpoint">The endpoint, without a fragment.</param>
        /// <param name="query">The encoded parameters to add, without a leading separator.</param>
        /// <returns>The endpoint with the parameters.</returns>
        protected static string AppendQuery(string endpoint, string query)
        {
            int start = endpoint.IndexOf('?');
            if (start < 0)
                return endpoint + "?" + query;

            char last = endpoint[endpoint.Length - 1];
            return last == '?' || last == '&' ? endpoint + query : endpoint + "&" + query;
        }

        /// <summary>
        /// Gets the URL of the user information endpoint. The default is <see cref="OAuth2Options.UserInfoEndpoint"/>.
        /// </summary>
        /// <returns>The user information URL.</returns>
        protected virtual string GetUserInfoUrl()
        {
            return Options.UserInfoEndpoint;
        }

        /// <summary>
        /// Reads the error that an unsuccessful token response reports. The default reads the <c>error</c> and
        /// <c>error_description</c> strings of RFC 6749, section 5.2.
        /// </summary>
        /// <param name="response">The root object of the token response.</param>
        /// <returns>The exception for the error, or null when the response names no error.</returns>
        protected virtual OAuth2Exception? ReadError(JsonElement response)
        {
            return OAuth2Json.GetProtocolString(response, "error") is { Length: > 0 } error
                ? OAuth2Exception.FromProviderError(error, OAuth2Json.GetProtocolString(response, "error_description"))
                : null;
        }

        /// <summary>
        /// Maps the fields of the user information object.
        /// </summary>
        /// <param name="user">The root object of the user information response.</param>
        /// <param name="json">The raw JSON of the response.</param>
        /// <param name="token">The tokens of the sign-in, or null when they are not available.</param>
        /// <returns>The user information.</returns>
        protected abstract UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token);

        /// <summary>
        /// Wraps one HTTP client, or the shared instance when it is null, as the factory of every request.
        /// </summary>
        /// <param name="httpClient">The HTTP client, or null.</param>
        /// <returns>A factory that returns that client.</returns>
        public static Func<HttpClient>? HttpClientFactoryFor(HttpClient? httpClient)
        {
            if (httpClient is null)
                return null;
            return () => httpClient;
        }

        private static HttpClient SharedHttpClientFactory()
        {
            return SharedHttpClient.Instance;
        }

        private void AddClientSecret(Dictionary<string, string> parameters, bool publicClient)
        {
            // A public client authenticates the code exchange with PKCE, because its client secret cannot be kept confidential (ADR-004).
            if (Options.ClientSecret is { Length: > 0 } clientSecret && (!publicClient || RequiresClientSecret))
                parameters["client_secret"] = clientSecret;
        }

        private async Task<TokenResponse> RequestTokenAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            using (var content = new FormUrlEncodedContent(parameters))
            using (var response = await _httpClientFactory().PostAsync(Options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw (Exception?)ReadErrorResponse(body)
                        ?? new HttpRequestException($"The token endpoint returned status code {(int)response.StatusCode}.");
                }
                return ReadTokenResponse(body);
            }
        }

        private OAuth2Exception? ReadErrorResponse(string body)
        {
            try
            {
                using (var document = OAuth2Json.ParseObject(body))
                {
                    return ReadError(document.RootElement);
                }
            }
            catch (JsonException)
            {
                // A body that is not a JSON object, such as an HTML error page, carries no error code.
                return null;
            }
        }

        private static TokenResponse ReadTokenResponse(string json)
        {
            using (var document = OAuth2Json.ParseObject(json))
            {
                var root = document.RootElement;
                if (OAuth2Json.GetProtocolString(root, "access_token") is not { Length: > 0 } accessToken)
                    throw new OAuth2Exception("The token response does not contain an access token.");

                return new TokenResponse(
                    accessToken,
                    OAuth2Json.GetProtocolString(root, "token_type"),
                    OAuth2Json.GetSeconds(root, "expires_in"),
                    OAuth2Json.GetProtocolString(root, "refresh_token"),
                    OAuth2Json.GetProtocolString(root, "id_token"),
                    OAuth2Json.GetProtocolString(root, "scope"),
                    json);
            }
        }
    }
}
