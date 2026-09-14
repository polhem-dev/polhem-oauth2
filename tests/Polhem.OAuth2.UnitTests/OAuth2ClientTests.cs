using System.ComponentModel;
using System.Net;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2ClientTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";

        [Fact]
        [DisplayName("The constructor rejects null options")]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new OAuth2Client(null!));
        }

        [Theory]
        [DisplayName("The constructor rejects options without a client ID, an absolute http or https redirect URI, or valid scopes")]
        [InlineData("", RedirectUri, "openid")]
        [InlineData("client-id", "", "openid")]
        [InlineData("client-id", "/auth/callback", "openid")]
        [InlineData("client-id", "myapp://callback", "openid")]
        [InlineData("client-id", RedirectUri, " ")]
        public void Constructor_InvalidOptions_ThrowsArgumentException(string clientId, string redirectUri, string scope)
        {
            var options = new GoogleOAuth2Options { ClientId = clientId, RedirectUri = redirectUri, Scopes = new[] { scope } };

            Assert.Throws<ArgumentException>(() => new OAuth2Client(options));
        }

        [Fact]
        [DisplayName("The constructor rejects options whose scopes are null")]
        public void Constructor_NullScopes_ThrowsArgumentException()
        {
            var options = CreateOptions();
            options.Scopes = null!;

            Assert.Throws<ArgumentException>(() => new OAuth2Client(options));
        }

        [Fact]
        [DisplayName("The client copies the options, so later changes to them have no effect")]
        public void Constructor_OptionsChangedLater_UsesCopiedOptions()
        {
            var options = CreateOptions();
            var client = new OAuth2Client(options);

            options.ClientId = "changed";
            options.RedirectUri = "https://changed.example.com/callback";
            options.Scopes[0] = "changed";
            var request = client.CreateAuthorizationRequest();

            Assert.Equal("client-id", LoopbackTestHttp.GetQueryValue(request.Url, "client_id"));
            Assert.Equal(RedirectUri, request.Pending.RedirectUri);
            Assert.DoesNotContain("changed", LoopbackTestHttp.GetQueryValue(request.Url, "scope"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An authorization request uses PKCE by default and keeps the values the callback needs")]
        public void CreateAuthorizationRequest_DefaultOptions_UsesPkceAndKeepsPendingValues()
        {
            var client = new OAuth2Client(CreateOptions());

            var request = client.CreateAuthorizationRequest();

            Assert.Equal(request.Pending.State, LoopbackTestHttp.GetQueryValue(request.Url, "state"));
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(request.Url, "redirect_uri"));
            Assert.Equal(RedirectUri, request.Pending.RedirectUri);
            Assert.NotNull(request.Pending.CodeVerifier);
            Assert.Equal(Pkce.GenerateCodeChallenge(request.Pending.CodeVerifier), LoopbackTestHttp.GetQueryValue(request.Url, "code_challenge"));
        }

        [Fact]
        [DisplayName("Each authorization request has its own state and code verifier")]
        public void CreateAuthorizationRequest_TwoRequests_HaveDifferentValues()
        {
            var client = new OAuth2Client(CreateOptions());

            var first = client.CreateAuthorizationRequest();
            var second = client.CreateAuthorizationRequest();

            Assert.NotEqual(first.Pending.State, second.Pending.State);
            Assert.NotEqual(first.Pending.CodeVerifier, second.Pending.CodeVerifier);
        }

        [Fact]
        [DisplayName("An authorization request has no code verifier when PKCE is turned off")]
        public void CreateAuthorizationRequest_UsePkceFalse_HasNoCodeVerifier()
        {
            var options = CreateOptions();
            options.UsePkce = false;
            var client = new OAuth2Client(options);

            var request = client.CreateAuthorizationRequest();

            Assert.Null(request.Pending.CodeVerifier);
            Assert.Null(LoopbackTestHttp.GetQueryValue(request.Url, "code_challenge"));
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync exchanges the code with the kept values and returns the tokens and user information")]
        public async Task CompleteAuthorizationAsync_MatchingCallback_ReturnsSuccessfulResult()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access","refresh_token":"refresh"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada"}""");
            var client = new OAuth2Client(CreateOptions(clientSecret: "secret"), handler.CreateClient());
            var kept = client.CreateAuthorizationRequest().Pending;
            var pending = new PendingAuthorization(kept.State, kept.CodeVerifier, kept.RedirectUri);

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code-value", kept.State, null, null), pending);

            Assert.True(result.IsSuccess);
            Assert.Equal("Google", result.ProviderName);
            Assert.Equal("access", result.Token.AccessToken);
            Assert.Equal("refresh", result.Token.RefreshToken);
            Assert.Equal("1", result.UserInfo.UserId);
            var tokenRequest = handler.Requests[0];
            Assert.Equal("code-value", tokenRequest.FormValue("code"));
            Assert.Equal(kept.CodeVerifier, tokenRequest.FormValue("code_verifier"));
            Assert.Equal(RedirectUri, tokenRequest.FormValue("redirect_uri"));
            Assert.Equal("secret", tokenRequest.FormValue("client_secret"));
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync rejects a callback whose state does not match before reading its other values")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("other-state")]
        public async Task CompleteAuthorizationAsync_StateMismatch_ReturnsFailedResultWithoutRequests(string? state)
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code", state, "access_denied", null), pending);

            Assert.False(result.IsSuccess);
            Assert.Null(Assert.IsType<OAuth2Exception>(result.Exception).Error);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns an error redirect into a failed result with the error code")]
        public async Task CompleteAuthorizationAsync_ErrorCallback_ReturnsFailedResultWithErrorCode()
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(
                new AuthorizationCallback(null, pending.State, "access_denied", "The user denied access."), pending);

            var exception = Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Equal("access_denied", exception.Error);
            Assert.Equal("The user denied access.", exception.ErrorDescription);
            Assert.DoesNotContain("denied access", exception.Message, StringComparison.Ordinal);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a callback without an authorization code into a failed result")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CompleteAuthorizationAsync_MissingCode_ReturnsFailedResult(string? code)
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback(code, pending.State, null, null), pending);

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync fails without contacting the provider when PKCE is used and the code verifier is missing")]
        public async Task CompleteAuthorizationAsync_PkceWithoutVerifier_ReturnsFailedResultWithoutRequests()
        {
            var handler = new StubHttpMessageHandler();
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var kept = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(
                new AuthorizationCallback("code", kept.State, null, null), new PendingAuthorization(kept.State, null, kept.RedirectUri));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns a request timeout into a failed result")]
        public async Task CompleteAuthorizationAsync_RequestTimeout_ReturnsFailedResult()
        {
            var handler = new StubHttpMessageHandler().Fail(new TaskCanceledException("Simulated timeout."));
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code", pending.State, null, null), pending);

            Assert.IsType<TaskCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync lets cancellation by the caller propagate")]
        public async Task CompleteAuthorizationAsync_CanceledByCaller_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Hang();
            var client = new OAuth2Client(CreateOptions(), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;
            using var cancellation = new CancellationTokenSource();

            var completion = client.CompleteAuthorizationAsync(new AuthorizationCallback("code", pending.State, null, null), pending, cancellation.Token);
            await handler.Hanging;
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync rejects a null callback or pending authorization")]
        public async Task CompleteAuthorizationAsync_NullArguments_ThrowsArgumentNullException()
        {
            var client = new OAuth2Client(CreateOptions());
            var pending = client.CreateAuthorizationRequest().Pending;

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.CompleteAuthorizationAsync(null!, pending));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.CompleteAuthorizationAsync(new AuthorizationCallback("code", pending.State, null, null), null!));
        }

        [Fact]
        [DisplayName("RefreshTokenAsync sends the refresh token together with the client secret")]
        public async Task RefreshTokenAsync_ConfidentialClient_SendsRefreshTokenAndSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"new-access"}""");
            var client = new OAuth2Client(CreateOptions(clientSecret: "secret"), handler.CreateClient());

            var token = await client.RefreshTokenAsync("refresh");

            var request = Assert.Single(handler.Requests);
            Assert.Equal("refresh", request.FormValue("refresh_token"));
            Assert.Equal("secret", request.FormValue("client_secret"));
            Assert.Equal("new-access", token.AccessToken);
        }

        [Fact]
        [DisplayName("RefreshTokenAsync rejects a null or empty refresh token")]
        public async Task RefreshTokenAsync_MissingRefreshToken_ThrowsArgumentException()
        {
            var client = new OAuth2Client(CreateOptions());

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.RefreshTokenAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => client.RefreshTokenAsync(string.Empty));
        }

        private static GoogleOAuth2Options CreateOptions(string clientSecret = "")
        {
            return new GoogleOAuth2Options { ClientId = "client-id", ClientSecret = clientSecret, RedirectUri = RedirectUri };
        }
    }
}
