using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2ProviderTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";

        [Theory]
        [DisplayName("Create picks the provider that matches the type of the options")]
        [InlineData("Google")]
        [InlineData("Facebook")]
        [InlineData("LINE")]
        [InlineData("Azure")]
        [InlineData("Auth0")]
        [InlineData("Okta")]
        public void Create_OptionsType_ReturnsMatchingProvider(string providerName)
        {
            var provider = OAuth2Provider.Create(CreateOptions(providerName), httpClient: null);

            Assert.Equal(providerName, provider.ProviderName);
        }

        [Fact]
        [DisplayName("Create rejects null options")]
        public void Create_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => OAuth2Provider.Create(null!, httpClient: null));
        }

        [Theory]
        [DisplayName("Create rejects an endpoint that is not an absolute https URI, or that has a fragment")]
        [InlineData(nameof(OAuth2Options.AuthorizationEndpoint), "http://accounts.google.com/o/oauth2/v2/auth")]
        [InlineData(nameof(OAuth2Options.TokenEndpoint), "http://oauth2.googleapis.com/token")]
        [InlineData(nameof(OAuth2Options.UserInfoEndpoint), "/userinfo")]
        [InlineData(nameof(OAuth2Options.TokenEndpoint), "")]
        [InlineData(nameof(OAuth2Options.AuthorizationEndpoint), "https://accounts.google.com/o/oauth2/v2/auth#fragment")]
        [InlineData(nameof(OAuth2Options.AuthorizationEndpoint), "https://accounts.google.com/o/oauth2/v2/auth?prompt=consent#fragment")]
        [InlineData(nameof(OAuth2Options.TokenEndpoint), "https://oauth2.googleapis.com/token#")]
        [InlineData(nameof(OAuth2Options.UserInfoEndpoint), "https://openidconnect.googleapis.com/v1/userinfo#fragment")]
        public void Create_InsecureEndpoint_ThrowsArgumentException(string endpointName, string endpoint)
        {
            var options = new GoogleOAuth2Options();
            switch (endpointName)
            {
                case nameof(OAuth2Options.AuthorizationEndpoint):
                    options.AuthorizationEndpoint = endpoint;
                    break;
                case nameof(OAuth2Options.TokenEndpoint):
                    options.TokenEndpoint = endpoint;
                    break;
                default:
                    options.UserInfoEndpoint = endpoint;
                    break;
            }

            var exception = Assert.Throws<ArgumentException>(() => OAuth2Provider.Create(options, httpClient: null));

            Assert.Contains(endpointName, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Create rejects Auth0 options whose domain is not set")]
        public void Create_Auth0WithoutDomain_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => OAuth2Provider.Create(new Auth0OAuth2Options(), httpClient: null));
        }

        [Fact]
        [DisplayName("The authorization URL carries the client, redirect URI, scopes, state and PKCE challenge")]
        public void GetAuthorizationUrl_WithCodeChallenge_IncludesAllParameters()
        {
            var provider = OAuth2Provider.Create(CreateOptions("Google"), httpClient: null);

            string url = provider.GetAuthorizationUrl("state-value", RedirectUri, "challenge-value");

            Assert.StartsWith(provider.Options.AuthorizationEndpoint + "?", url, StringComparison.Ordinal);
            Assert.Equal("client-id", LoopbackTestHttp.GetQueryValue(url, "client_id"));
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(url, "redirect_uri"));
            Assert.Equal("code", LoopbackTestHttp.GetQueryValue(url, "response_type"));
            Assert.Equal("openid email profile", LoopbackTestHttp.GetQueryValue(url, "scope"));
            Assert.Equal("state-value", LoopbackTestHttp.GetQueryValue(url, "state"));
            Assert.Equal("challenge-value", LoopbackTestHttp.GetQueryValue(url, "code_challenge"));
            Assert.Equal("S256", LoopbackTestHttp.GetQueryValue(url, "code_challenge_method"));
        }

        [Theory]
        [DisplayName("The authorization URL keeps the query of the authorization endpoint and adds its parameters after it")]
        [InlineData("https://tenant.auth0.com/authorize?audience=https%3A%2F%2Fapi.example.com", "https://tenant.auth0.com/authorize?audience=https%3A%2F%2Fapi.example.com&client_id=")]
        [InlineData("https://tenant.auth0.com/authorize?audience=a&prompt=login", "https://tenant.auth0.com/authorize?audience=a&prompt=login&client_id=")]
        [InlineData("https://tenant.auth0.com/authorize?", "https://tenant.auth0.com/authorize?client_id=")]
        [InlineData("https://tenant.auth0.com/authorize?audience=a&", "https://tenant.auth0.com/authorize?audience=a&client_id=")]
        [InlineData("https://tenant.auth0.com/authorize", "https://tenant.auth0.com/authorize?client_id=")]
        public void GetAuthorizationUrl_EndpointWithQuery_KeepsQuery(string endpoint, string expectedStart)
        {
            var options = new Auth0OAuth2Options { Domain = "tenant.auth0.com", ClientId = "client-id", RedirectUri = RedirectUri };
            options.AuthorizationEndpoint = endpoint;
            var provider = OAuth2Provider.Create(options, httpClient: null);

            string url = provider.GetAuthorizationUrl("state-1", RedirectUri, "challenge-1");

            Assert.StartsWith(expectedStart, url, StringComparison.Ordinal);
            Assert.Equal(1, url.Count(c => c == '?'));
            Assert.Equal("client-id", LoopbackTestHttp.GetQueryValue(url, "client_id"));
            Assert.Equal("state-1", LoopbackTestHttp.GetQueryValue(url, "state"));
            Assert.Equal("challenge-1", LoopbackTestHttp.GetQueryValue(url, "code_challenge"));
        }

        [Fact]
        [DisplayName("Facebook keeps the query of the user information endpoint and adds the fields after it")]
        public async Task Facebook_GetUserInfoAsync_EndpointWithQuery_KeepsQuery()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"id":"1"}""");
            var options = new FacebookOAuth2Options { ClientId = "client-id", RedirectUri = RedirectUri };
            options.UserInfoEndpoint += "?locale=en_US";
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("?locale=en_US&fields=id%2Cname%2Cfirst_name%2Clast_name%2Cemail", handler.Requests[0].Uri.Query);
        }

        [Fact]
        [DisplayName("The token request goes to the token endpoint with its query")]
        public async Task ExchangeCodeAsync_TokenEndpointWithQuery_KeepsQuery()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var options = new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = RedirectUri };
            options.TokenEndpoint += "?tenant=a";
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            Assert.Equal(options.TokenEndpoint, handler.Requests[0].Uri.AbsoluteUri);
        }

        [Fact]
        [DisplayName("The authorization URL has no PKCE parameters without a challenge")]
        public void GetAuthorizationUrl_WithoutCodeChallenge_OmitsPkceParameters()
        {
            var provider = OAuth2Provider.Create(CreateOptions("Google"), httpClient: null);

            string url = provider.GetAuthorizationUrl("state-value", RedirectUri, codeChallenge: null);

            Assert.Null(LoopbackTestHttp.GetQueryValue(url, "code_challenge"));
            Assert.Null(LoopbackTestHttp.GetQueryValue(url, "code_challenge_method"));
        }

        [Fact]
        [DisplayName("Facebook separates the scopes of the authorization URL with commas")]
        public void Facebook_GetAuthorizationUrl_SeparatesScopesWithCommas()
        {
            var provider = OAuth2Provider.Create(CreateOptions("Facebook"), httpClient: null);

            string url = provider.GetAuthorizationUrl("state-value", RedirectUri, codeChallenge: null);

            Assert.Equal("public_profile,email", LoopbackTestHttp.GetQueryValue(url, "scope"));
        }

        [Fact]
        [DisplayName("A confidential client sends both the PKCE code verifier and the client secret")]
        public async Task ExchangeCodeAsync_ConfidentialClientWithVerifier_SendsVerifierAndSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider("Azure", handler, clientSecret: "secret");

            await provider.ExchangeCodeAsync("code-value", RedirectUri, "verifier-value", publicClient: false, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(provider.Options.TokenEndpoint, request.Uri.AbsoluteUri);
            Assert.Equal("authorization_code", request.FormValue("grant_type"));
            Assert.Equal("code-value", request.FormValue("code"));
            Assert.Equal(RedirectUri, request.FormValue("redirect_uri"));
            Assert.Equal("client-id", request.FormValue("client_id"));
            Assert.Equal("verifier-value", request.FormValue("code_verifier"));
            Assert.Equal("secret", request.FormValue("client_secret"));
            Assert.Null(request.FormValue("response_mode"));
        }

        [Fact]
        [DisplayName("A public client sends the PKCE code verifier without the client secret")]
        public async Task ExchangeCodeAsync_PublicClient_SendsVerifierWithoutSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider("LINE", handler, clientSecret: "secret");

            await provider.ExchangeCodeAsync("code-value", RedirectUri, "verifier-value", publicClient: true, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal("verifier-value", request.FormValue("code_verifier"));
            Assert.Null(request.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("Google receives the client secret from a public client too")]
        public async Task Google_ExchangeCodeAsync_PublicClient_SendsSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider("Google", handler, clientSecret: "secret");

            await provider.ExchangeCodeAsync("code-value", RedirectUri, "verifier-value", publicClient: true, CancellationToken.None);

            Assert.Equal("secret", Assert.Single(handler.Requests).FormValue("client_secret"));
        }

        [Theory]
        [DisplayName("Of the providers, only Google receives a client secret that is set from a public client")]
        [InlineData("Google", true)]
        [InlineData("LINE", false)]
        [InlineData("Azure", false)]
        [InlineData("Facebook", false)]
        [InlineData("Auth0", false)]
        [InlineData("Okta", false)]
        public async Task ExchangeCodeAsync_PublicClientWithSecret_SendsSecretOnlyToGoogle(string providerName, bool sendsSecret)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider(providerName, handler, clientSecret: "secret");

            await provider.ExchangeCodeAsync("code", RedirectUri, "verifier", publicClient: true, CancellationToken.None);

            Assert.Equal(sendsSecret ? "secret" : null, handler.Requests[0].FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("The token request omits the client secret and the code verifier when neither is set")]
        public async Task ExchangeCodeAsync_NoSecretNoVerifier_OmitsBoth()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider("Okta", handler);

            await provider.ExchangeCodeAsync("code-value", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Null(request.FormValue("client_secret"));
            Assert.Null(request.FormValue("code_verifier"));
        }

        [Fact]
        [DisplayName("ExchangeCodeAsync reads every field of a successful token response")]
        public async Task ExchangeCodeAsync_SuccessfulResponse_ReadsAllFields()
        {
            const string json = """{"access_token":"access","token_type":"Bearer","expires_in":3600,"refresh_token":"refresh","id_token":"id","scope":"openid email"}""";
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider("Google", handler);

            var token = await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            Assert.Equal("access", token.AccessToken);
            Assert.Equal("Bearer", token.TokenType);
            Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
            Assert.Equal("refresh", token.RefreshToken);
            Assert.Equal("id", token.IdToken);
            Assert.Equal("openid email", token.Scope);
            Assert.Equal(json, token.RawJson);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync accepts the Bearer token type in any case, and a response without a token type")]
        [InlineData("""{"access_token":"a","token_type":"Bearer"}""")]
        [InlineData("""{"access_token":"a","token_type":"bearer"}""")]
        [InlineData("""{"access_token":"a"}""")]
        public async Task ExchangeCodeAsync_BearerOrNoTokenType_ReturnsToken(string json)
        {
            var provider = CreateProvider("Google", new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json));

            var token = await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            Assert.Equal("a", token.AccessToken);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync and RefreshTokenAsync reject a token type other than Bearer, which RFC 6749 forbids the client to use")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TokenRequest_OtherTokenType_ThrowsOAuth2Exception(bool refresh)
        {
            const string json = """{"access_token":"a","token_type":"DPoP"}""";
            var provider = CreateProvider("Google", new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json));

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => refresh
                ? provider.RefreshTokenAsync("refresh", publicClient: false, CancellationToken.None)
                : provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));
            Assert.Contains("DPoP", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync reads expires_in written as a number or as a string of digits")]
        [InlineData("""{"access_token":"a","expires_in":3599}""")]
        [InlineData("""{"access_token":"a","expires_in":"3599"}""")]
        public async Task ExchangeCodeAsync_ExpiresIn_ReadsSeconds(string json)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider("Google", handler);

            var token = await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(3599), token.ExpiresIn);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync leaves ExpiresIn empty when expires_in is missing or not a whole number of seconds")]
        [InlineData("""{"access_token":"a"}""")]
        [InlineData("""{"access_token":"a","expires_in":-1}""")]
        [InlineData("""{"access_token":"a","expires_in":1.5}""")]
        [InlineData("""{"access_token":"a","expires_in":"soon"}""")]
        [InlineData("""{"access_token":"a","expires_in":null}""")]
        public async Task ExchangeCodeAsync_InvalidExpiresIn_LeavesExpiresInEmpty(string json)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider("Google", handler);

            var token = await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            Assert.Null(token.ExpiresIn);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync rejects a response whose access token is missing, empty or not a string")]
        [InlineData("""{}""")]
        [InlineData("""{"access_token":null}""")]
        [InlineData("""{"access_token":""}""")]
        [InlineData("""{"access_token":123}""")]
        [InlineData("""{"access_token":true}""")]
        [InlineData("""{"access_token":{"value":"a"}}""")]
        public async Task ExchangeCodeAsync_InvalidAccessToken_ThrowsOAuth2Exception(string json)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAsync<OAuth2Exception>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));
        }

        [Fact]
        [DisplayName("ExchangeCodeAsync reports an error response as OAuth2Exception with the error code")]
        public async Task ExchangeCodeAsync_ErrorResponse_ThrowsOAuth2ExceptionWithErrorCode()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"The code has expired."}""");
            var provider = CreateProvider("Google", handler);

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));

            Assert.Equal("invalid_grant", exception.Error);
            Assert.Equal("The code has expired.", exception.ErrorDescription);
            Assert.DoesNotContain("expired", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ExchangeCodeAsync reports an unsuccessful status without an error code as HttpRequestException")]
        [InlineData("")]
        [InlineData("<html><body>Bad request</body></html>")]
        [InlineData("""{"error":{"message":"No code and no type."}}""")]
        [InlineData("""{"error":["invalid_grant"]}""")]
        public async Task ExchangeCodeAsync_UnsuccessfulStatusWithoutErrorCode_ThrowsHttpRequestException(string body)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, body);
            var provider = CreateProvider("Facebook", handler);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));
        }

        [Theory]
        [DisplayName("Facebook reports a Graph API error object as OAuth2Exception with its code, or its type when it has no code")]
        [InlineData("""{"error":{"message":"This authorization code has been used.","type":"OAuthException","code":100,"error_subcode":36009}}""", "100")]
        [InlineData("""{"error":{"message":"This authorization code has been used.","type":"OAuthException"}}""", "OAuthException")]
        public async Task Facebook_ExchangeCodeAsync_GraphApiError_ThrowsOAuth2ExceptionWithCode(string body, string expectedError)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, body);
            var provider = CreateProvider("Facebook", handler);

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));

            Assert.Equal(expectedError, exception.Error);
            Assert.Equal("This authorization code has been used.", exception.ErrorDescription);
            Assert.DoesNotContain("has been used", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Facebook still reads an error response in the form of RFC 6749")]
        public async Task Facebook_ExchangeCodeAsync_Rfc6749Error_ThrowsOAuth2ExceptionWithErrorCode()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"The code has expired."}""");
            var provider = CreateProvider("Facebook", handler);

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));

            Assert.Equal("invalid_grant", exception.Error);
        }

        [Fact]
        [DisplayName("A provider other than Facebook does not read an error object")]
        public async Task ExchangeCodeAsync_ErrorObjectFromOtherProvider_ThrowsHttpRequestException()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid verification code format.","code":100}}""");
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));
        }

        [Fact]
        [DisplayName("ExchangeCodeAsync reports a successful response that is not JSON as JsonException")]
        public async Task ExchangeCodeAsync_SuccessfulResponseNotJson_ThrowsJsonException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, "access_token=access");
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAnyAsync<JsonException>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None));
        }

        [Fact]
        [DisplayName("ExchangeCodeAsync observes the cancellation token")]
        public async Task ExchangeCodeAsync_CanceledToken_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"access"}""");
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, new CancellationToken(canceled: true)));
        }

        [Fact]
        [DisplayName("RefreshTokenAsync sends the refresh token grant and reads the rotated refresh token")]
        public async Task RefreshTokenAsync_ConfidentialClient_SendsGrantAndReadsNewTokens()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"new-access","refresh_token":"rotated"}""");
            var provider = CreateProvider("Auth0", handler, clientSecret: "secret");

            var token = await provider.RefreshTokenAsync("old-refresh", publicClient: false, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(provider.Options.TokenEndpoint, request.Uri.AbsoluteUri);
            Assert.Equal("refresh_token", request.FormValue("grant_type"));
            Assert.Equal("old-refresh", request.FormValue("refresh_token"));
            Assert.Equal("client-id", request.FormValue("client_id"));
            Assert.Equal("secret", request.FormValue("client_secret"));
            Assert.Null(request.FormValue("code"));
            Assert.Equal("new-access", token.AccessToken);
            Assert.Equal("rotated", token.RefreshToken);
        }

        [Fact]
        [DisplayName("RefreshTokenAsync from a public client does not send the client secret")]
        public async Task RefreshTokenAsync_PublicClient_OmitsSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"new-access"}""");
            var provider = CreateProvider("Okta", handler, clientSecret: "secret");

            await provider.RefreshTokenAsync("old-refresh", publicClient: true, CancellationToken.None);

            Assert.Null(Assert.Single(handler.Requests).FormValue("client_secret"));
        }

        [Theory]
        [DisplayName("With ClientSecretBasic the token requests send the form-encoded client ID and secret in a Basic header, and neither in the body")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TokenRequest_ClientSecretBasic_SendsBasicHeader(bool refresh)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"a"}""");
            var options = new GoogleOAuth2Options
            {
                ClientId = "id:with space",
                ClientSecret = "s&cret/+",
                RedirectUri = RedirectUri,
                ClientAuthentication = ClientAuthenticationMethod.ClientSecretBasic
            };
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            if (refresh)
                await provider.RefreshTokenAsync("refresh", publicClient: false, CancellationToken.None);
            else
                await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal("Basic", request.Authorization?.Scheme);
            Assert.Equal("id%3Awith+space:s%26cret%2F%2B", Encoding.UTF8.GetString(Convert.FromBase64String(request.Authorization!.Parameter!)));
            Assert.Null(request.FormValue("client_id"));
            Assert.Null(request.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("With ClientSecretBasic but no secret to send, the token request sends the client ID in the body and no header")]
        public async Task ExchangeCodeAsync_ClientSecretBasicWithoutSecret_SendsClientIdInBody()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"a"}""");
            var options = new AzureOAuth2Options
            {
                ClientId = "client-id",
                ClientSecret = "secret",
                RedirectUri = RedirectUri,
                ClientAuthentication = ClientAuthenticationMethod.ClientSecretBasic
            };
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            await provider.ExchangeCodeAsync("code", RedirectUri, "verifier", publicClient: true, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Null(request.Authorization);
            Assert.Equal("client-id", request.FormValue("client_id"));
            Assert.Null(request.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("By default the token request sends the client secret in the body and no Authorization header")]
        public async Task ExchangeCodeAsync_DefaultClientAuthentication_SendsSecretInBody()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"a"}""");
            var options = new GoogleOAuth2Options { ClientId = "client-id", ClientSecret = "secret", RedirectUri = RedirectUri };
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            await provider.ExchangeCodeAsync("code", RedirectUri, codeVerifier: null, publicClient: false, CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Null(request.Authorization);
            Assert.Equal("client-id", request.FormValue("client_id"));
            Assert.Equal("secret", request.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("RefreshTokenAsync is not supported for Facebook, which issues no refresh tokens, and reports it through the returned task")]
        public async Task Facebook_RefreshTokenAsync_ThrowsNotSupportedException()
        {
            var handler = new StubHttpMessageHandler();
            var provider = CreateProvider("Facebook", handler);

            Task<TokenResponse> refresh = provider.RefreshTokenAsync("refresh", publicClient: false, CancellationToken.None);

            await Assert.ThrowsAsync<NotSupportedException>(() => refresh);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync sends the access token as a bearer token and parses the response")]
        public async Task GetUserInfoAsync_SuccessfulResponse_SendsBearerTokenAndParses()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada","email":"ada@example.com"}""");
            var provider = CreateProvider("Google", handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(provider.Options.UserInfoEndpoint, request.Uri.AbsoluteUri);
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("access", request.Authorization?.Parameter);
            Assert.Equal("1", user.UserId);
            Assert.Equal("Ada", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Facebook asks the Graph API for the id, name, name parts and email fields only")]
        public async Task Facebook_GetUserInfoAsync_RequestsIdNameAndEmail()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"id":"1"}""");
            var provider = CreateProvider("Facebook", handler);

            await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("id,name,first_name,last_name,email", LoopbackTestHttp.GetQueryValue(Assert.Single(handler.Requests).Uri.AbsoluteUri, "fields"));
        }

        [Fact]
        [DisplayName("GetUserInfoAsync reports an unsuccessful status as HttpRequestException")]
        public async Task GetUserInfoAsync_UnsuccessfulStatus_ThrowsHttpRequestException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.Unauthorized, string.Empty);
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None));
        }

        [Theory]
        [DisplayName("GetUserInfoAsync reports an empty response as OAuth2Exception")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetUserInfoAsync_EmptyResponse_ThrowsOAuth2Exception(string body)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, body);
            var provider = CreateProvider("Google", handler);

            await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None));
        }

        private static OAuth2Options CreateOptions(string providerName, string clientSecret = "")
        {
            return TestOptions.Create(providerName, RedirectUri, clientSecret);
        }

        private static OAuth2Provider CreateProvider(string providerName, StubHttpMessageHandler handler, string clientSecret = "")
        {
            return OAuth2Provider.Create(CreateOptions(providerName, clientSecret), handler.CreateClient());
        }
    }
}
