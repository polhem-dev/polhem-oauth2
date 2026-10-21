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
        private const string ClientIdParameter = "client_id";

        private readonly Func<HttpClient> _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        protected OAuth2Provider(OAuth2Options options, Func<HttpClient>? httpClientFactory) : this(options, httpClientFactory, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Provider"/> class for a provider that can tell which client an
        /// access token was issued to.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <param name="tokenVerificationEndpoint">
        /// The endpoint that <see cref="GetTokenClientIdsAsync"/> sends access tokens to, or null when the provider has none.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        protected OAuth2Provider(OAuth2Options options, Func<HttpClient>? httpClientFactory, string? tokenVerificationEndpoint)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.GetEndpointError() is { } error)
                throw new ArgumentException(error, nameof(options));

            Options = options;
            TokenVerificationEndpoint = tokenVerificationEndpoint;
            _httpClientFactory = httpClientFactory ?? SharedHttpClientFactory;
            if (httpClientFactory is null)
            {
                SharedHttpClient.UseEndpoint(options.TokenEndpoint);
                if (!string.IsNullOrEmpty(options.UserInfoEndpoint))
                    SharedHttpClient.UseEndpoint(options.UserInfoEndpoint);
                if (tokenVerificationEndpoint is not null)
                    SharedHttpClient.UseEndpoint(tokenVerificationEndpoint);
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
        /// Gets the endpoint that tells which client an access token was issued to, or null when the provider has none.
        /// </summary>
        protected string? TokenVerificationEndpoint { get; }

        /// <summary>
        /// Gets a value indicating whether the client secret is sent to the token endpoint even from a public client.
        /// </summary>
        protected virtual bool RequiresClientSecret => false;

        /// <summary>
        /// Gets a value indicating whether the provider issues refresh tokens.
        /// </summary>
        protected virtual bool SupportsRefreshToken => true;

        /// <summary>
        /// Gets a value indicating whether a public client, <see cref="LoopbackOAuth2Client"/> or <see cref="AppOAuth2Client"/>,
        /// can sign in with the provider. A provider that refuses every redirect URI such a client can use returns false.
        /// </summary>
        public virtual bool SupportsPublicClient => true;

        /// <summary>
        /// Gets a value indicating whether <see cref="AppOAuth2Client"/> can sign in with the provider directly. A provider
        /// whose code exchange fails without the client secret for every client type, such as GitHub, returns false, because
        /// a mobile application cannot keep the secret, while a desktop application may choose to. When <see cref="SupportsPublicClient"/> is false, the
        /// application client is refused before this value is read.
        /// </summary>
        public virtual bool SupportsAppClient => SupportsPublicClient;

        /// <summary>
        /// Gets a value indicating whether the provider returns to the redirect URI with a form POST from its own site
        /// (<c>response_mode=form_post</c>) rather than a GET redirect.
        /// </summary>
        public virtual bool UsesFormPost => false;

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
            ArgumentNullException.ThrowIfNull(options);

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
                [ClientIdParameter] = Options.ClientId
            };
            if (codeVerifier is { Length: > 0 } verifier)
                parameters["code_verifier"] = verifier;
            return RequestAuthenticatedTokenAsync(parameters, publicClient, cancellationToken);
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
                [ClientIdParameter] = Options.ClientId
            };
            return RequestAuthenticatedTokenAsync(parameters, publicClient, cancellationToken);
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
        public virtual async Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            string json = await GetUserInfoJsonAsync(token, cancellationToken).ConfigureAwait(false);
            return ParseUserJson(json, token);
        }

        /// <summary>
        /// Adds to the user information what the provider sent with the redirect rather than in a response to the library.
        /// The default adds nothing.
        /// </summary>
        /// <param name="user">The user information read from the provider's responses.</param>
        /// <param name="callback">The parameters of the redirect, whose state matched the sign-in.</param>
        /// <returns>The user information to return.</returns>
        public virtual UserInfo AddCallbackDetails(UserInfo user, AuthorizationCallback callback)
        {
            return user;
        }

        /// <summary>
        /// Retrieves the user information of an access token that the application did not obtain itself, after checking that
        /// the token was issued to <see cref="OAuth2Options.ClientId"/> or one of <see cref="OAuth2Options.AcceptedClientIds"/>.
        /// </summary>
        /// <param name="token">The access token, wrapped as a token response.</param>
        /// <param name="cancellationToken">Cancels the requests.</param>
        /// <returns>The user information.</returns>
        /// <exception cref="NotSupportedException">The provider cannot tell which client an access token was issued to.</exception>
        /// <exception cref="InvalidOperationException">The options lack a setting that the check needs.</exception>
        /// <exception cref="OAuth2Exception">
        /// The provider refused the access token, did not name its client, or named a client that is not accepted, or the user
        /// information response is empty.
        /// </exception>
        /// <exception cref="HttpRequestException">A request failed, or an endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">A response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">A request was canceled or timed out.</exception>
        public async Task<UserInfo> GetVerifiedUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<string> clientIds = await GetTokenClientIdsAsync(token.AccessToken, cancellationToken).ConfigureAwait(false);
            // IMPORTANT: The user information endpoint answers for any valid token, whichever application obtained it. Only this
            // check stops another application from signing in to the back end with a token that one of its users gave it.
            if (clientIds.Count == 0)
                throw new OAuth2Exception("The provider did not report which client the access token was issued to.");
            if (!clientIds.All(IsAcceptedClientId))
                throw new OAuth2Exception("The access token was issued to a client that is not accepted.");

            return await GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);
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
            ArgumentNullException.ThrowIfNull(json);
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
                [ClientIdParameter] = Options.ClientId,
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
        /// Gets the client secret to send to the token endpoint. The default is <see cref="OAuth2Options.ClientSecret"/>.
        /// </summary>
        /// <returns>The client secret, or an empty string when none is sent.</returns>
        protected virtual string GetClientSecret()
        {
            return Options.ClientSecret;
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
        /// Adds the headers that the provider's API asks for to a request for user information, after the access token is
        /// set. The default adds nothing.
        /// </summary>
        /// <param name="request">The request.</param>
        protected virtual void PrepareUserInfoRequest(HttpRequestMessage request)
        {
        }

        /// <summary>
        /// Creates a GET request for user information that carries the access token as a bearer token.
        /// </summary>
        /// <param name="url">The URL of the request.</param>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The request, which the caller disposes.</returns>
        protected HttpRequestMessage CreateUserInfoRequest(string url, string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            PrepareUserInfoRequest(request);
            return request;
        }

        /// <summary>
        /// Retrieves the JSON of the user information endpoint with the access token.
        /// </summary>
        /// <param name="token">The tokens returned by the token endpoint.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The JSON, which is not empty.</returns>
        /// <exception cref="HttpRequestException">The request failed, or the endpoint returned an unsuccessful status code.</exception>
        /// <exception cref="OAuth2Exception">The response is empty.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        protected async Task<string> GetUserInfoJsonAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(token);

            ProviderResponse response;
            using (var request = CreateUserInfoRequest(GetUserInfoUrl(), token.AccessToken))
            {
                response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to retrieve user information. Status code: {(int)response.StatusCode}.");

            string json = response.Body;
            if (string.IsNullOrWhiteSpace(json))
                throw new OAuth2Exception("The user information response is empty.");
            return json;
        }

        /// <summary>
        /// Sends a request to the provider with the HTTP client of the provider, after adding this library to the
        /// <c>User-Agent</c> header. It is the only member that uses the HTTP client, so every request to the provider gets
        /// the header.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The status code and the body of the response.</returns>
        /// <exception cref="HttpRequestException">
        /// The request failed, or the body is larger than <see cref="ProviderResponse.MaxBodyBytes"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        protected Task<ProviderResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return ProviderResponse.SendAsync(_httpClientFactory(), request, cancellationToken);
        }

        /// <summary>
        /// Reads the error that a response of the token endpoint, or an unsuccessful response of the token verification
        /// endpoint, reports. The token endpoint's successful responses are read too, because GitHub reports an error with
        /// status 200. The default reads the <c>error</c> and <c>error_description</c> strings of RFC 6749, section 5.2.
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
        /// Asks the provider which client an access token was issued to. The default throws <see cref="NotSupportedException"/>,
        /// so a provider without a way to tell cannot return the user information of a token from elsewhere.
        /// </summary>
        /// <param name="accessToken">The access token. Keep it out of exception messages and logs.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>
        /// The client IDs that the provider reports for the token, every one of which must be accepted, or an empty collection
        /// when the response names none.
        /// </returns>
        /// <exception cref="NotSupportedException">The provider cannot tell which client an access token was issued to.</exception>
        /// <exception cref="OAuth2Exception">The provider refused the access token.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">The response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        protected virtual Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                $"{ProviderName} offers no way to check which client an access token was issued to, so the user information of an access token from a front end is not available."));
        }

        /// <summary>
        /// Sends a request to the token verification endpoint and parses the response.
        /// </summary>
        /// <param name="request">The request, which carries the access token.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The parsed response, which the caller disposes.</returns>
        /// <exception cref="OAuth2Exception">The endpoint returned an error, which <see cref="ReadError"/> reads.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the endpoint returned an unsuccessful status code without an error code.</exception>
        /// <exception cref="JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        protected async Task<JsonDocument> SendTokenVerificationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = response.Body;
            // The messages name the status code but never the request URI, which can carry the access token in its query.
            if (!response.IsSuccessStatusCode)
            {
                throw (Exception?)ReadErrorResponse(body)
                    ?? new HttpRequestException($"The token verification endpoint returned status code {(int)response.StatusCode}.");
            }
            return OAuth2Json.ParseObject(body);
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

        // A client secret that cannot be produced, such as one that the platform cannot sign for Apple, fails the request as a
        // faulted task, like every other failure of it.
        private Task<TokenResponse> RequestAuthenticatedTokenAsync(
            Dictionary<string, string> parameters, bool publicClient, CancellationToken cancellationToken)
        {
            AuthenticationHeaderValue? authentication;
            try
            {
                authentication = AddClientAuthentication(parameters, publicClient);
            }
            catch (OAuth2Exception ex)
            {
                return Task.FromException<TokenResponse>(ex);
            }
            return RequestTokenAsync(parameters, authentication, cancellationToken);
        }

        // Adds the client secret to the body, or returns the Basic header that carries it, as the options ask.
        private AuthenticationHeaderValue? AddClientAuthentication(Dictionary<string, string> parameters, bool publicClient)
        {
            // A public client authenticates the code exchange with PKCE, because its client secret cannot be kept confidential (ADR-004).
            if ((publicClient && !RequiresClientSecret) || GetClientSecret() is not { Length: > 0 } clientSecret)
                return null;

            if (Options.ClientAuthentication == ClientAuthenticationMethod.ClientSecretBasic)
            {
                // RFC 6749, section 2.3.1: the client ID and the secret are form-encoded before they are joined and encoded as
                // base64. The client authenticates in the header, so the body leaves out the client ID (section 4.1.3).
                parameters.Remove(ClientIdParameter);
                string credentials = FormEncode(Options.ClientId) + ":" + FormEncode(clientSecret);
                return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }

            parameters["client_secret"] = clientSecret;
            return null;
        }

        private bool IsAcceptedClientId(string clientId)
        {
            return string.Equals(clientId, Options.ClientId, StringComparison.Ordinal)
                || Options.AcceptedClientIds.Contains(clientId, StringComparer.Ordinal);
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
                // RFC 6749, section 5.1, has the token endpoint answer in JSON, but GitHub answers in form encoding unless
                // the request asks for JSON.
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
                string body = response.Body;
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

        private TokenResponse ReadTokenResponse(string json)
        {
            using (var document = OAuth2Json.ParseObject(json))
            {
                var root = document.RootElement;
                // GitHub reports a failed token request with status 200 and the error in the body. RFC 6749 gives a
                // successful response no error field, so a response that names one is an error from any provider.
                if (ReadError(root) is { } error)
                    throw error;
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
