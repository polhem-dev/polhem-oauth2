using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2ClientAccessTokenTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";
        private const string AccessToken = "front-end-access-token";
        private const string GoogleUser = """{"sub":"google-user","name":"Ada","email":"ada@example.com"}""";

        [Fact]
        [DisplayName("GetUserInfoAsync checks a Google token in a form body and then returns the user information")]
        public async Task GetUserInfoAsync_GoogleTokenForClient_ChecksTokenAndReturnsUser()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"aud":"client-id","azp":"client-id","sub":"google-user"}""")
                .Respond(HttpStatusCode.OK, GoogleUser);
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("Google", result.ProviderName);
            Assert.Equal(AccessToken, result.Token.AccessToken);
            Assert.Equal("google-user", result.UserInfo.UserId);
            Assert.Equal(2, handler.Requests.Count);
            var check = handler.Requests[0];
            Assert.Equal(HttpMethod.Post, check.Method);
            Assert.Equal("https://oauth2.googleapis.com/tokeninfo", check.Uri.AbsoluteUri);
            Assert.Equal(AccessToken, check.FormValue("access_token"));
            Assert.Equal(AccessToken, handler.Requests[1].Authorization?.Parameter);
            Assert.All(handler.Requests, request => Assert.StartsWith("Polhem.OAuth2/", request.Header("User-Agent"), StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("GetUserInfoAsync refuses a token issued to another client and never asks for its user information")]
        public async Task GetUserInfoAsync_TokenOfAnotherClient_FailsWithoutUserInfoRequest()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"aud":"attacker-client","azp":"attacker-client"}""");
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.False(result.IsSuccess);
            var exception = Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.DoesNotContain(AccessToken, exception.Message, StringComparison.Ordinal);
            Assert.Single(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync accepts a token issued to one of AcceptedClientIds")]
        public async Task GetUserInfoAsync_TokenOfAcceptedClient_Succeeds()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"aud":"ios-client","azp":"ios-client"}""")
                .Respond(HttpStatusCode.OK, GoogleUser);
            var options = CreateOptions("Google");
            options.AcceptedClientIds = new[] { "ios-client" };
            var client = new OAuth2Client(options, handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.True(result.IsSuccess);
        }

        [Theory]
        [DisplayName("GetUserInfoAsync refuses a Google token whose audience or authorized party is not accepted, or that names no audience")]
        [InlineData("""{"aud":"client-id","azp":"attacker-client"}""")]
        [InlineData("""{"aud":"attacker-client","azp":"client-id"}""")]
        [InlineData("""{"aud":"CLIENT-ID"}""")]
        [InlineData("""{"azp":"client-id"}""")]
        [InlineData("""{"aud":["client-id"]}""")]
        [InlineData("""{"aud":""}""")]
        [InlineData("""{"aud":"client-id","azp":null}""")]
        [InlineData("""{"aud":"client-id","azp":["client-id"]}""")]
        public async Task GetUserInfoAsync_GoogleClientNotAccepted_Fails(string tokenInfo)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, tokenInfo);
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Single(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync turns a token that the provider refuses, such as an expired one, into a failed result with the error code")]
        public async Task GetUserInfoAsync_ProviderRefusesToken_FailsWithErrorCode()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.BadRequest, """{"error":"invalid_token","error_description":"Invalid Value"}""");
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            var exception = Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Equal("invalid_token", exception.Error);
            Assert.DoesNotContain(AccessToken, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync turns an unsuccessful status without an error code into a failed result that leaves out the token")]
        public async Task GetUserInfoAsync_VerificationStatusWithoutErrorCode_FailsWithHttpRequestException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.InternalServerError, "<html>error</html>");
            var client = new OAuth2Client(CreateOptions("LINE"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            var exception = Assert.IsType<HttpRequestException>(result.Exception);
            Assert.Contains("500", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync turns a verification response that is not JSON into a failed result")]
        public async Task GetUserInfoAsync_VerificationNotJson_FailsWithJsonException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, "client_id=client-id");
            var client = new OAuth2Client(CreateOptions("LINE"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<JsonException>(result.Exception, exactMatch: false);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync turns an access token longer than any provider issues into a failed result, before any request")]
        public async Task GetUserInfoAsync_OverlongAccessToken_FailsWithoutRequest()
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions("LINE"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(new string('a', 40000));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync turns a network failure into a failed result")]
        public async Task GetUserInfoAsync_NetworkFailure_Fails()
        {
            var handler = new StubHttpMessageHandler().Fail(new HttpRequestException("Simulated network failure."));
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync lets cancellation by the caller propagate")]
        public async Task GetUserInfoAsync_CanceledByCaller_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Hang();
            var client = new OAuth2Client(CreateOptions("Google"), handler.CreateClient());
            using var cancellation = new CancellationTokenSource();

            var task = client.GetUserInfoAsync(AccessToken, cancellation.Token);
            await handler.Hanging;
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync checks a LINE token with the verify endpoint and compares its channel ID")]
        public async Task GetUserInfoAsync_LineTokenForChannel_ChecksTokenAndReturnsUser()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"scope":"profile","client_id":"client-id","expires_in":2591659}""")
                .Respond(HttpStatusCode.OK, """{"userId":"line-user","displayName":"Ada"}""");
            var client = new OAuth2Client(CreateOptions("LINE"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("line-user", result.UserInfo.UserId);
            var check = handler.Requests[0];
            Assert.Equal(HttpMethod.Get, check.Method);
            Assert.Equal("https://api.line.me/oauth2/v2.1/verify", check.Uri.GetLeftPart(UriPartial.Path));
            Assert.Equal(AccessToken, LoopbackTestHttp.GetQueryValue(check.Uri.AbsoluteUri, "access_token"));
        }

        [Fact]
        [DisplayName("GetUserInfoAsync refuses a LINE token of another channel")]
        public async Task GetUserInfoAsync_LineTokenOfAnotherChannel_Fails()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"client_id":"1234567890","expires_in":100}""");
            var client = new OAuth2Client(CreateOptions("LINE"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Single(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync checks a Facebook token with debug_token, sending the app access token in the header")]
        public async Task GetUserInfoAsync_FacebookUserToken_ChecksTokenAndReturnsUser()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"data":{"app_id":"client-id","type":"USER","is_valid":true,"user_id":"fb-user"}}""")
                .Respond(HttpStatusCode.OK, """{"id":"fb-user","name":"Ada"}""");
            var client = new OAuth2Client(CreateOptions("Facebook", clientSecret: "app-secret"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("fb-user", result.UserInfo.UserId);
            var check = handler.Requests[0];
            Assert.Equal("https://graph.facebook.com/v26.0/debug_token", check.Uri.GetLeftPart(UriPartial.Path));
            Assert.Equal(AccessToken, LoopbackTestHttp.GetQueryValue(check.Uri.AbsoluteUri, "input_token"));
            Assert.DoesNotContain("app-secret", check.Uri.AbsoluteUri, StringComparison.Ordinal);
            Assert.Equal("Bearer", check.Authorization?.Scheme);
            Assert.Equal("client-id|app-secret", check.Authorization?.Parameter);
        }

        [Theory]
        [DisplayName("GetUserInfoAsync refuses a Facebook token that is not valid, is not a user token, or belongs to another app")]
        [InlineData("""{"data":{"error":{"code":190,"message":"Invalid OAuth access token"},"is_valid":false,"scopes":[]}}""", "190")]
        [InlineData("""{"data":{"app_id":"client-id","type":"USER","is_valid":false}}""", null)]
        [InlineData("""{"data":{"app_id":"client-id","type":"USER","is_valid":"true"}}""", null)]
        [InlineData("""{"data":{"app_id":"client-id","type":"APP","is_valid":true}}""", null)]
        [InlineData("""{"data":{"app_id":"client-id","is_valid":true}}""", null)]
        [InlineData("""{"data":{"app_id":"other-app","type":"USER","is_valid":true}}""", null)]
        [InlineData("""{"data":{"type":"USER","is_valid":true}}""", null)]
        [InlineData("""{"app_id":"client-id","type":"USER","is_valid":true}""", null)]
        public async Task GetUserInfoAsync_FacebookTokenNotAccepted_Fails(string debugToken, string? error)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, debugToken);
            var client = new OAuth2Client(CreateOptions("Facebook", clientSecret: "app-secret"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            var exception = Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Equal(error, exception.Error);
            Assert.Single(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync checks a GitHub token for the application's own client ID, with the client credentials and the token in a JSON body")]
        public async Task GetUserInfoAsync_GitHubTokenOfApplication_ChecksTokenAndReturnsUser()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"token":"front-end-access-token","app":{"client_id":"client-id","name":"App"},"user":{"id":1}}""")
                .Respond(HttpStatusCode.OK, """{"id":1,"login":"ada","email":"ada@example.com"}""");
            var client = new OAuth2Client(CreateOptions("GitHub", clientSecret: "client-secret"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.UserInfo.UserId);
            var check = handler.Requests[0];
            Assert.Equal(HttpMethod.Post, check.Method);
            Assert.Equal("https://api.github.com/applications/client-id/token", check.Uri.AbsoluteUri);
            Assert.Equal("Basic", check.Authorization?.Scheme);
            Assert.Equal("client-id:client-secret", Encoding.UTF8.GetString(Convert.FromBase64String(check.Authorization!.Parameter!)));
            using var body = JsonDocument.Parse(check.Body!);
            Assert.Equal(AccessToken, body.RootElement.GetProperty("access_token").GetString());
        }

        [Theory]
        [DisplayName("GetUserInfoAsync refuses a GitHub token that GitHub does not find for the application, or that names another or no client")]
        [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found","status":"404"}""")]
        [InlineData(HttpStatusCode.OK, """{"app":{"client_id":"other-client"}}""")]
        [InlineData(HttpStatusCode.OK, """{"app":null}""")]
        [InlineData(HttpStatusCode.OK, """{"client_id":"client-id"}""")]
        public async Task GetUserInfoAsync_GitHubTokenNotAccepted_Fails(HttpStatusCode statusCode, string checkResponse)
        {
            var handler = new StubHttpMessageHandler().Respond(statusCode, checkResponse);
            var options = CreateOptions("GitHub", clientSecret: "client-secret");
            options.AcceptedClientIds = new[] { "other-client" };
            var client = new OAuth2Client(options, handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            var exception = Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.DoesNotContain(AccessToken, exception.Message, StringComparison.Ordinal);
            Assert.Single(handler.Requests);
        }

        [Theory]
        [DisplayName("GetUserInfoAsync turns another unsuccessful status of the GitHub check into a failed result")]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetUserInfoAsync_GitHubCheckFails_FailsWithHttpRequestException(HttpStatusCode statusCode)
        {
            var handler = new StubHttpMessageHandler().Respond(statusCode, """{"message":"Validation Failed"}""");
            var client = new OAuth2Client(CreateOptions("GitHub", clientSecret: "client-secret"), handler.CreateClient());

            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync checks a GitHub token at the API that serves the user information, such as a GitHub Enterprise Server")]
        public async Task GetUserInfoAsync_GitHubEnterpriseServer_ChecksTokenAtSameApi()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            var options = CreateOptions("GitHub", clientSecret: "client-secret");
            options.UserInfoEndpoint = "https://github.example.com/api/v3/user/";
            var client = new OAuth2Client(options, handler.CreateClient());

            await client.GetUserInfoAsync(AccessToken);

            Assert.Equal("https://github.example.com/api/v3/applications/client-id/token", handler.Requests[0].Uri.AbsoluteUri);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync throws InvalidOperationException when GitHub refuses the client credentials")]
        public async Task GetUserInfoAsync_GitHubRefusesCredentials_ThrowsInvalidOperationException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");
            var client = new OAuth2Client(CreateOptions("GitHub", clientSecret: "wrong-secret"), handler.CreateClient());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUserInfoAsync(AccessToken));

            Assert.DoesNotContain("wrong-secret", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync throws InvalidOperationException for GitHub options without a client secret, before any request")]
        public async Task GetUserInfoAsync_GitHubWithoutSecret_ThrowsInvalidOperationException()
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions("GitHub"), handler.CreateClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUserInfoAsync(AccessToken));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync throws InvalidOperationException for Facebook options without an app secret, before any request")]
        public async Task GetUserInfoAsync_FacebookWithoutSecret_ThrowsInvalidOperationException()
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions("Facebook"), handler.CreateClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUserInfoAsync(AccessToken));
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("GetUserInfoAsync throws NotSupportedException for a provider that cannot check a token, before any request")]
        [InlineData("Azure")]
        [InlineData("Auth0")]
        [InlineData("Okta")]
        public async Task GetUserInfoAsync_UnsupportedProvider_ThrowsNotSupportedException(string providerName)
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions(providerName, clientSecret: "secret"), handler.CreateClient());

            await Assert.ThrowsAsync<NotSupportedException>(() => client.GetUserInfoAsync(AccessToken));
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("GetUserInfoAsync tells a caller of an OpenID Connect provider to validate an ID token or a token for its own API instead")]
        [InlineData("Azure")]
        [InlineData("Auth0")]
        [InlineData("Okta")]
        public async Task GetUserInfoAsync_OidcProvider_NotSupportedExceptionNamesAlternative(string providerName)
        {
            var client = new OAuth2Client(CreateOptions(providerName, clientSecret: "secret"), new StubHttpMessageHandler().CreateClient());

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => client.GetUserInfoAsync(AccessToken));

            Assert.Contains("JwtBearer", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("GetUserInfoAsync rejects a null, empty or white-space access token")]
        public async Task GetUserInfoAsync_MissingAccessToken_ThrowsArgumentException()
        {
            var client = new OAuth2Client(CreateOptions("Google"));

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserInfoAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => client.GetUserInfoAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => client.GetUserInfoAsync("  "));
        }

        [Fact]
        [DisplayName("The client copies AcceptedClientIds, so a client ID added later is not accepted")]
        public async Task Constructor_AcceptedClientIdsChangedLater_UsesCopiedList()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"aud":"added-later"}""");
            var options = CreateOptions("Google");
            options.AcceptedClientIds = new[] { "ios-client" };
            var client = new OAuth2Client(options, handler.CreateClient());

            options.AcceptedClientIds[0] = "added-later";
            var result = await client.GetUserInfoAsync(AccessToken);

            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        [Fact]
        [DisplayName("AcceptedClientIds is empty by default")]
        public void AcceptedClientIds_Default_IsEmpty()
        {
            Assert.Empty(new GoogleOAuth2Options().AcceptedClientIds);
        }

        [Theory]
        [DisplayName("The constructor rejects AcceptedClientIds that is null or holds an empty client ID")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_InvalidAcceptedClientIds_ThrowsArgumentException(string? clientId)
        {
            var options = CreateOptions("Google");
            options.AcceptedClientIds = clientId is null ? null! : new[] { "ios-client", clientId };

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));
            Assert.Contains(nameof(OAuth2Options.AcceptedClientIds), exception.Message, StringComparison.Ordinal);
        }

        private static OAuth2Options CreateOptions(string providerName, string clientSecret = "")
        {
            return TestOptions.Create(providerName, RedirectUri, clientSecret);
        }
    }
}
