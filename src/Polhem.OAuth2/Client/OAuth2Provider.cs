using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for OAuth2 providers. It builds the authorization URL, exchanges the authorization code for an
    /// access token, and retrieves user information.
    /// </summary>
    public abstract class OAuth2Provider : IOAuth2Provider
    {
        /// <summary>
        /// The HTTP client used for requests to the provider.
        /// </summary>
        protected readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        public OAuth2Provider(OAuth2Options options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            Options = options;
        }

        /// <inheritdoc/>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Gets the OAuth2 options.
        /// </summary>
        public OAuth2Options Options { get; private set; }

        /// <summary>
        /// Builds the query parameters of the authorization URL.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <param name="codeChallenge">The PKCE <c>code_challenge</c>, or an empty string when PKCE is not used.</param>
        /// <returns>The query parameters keyed by name.</returns>
        protected virtual Dictionary<string, string> GetAuthorizationUrlParams(string state, string codeChallenge = "")
        {
            var queryParams = new Dictionary<string, string>
            {
                { "client_id", Options.ClientId },
                { "redirect_uri", Options.RedirectUri },
                { "response_type", "code" },
                { "scope", string.Join(" ", Options.Scopes) },
                { "state", state }
            };

            if (!string.IsNullOrWhiteSpace(codeChallenge))
            {
                queryParams["code_challenge"] = codeChallenge;
                queryParams["code_challenge_method"] = "S256";
            }
            return queryParams;
        }

        /// <inheritdoc/>
        public virtual string GetAuthorizationUrl(string state, string codeChallenge = "")
        {
            var queryParams = GetAuthorizationUrlParams(state, codeChallenge);
            string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            return $"{Options.AuthorizationEndpoint}?{queryString}";
        }

        /// <inheritdoc/>
        public virtual string GetRedirectUrl()
        {
            return Options.RedirectUri;
        }

        /// <summary>
        /// Builds the form parameters of the token request.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <param name="codeVerifier">The PKCE <c>code_verifier</c>, or an empty string when PKCE is not used.</param>
        /// <returns>The form parameters keyed by name.</returns>
        protected virtual Dictionary<string, string> GetAccessTokenParams(string authorizationCode, string codeVerifier = "")
        {
            var requestParams = new Dictionary<string, string>
            {
                { "client_id", Options.ClientId },
                { "redirect_uri", Options.RedirectUri },
                { "code", authorizationCode },
                { "grant_type", "authorization_code" }
            };

            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                requestParams["code_verifier"] = codeVerifier;
            }
            else
            {
                requestParams["client_secret"] = Options.ClientSecret;
            }
            return requestParams;
        }

        /// <inheritdoc/>
        /// <exception cref="HttpRequestException">The token endpoint returned an unsuccessful status code.</exception>
        /// <exception cref="OAuth2Exception">The token response does not contain an access token.</exception>
        public virtual async Task<string> GetAccessTokenAsync(string authorizationCode, string codeVerifier = "")
        {
            var requestParams = GetAccessTokenParams(authorizationCode, codeVerifier);

            using (var requestBody = new FormUrlEncodedContent(requestParams))
            using (var response = await _httpClient.PostAsync(Options.TokenEndpoint, requestBody).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Failed to obtain an access token. Status code: {(int)response.StatusCode}.");

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var tokenData = JObject.Parse(responseContent);
                return tokenData["access_token"]?.ToString() ?? throw new OAuth2Exception("The token response does not contain an access token.");
            }
        }

        /// <summary>
        /// Gets the URL of the user information endpoint. The default is <see cref="OAuth2Options.UserInfoEndpoint"/>.
        /// </summary>
        /// <returns>The user information URL.</returns>
        protected virtual string GetUserInfoUrl()
        {
            return Options.UserInfoEndpoint;
        }

        /// <inheritdoc/>
        /// <exception cref="HttpRequestException">The user information endpoint returned an unsuccessful status code.</exception>
        public virtual async Task<string> GetUserInfoAsync(string accessToken)
        {
            string url = GetUserInfoUrl();
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Failed to retrieve user information. Status code: {(int)response.StatusCode}.");

                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc/>
        public abstract UserInfo ParseUserJson(string json);

        /// <summary>
        /// Builds the form parameters of the refresh token request.
        /// </summary>
        /// <param name="refreshToken">The refresh token issued together with the original access token.</param>
        /// <returns>The form parameters keyed by name.</returns>
        protected virtual Dictionary<string, string> GetRefreshAccessTokenParams(string refreshToken)
        {
            return new Dictionary<string, string>
            {
                { "client_id", Options.ClientId },
                { "client_secret", Options.ClientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            };
        }

        /// <inheritdoc/>
        /// <exception cref="HttpRequestException">The token endpoint returned an unsuccessful status code.</exception>
        /// <exception cref="OAuth2Exception">The token response does not contain an access token.</exception>
        public virtual async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            var parameters = GetRefreshAccessTokenParams(refreshToken);

            using (var requestBody = new FormUrlEncodedContent(parameters))
            using (var response = await _httpClient.PostAsync(Options.TokenEndpoint, requestBody).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Failed to refresh the access token. Status code: {(int)response.StatusCode}.");

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var tokenData = JObject.Parse(content);
                if (tokenData["access_token"]?.ToString() is not { Length: > 0 } accessToken)
                    throw new OAuth2Exception("The token response does not contain an access token.");

                return accessToken;
            }
        }
    }
}
