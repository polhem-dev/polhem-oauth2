using System.Net.Http.Headers;
using System.Text;
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
            if (httpClientFactory is null)
            {
                SharedHttpClient.UseEndpoint(options.TokenEndpoint);
                SharedHttpClient.UseEndpoint(options.UserInfoEndpoint);
            }
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
        public static OAuth2Provider Create(OAuth2Options options, Func<HttpClient>? httpClientFactory)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            return options.CreateProvider(httpClientFactory);
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
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token or names a token type other than Bearer.</exception>
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
            return RequestTokenAsync(parameters, AddClientAuthentication(parameters, publicClient), cancellationToken);
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
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token or names a token type other than Bearer.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the token endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, bool publicClient, CancellationToken cancellationToken)
        {
            // Returned as a faulted task, like every other failure of the request, so that a caller who starts the task and
            // awaits it later still catches the exception.
            if (!SupportsRefreshToken)
                return Task.FromException<TokenResponse>(new NotSupportedException($"{ProviderName} does not issue refresh tokens."));

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = Options.ClientId
            };
            return RequestTokenAsync(parameters, AddClientAuthentication(parameters, publicClient), cancellationToken);
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

                    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="json"/> is empty.</exception>
        /// <exception cref="JsonException"><paramref name="json"/> is not a JSON object.</exception>
        public UserInfo ParseUserJson(string json, TokenResponse? token)
        {
            if (json is null)
                throw new ArgumentNullException(nameof(json));
            if (json.Length == 0)
                throw new ArgumentException("The JSON cannot be empty.", nameof(json));

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
        /// Gets the display name of a user from an OpenID Connect user information response: the <c>name</c> claim, or the
        /// <c>given_name</c> and <c>family_name</c> claims joined with a space when the provider returned those but no name.
        /// </summary>
        /// <param name="user">The root object of the user information response.</param>
        /// <returns>The display name, or null when the response has neither the name nor any of its parts.</returns>
        protected static string? GetOidcDisplayName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "name") ?? JoinNameParts(user, "given_name", "family_name");
        }

        /// <summary>
        /// Joins the given and family names of a user with a space, leaving out a part that is missing or blank.
        /// </summary>
        /// <param name="user">The root object of the user information response.</param>
        /// <param name="givenNameField">The field that holds the given name.</param>
        /// <param name="familyNameField">The field that holds the family name.</param>
        /// <returns>The joined name, or null when both parts are missing or blank.</returns>
        protected static string? JoinNameParts(JsonElement user, string givenNameField, string familyNameField)
        {
            string?[] parts = { OAuth2Json.GetString(user, givenNameField), OAuth2Json.GetString(user, familyNameField) };
            string name = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
            return name.Length == 0 ? null : name;
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

        // Adds the client secret to the body, or returns the Basic header that carries it, as the options ask.
        private AuthenticationHeaderValue? AddClientAuthentication(Dictionary<string, string> parameters, bool publicClient)
        {
            // A public client authenticates the code exchange with PKCE, because its client secret cannot be kept confidential (ADR-004).
            if (Options.ClientSecret is not { Length: > 0 } clientSecret || (publicClient && !RequiresClientSecret))
                return null;

            if (Options.ClientAuthentication == ClientAuthenticationMethod.ClientSecretBasic)
            {
                // RFC 6749, section 2.3.1: the client ID and the secret are form-encoded before they are joined and encoded as
                // base64. The client authenticates in the header, so the body leaves out the client ID (section 4.1.3).
                parameters.Remove("client_id");
                string credentials = FormEncode(Options.ClientId) + ":" + FormEncode(clientSecret);
                return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }

            parameters["client_secret"] = clientSecret;
            return null;
        }

        private static string FormEncode(string value)
        {
            return Uri.EscapeDataString(value).Replace("%20", "+");
        }

        private async Task<TokenResponse> RequestTokenAsync(
            Dictionary<string, string> parameters, AuthenticationHeaderValue? authorization, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint))
            {
                request.Content = new FormUrlEncodedContent(parameters);
                request.Headers.Authorization = authorization;
                using (var response = await _httpClientFactory().SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw (Exception?)ReadErrorResponse(body)
                            ?? new HttpRequestException($"The token endpoint returned status code {(int)response.StatusCode}.");
                    }
                    return ReadTokenResponse(body);
                }
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

                // RFC 6749, section 7.1: a client must not use an access token whose type it does not understand, and the
                // user information request sends it as a bearer token (RFC 6750). The comparison ignores case, because
                // Facebook writes the type in lower case. A response without a type is accepted and used as a bearer token.
                string? tokenType = OAuth2Json.GetProtocolString(root, "token_type");
                if (tokenType is not null && !string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
                    throw new OAuth2Exception($"The token response names the token type '{tokenType}', and only Bearer is supported.");

                return new TokenResponse(
                    accessToken,
                    tokenType,
                    OAuth2Json.GetSeconds(root, "expires_in"),
                    OAuth2Json.GetProtocolString(root, "refresh_token"),
                    OAuth2Json.GetProtocolString(root, "id_token"),
                    OAuth2Json.GetProtocolString(root, "scope"),
                    json);
            }
        }
    }
}
