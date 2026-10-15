using System.ComponentModel;
using System.Net;

namespace Polhem.OAuth2.UnitTests
{
    public class GitHubOAuth2ProviderTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";
        private const string PrivateUser = """{"id":583231,"login":"octocat","name":"The Octocat","email":null}""";

        [Fact]
        [DisplayName("GitHub options point to the GitHub endpoints and ask for the read:user and user:email scopes, separated by a space")]
        public void Options_Defaults_UseGitHubEndpointsAndScopes()
        {
            var provider = OAuth2Provider.Create(TestOptions.Create("GitHub", RedirectUri), httpClient: null);

            string url = provider.GetAuthorizationUrl("state", RedirectUri, "challenge");

            Assert.StartsWith("https://github.com/login/oauth/authorize?", url, StringComparison.Ordinal);
            Assert.Equal("read:user user:email", LoopbackTestHttp.GetQueryValue(url, "scope"));
            Assert.Equal("https://github.com/login/oauth/access_token", provider.Options.TokenEndpoint);
            Assert.Equal("https://api.github.com/user", provider.Options.UserInfoEndpoint);
        }

        [Fact]
        [DisplayName("GitHub maps the numeric id, the name and a public email address, with no request for the address list")]
        public async Task GetUserInfoAsync_PublicEmail_MapsUserWithOneRequest()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"id":583231,"login":"octocat","name":"The Octocat","email":"octocat@github.com"}""");
            var provider = CreateProvider(handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            var request = Assert.Single(handler.Requests);
            Assert.Equal("https://api.github.com/user", request.Uri.AbsoluteUri);
            Assert.Equal("583231", user.UserId);
            Assert.Equal("The Octocat", user.UserName);
            Assert.Equal("octocat@github.com", user.Email);
        }

        [Theory]
        [DisplayName("GitHub falls back to the login when the user has no name")]
        [InlineData("""{"id":1,"login":"octocat","email":"a@example.com"}""")]
        [InlineData("""{"id":1,"login":"octocat","name":null,"email":"a@example.com"}""")]
        public void ParseUserJson_NoName_FallsBackToLogin(string json)
        {
            var user = new GitHubOAuth2Provider(new GitHubOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("1", user.UserId);
            Assert.Equal("octocat", user.UserName);
        }

        [Fact]
        [DisplayName("GitHub reads the primary verified address from /user/emails when /user has no email address")]
        public async Task GetUserInfoAsync_PrivateEmail_ReadsPrimaryVerifiedAddress()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, PrivateUser)
                .Respond(HttpStatusCode.OK, """
                    [{"email":"unverified@example.com","primary":false,"verified":false,"visibility":null},
                     {"email":"secondary@example.com","primary":false,"verified":true,"visibility":null},
                     {"email":"octocat@example.com","primary":true,"verified":true,"visibility":"private"}]
                    """);
            var provider = CreateProvider(handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("octocat@example.com", user.Email);
            Assert.Equal("583231", user.UserId);
            Assert.Equal(PrivateUser, user.RawJson);
            Assert.Equal(2, handler.Requests.Count);
            var emails = handler.Requests[1];
            Assert.Equal("https://api.github.com/user/emails?per_page=100", emails.Uri.AbsoluteUri);
            Assert.Equal("Bearer", emails.Authorization?.Scheme);
            Assert.Equal("access", emails.Authorization?.Parameter);
        }

        [Fact]
        [DisplayName("GitHub keeps the query of the user information endpoint when it asks for the address list")]
        public async Task GetUserInfoAsync_EndpointWithQuery_KeepsQueryForEmails()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, PrivateUser)
                .Respond(HttpStatusCode.OK, "[]");
            var options = TestOptions.Create("GitHub", RedirectUri);
            options.UserInfoEndpoint = "https://ghe.example.com/api/v3/user/?tenant=a";
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("https://ghe.example.com/api/v3/user/emails?tenant=a&per_page=100", handler.Requests[1].Uri.AbsoluteUri);
        }

        [Theory]
        [DisplayName("GitHub leaves the email address empty when no address in the list is both primary and verified")]
        [InlineData("""[{"email":"octocat@example.com","primary":true,"verified":false}]""")]
        [InlineData("""[{"email":"octocat@example.com","primary":false,"verified":true}]""")]
        [InlineData("""[{"email":"octocat@example.com","primary":"true","verified":"true"}]""")]
        [InlineData("""[{"email":"","primary":true,"verified":true}]""")]
        [InlineData("""[{"email":123,"primary":true,"verified":true}]""")]
        [InlineData("""[]""")]
        [InlineData("""[null,"octocat@example.com"]""")]
        [InlineData("""{"email":"octocat@example.com","primary":true,"verified":true}""")]
        public async Task GetUserInfoAsync_NoPrimaryVerifiedAddress_LeavesEmailEmpty(string emails)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, PrivateUser).Respond(HttpStatusCode.OK, emails);
            var provider = CreateProvider(handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Null(user.Email);
            Assert.Equal("583231", user.UserId);
        }

        [Theory]
        [DisplayName("GitHub signs the user in without an email address when the address list fails with an unsuccessful status or a body that is not JSON")]
        [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
        [InlineData(HttpStatusCode.Forbidden, """{"message":"Resource not accessible by integration"}""")]
        [InlineData(HttpStatusCode.Unauthorized, "")]
        [InlineData(HttpStatusCode.OK, "<html>Unicorn!</html>")]
        [InlineData(HttpStatusCode.OK, "")]
        public async Task GetUserInfoAsync_EmailsFail_LeavesEmailEmpty(HttpStatusCode status, string body)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, PrivateUser).Respond(status, body);
            var provider = CreateProvider(handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Null(user.Email);
            Assert.Equal("The Octocat", user.UserName);
        }

        [Fact]
        [DisplayName("GitHub signs the user in without an email address when the address list request fails or times out")]
        public async Task GetUserInfoAsync_EmailsRequestFailsOrTimesOut_LeavesEmailEmpty()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, PrivateUser).Fail(new HttpRequestException("Connection reset."))
                .Respond(HttpStatusCode.OK, PrivateUser).Fail(new TaskCanceledException("The request timed out."));
            var provider = CreateProvider(handler);

            Assert.Null((await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None)).Email);
            Assert.Null((await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None)).Email);
        }

        [Fact]
        [DisplayName("GitHub lets the caller's cancellation of the address list request propagate")]
        public async Task GetUserInfoAsync_CanceledDuringEmails_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, PrivateUser).Hang();
            var provider = CreateProvider(handler);
            using var cancellation = new CancellationTokenSource();

            var task = provider.GetUserInfoAsync(new TokenResponse("access"), cancellation.Token);
            await handler.Hanging.WithTimeout();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }

        [Fact]
        [DisplayName("GitHub reports a failed /user request as HttpRequestException and does not ask for the address list")]
        public async Task GetUserInfoAsync_UserFails_ThrowsHttpRequestException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");
            var provider = CreateProvider(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None));
            Assert.Single(handler.Requests);
        }

        [Fact]
        [DisplayName("GitHub API requests ask for the GitHub media type, name the API version and send the User-Agent")]
        public async Task GetUserInfoAsync_ApiRequests_SendGitHubHeaders()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, PrivateUser).Respond(HttpStatusCode.OK, "[]");
            var provider = CreateProvider(handler);

            await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal(2, handler.Requests.Count);
            Assert.All(handler.Requests, request =>
            {
                Assert.Equal("application/vnd.github+json", request.Header("Accept"));
                Assert.Equal(GitHubOAuth2Provider.ApiVersion, request.Header("X-GitHub-Api-Version"));
                Assert.StartsWith("Polhem.OAuth2/", request.Header("User-Agent"), StringComparison.Ordinal);
            });
        }

        [Fact]
        [DisplayName("GitHub refreshes the tokens of a GitHub App, and an OAuth App's response without a refresh token leaves it empty")]
        public async Task RefreshTokenAsync_GitHubApp_SendsGrant()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"new","token_type":"bearer","expires_in":28800,"refresh_token":"next","refresh_token_expires_in":15897600,"scope":""}""")
                .Respond(HttpStatusCode.OK, """{"access_token":"gho_1","token_type":"bearer","scope":"read:user,user:email"}""");
            var provider = CreateProvider(handler, clientSecret: "secret");

            var refreshed = await provider.RefreshTokenAsync("old", publicClient: false, CancellationToken.None);
            var exchanged = await provider.ExchangeCodeAsync("code", RedirectUri, "verifier", publicClient: false, CancellationToken.None);

            Assert.Equal("refresh_token", handler.Requests[0].FormValue("grant_type"));
            Assert.Equal("secret", handler.Requests[0].FormValue("client_secret"));
            Assert.Equal("next", refreshed.RefreshToken);
            Assert.Equal("gho_1", exchanged.AccessToken);
            Assert.Null(exchanged.RefreshToken);
        }

        private static OAuth2Provider CreateProvider(StubHttpMessageHandler handler, string clientSecret = "")
        {
            return OAuth2Provider.Create(TestOptions.Create("GitHub", RedirectUri, clientSecret), handler.CreateClient());
        }
    }
}
