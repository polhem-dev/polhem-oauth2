using System.ComponentModel;
using System.Net;

namespace Polhem.OAuth2.UnitTests
{
    public class AppOAuth2ClientTests
    {
        private const string RedirectUri = "com.example.app:/oauth2redirect";

        [Theory]
        [DisplayName("The constructor accepts https and custom scheme redirect URIs, including schemes without a period")]
        [InlineData(RedirectUri)]
        [InlineData("com.googleusercontent.apps.123-abc:/oauthredirect")]
        [InlineData("msauth.com.example.app://auth")]
        [InlineData("msauth://com.example.app/UXf4Y2Ytt9xnDOHMzl6HexI08%2Bo%3D")]
        [InlineData("fb1234567890://authorize")]
        [InlineData("line3rdp.com.example.app://auth")]
        [InlineData("https://app.example.com/oauth2redirect")]
        public void Constructor_AppRedirectUri_Succeeds(string redirectUri)
        {
            var client = new AppOAuth2Client(CreateOptions("Auth0", redirectUri: redirectUri), (_, _, _) => Task.FromResult(new Uri(redirectUri)));

            Assert.NotNull(client);
        }

        [Theory]
        [DisplayName("The constructor rejects http, script, data and file URIs, relative URIs and URIs with a fragment")]
        [InlineData("")]
        [InlineData("http://app.example.com/oauth2redirect")]
        [InlineData("http://127.0.0.1:53682/callback")]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,x")]
        [InlineData("file:///tmp/callback")]
        [InlineData("/oauth2redirect")]
        [InlineData("oauth2redirect")]
        [InlineData("com.example.app:/oauth2redirect#fragment")]
        public void Constructor_NonAppRedirectUri_ThrowsArgumentException(string redirectUri)
        {
            var options = CreateOptions("Auth0", redirectUri: redirectUri);

            Assert.Throws<ArgumentException>(() => new AppOAuth2Client(options, (_, _, _) => Task.FromResult(new Uri(RedirectUri))));
        }

        [Fact]
        [DisplayName("The constructor rejects null options and a null authenticate function")]
        public void Constructor_NullArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AppOAuth2Client(null!, (_, _, _) => Task.FromResult(new Uri(RedirectUri))));
            Assert.Throws<ArgumentNullException>(() => new AppOAuth2Client(CreateOptions("Auth0"), null!));
        }

        [Fact]
        [DisplayName("SignInAsync returns the tokens and user information, sending the same redirect URI and the PKCE verifier")]
        public async Task SignInAsync_Callback_ExchangesCodeWithPkce()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"user-1","name":"User","email":"user@example.com"}""");
            var browser = new FakeAuthenticator("?code=abc&state={state}");
            var client = new AppOAuth2Client(CreateOptions("Auth0"), browser.AuthenticateAsync, handler.CreateClient());

            var result = await client.SignInAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("user-1", result.UserInfo!.UserId);
            Assert.Equal(RedirectUri, browser.RedirectUri!.OriginalString);
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "redirect_uri"));
            Assert.Equal("S256", LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "code_challenge_method"));
            var tokenRequest = handler.Requests[0];
            Assert.Equal("abc", tokenRequest.FormValue("code"));
            Assert.Equal(RedirectUri, tokenRequest.FormValue("redirect_uri"));
            string verifier = tokenRequest.FormValue("code_verifier")!;
            Assert.Equal(Pkce.GenerateCodeChallenge(verifier), LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "code_challenge"));
        }

        [Fact]
        [DisplayName("SignInAsync uses PKCE even when the options turn it off")]
        public async Task SignInAsync_UsePkceFalse_StillSendsChallenge()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var options = CreateOptions("Auth0");
            options.UsePkce = false;
            var browser = new FakeAuthenticator("?code=abc&state={state}");

            await new AppOAuth2Client(options, browser.AuthenticateAsync, handler.CreateClient()).SignInAsync();

            Assert.NotNull(LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "code_challenge"));
            Assert.NotNull(Assert.Single(handler.Requests).FormValue("code_verifier"));
        }

        [Theory]
        [DisplayName("SignInAsync sends a client secret that is set only to a provider that requires one from a public client")]
        [InlineData("LINE", false)]
        [InlineData("Auth0", false)]
        [InlineData("Google", true)]
        public async Task SignInAsync_ClientSecretSet_FollowsProvider(string providerName, bool sent)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeAuthenticator("?code=abc&state={state}");
            var client = new AppOAuth2Client(CreateOptions(providerName, clientSecret: "secret"), browser.AuthenticateAsync, handler.CreateClient());

            await client.SignInAsync();

            Assert.Equal(sent ? "secret" : null, Assert.Single(handler.Requests).FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("SignInAsync sends no client secret when none is set")]
        public async Task SignInAsync_NoClientSecret_OmitsSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeAuthenticator("?code=abc&state={state}");

            await new AppOAuth2Client(CreateOptions("Google"), browser.AuthenticateAsync, handler.CreateClient()).SignInAsync();

            Assert.Null(Assert.Single(handler.Requests).FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("SignInAsync reads the parameters from the fragment of the callback URI")]
        public async Task SignInAsync_ParametersInFragment_ExchangesCode()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeAuthenticator("#code=from-fragment&state={state}");

            await new AppOAuth2Client(CreateOptions("Auth0"), browser.AuthenticateAsync, handler.CreateClient()).SignInAsync();

            Assert.Equal("from-fragment", Assert.Single(handler.Requests).FormValue("code"));
        }

        [Theory]
        [DisplayName("SignInAsync fails with OAuth2Exception for another state, a provider error or a missing code")]
        [InlineData("?code=abc&state=forged")]
        [InlineData("?code=abc")]
        [InlineData("?error=access_denied&error_description=Denied&state={state}")]
        [InlineData("?state={state}")]
        public async Task SignInAsync_InvalidCallback_FailsWithOAuth2Exception(string callback)
        {
            var handler = new StubHttpMessageHandler();
            var browser = new FakeAuthenticator(callback);

            var result = await new AppOAuth2Client(CreateOptions("Auth0"), browser.AuthenticateAsync, handler.CreateClient()).SignInAsync();

            Assert.False(result.IsSuccess);
            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("SignInAsync turns an OperationCanceledException from the authenticate function, such as the TaskCanceledException of a closed sign-in, into a failed result")]
        public async Task SignInAsync_AuthenticateCanceled_ReturnsFailure()
        {
            var client = new AppOAuth2Client(CreateOptions("Auth0"), (_, _, _) => Task.FromException<Uri>(new TaskCanceledException()));

            var result = await client.SignInAsync();

            Assert.False(result.IsSuccess);
            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation of the caller's token while the sign-in is open into a failed result")]
        public async Task SignInAsync_TokenCanceledDuringAuthenticate_ReturnsFailure()
        {
            using var cancellation = new CancellationTokenSource();
            var client = new AppOAuth2Client(CreateOptions("Auth0"), async (_, _, cancellationToken) =>
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new Uri(RedirectUri);
            });

            var result = await client.SignInAsync(cancellation.Token).WithTimeout();

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync returns a failed result without opening the sign-in when the token is already canceled")]
        public async Task SignInAsync_TokenCanceledBeforeStart_DoesNotAuthenticate()
        {
            bool opened = false;
            var client = new AppOAuth2Client(CreateOptions("Auth0"), (_, _, _) =>
            {
                opened = true;
                return Task.FromResult(new Uri(RedirectUri));
            });

            var result = await client.SignInAsync(new CancellationToken(canceled: true));

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
            Assert.False(opened);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation of the caller's token during the code exchange into a failed result")]
        public async Task SignInAsync_TokenCanceledDuringExchange_ReturnsFailure()
        {
            var handler = new StubHttpMessageHandler().Hang();
            using var cancellation = new CancellationTokenSource();
            var browser = new FakeAuthenticator("?code=abc&state={state}");
            var client = new AppOAuth2Client(CreateOptions("Auth0"), browser.AuthenticateAsync, handler.CreateClient());

            var signIn = client.SignInAsync(cancellation.Token);
            await handler.Hanging.WithTimeout();
            cancellation.Cancel();
            var result = await signIn.WithTimeout();

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Theory]
        [DisplayName("SignInAsync turns a failed request to the provider and an error from the token endpoint into a failed result")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignInAsync_ProviderFailure_ReturnsFailedResult(bool requestFails)
        {
            var handler = requestFails
                ? new StubHttpMessageHandler().Fail(new HttpRequestException("no route"))
                : new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            var browser = new FakeAuthenticator("?code=abc&state={state}");

            var result = await new AppOAuth2Client(CreateOptions("Auth0"), browser.AuthenticateAsync, handler.CreateClient()).SignInAsync();

            Assert.False(result.IsSuccess);
            if (requestFails)
                Assert.IsType<HttpRequestException>(result.Exception);
            else
                Assert.Equal("invalid_grant", Assert.IsType<OAuth2Exception>(result.Exception).Error);
        }

        [Theory]
        [DisplayName("RefreshTokenAsync sends a client secret that is set only to a provider that requires one from a public client")]
        [InlineData("Auth0", false)]
        [InlineData("Google", true)]
        public async Task RefreshTokenAsync_PublicClient_FollowsProvider(string providerName, bool sendsSecret)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"new","refresh_token":"next"}""");
            var client = new AppOAuth2Client(CreateOptions(providerName, clientSecret: "secret"), (_, _, _) => Task.FromResult(new Uri(RedirectUri)), handler.CreateClient());

            var token = await client.RefreshTokenAsync("old");

            Assert.Equal("new", token.AccessToken);
            Assert.Equal("next", token.RefreshToken);
            var request = handler.Requests[0];
            Assert.Equal("refresh_token", request.FormValue("grant_type"));
            Assert.Equal("old", request.FormValue("refresh_token"));
            Assert.Equal(sendsSecret ? "secret" : null, request.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("SignInAsync lets any other exception from the authenticate function propagate")]
        public async Task SignInAsync_AuthenticateThrows_Propagates()
        {
            var client = new AppOAuth2Client(CreateOptions("Auth0"), (_, _, _) => Task.FromException<Uri>(new InvalidOperationException("no browser")));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());

            Assert.Equal("no browser", ex.Message);
        }

        [Fact]
        [DisplayName("SignInAsync throws InvalidOperationException when the authenticate function returns null")]
        public async Task SignInAsync_AuthenticateReturnsNull_ThrowsInvalidOperationException()
        {
            var client = new AppOAuth2Client(CreateOptions("Auth0"), (_, _, _) => Task.FromResult<Uri>(null!));

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());
        }

        [Fact]
        [DisplayName("A second sign-in on the same client throws InvalidOperationException while the first is open, and works after it ends")]
        public async Task SignInAsync_SecondSignInWhileOpen_ThrowsInvalidOperationException()
        {
            var release = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = new AppOAuth2Client(CreateOptions("Auth0"), (_, _, _) => release.Task);

            var first = client.SignInAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());
            release.SetResult(new Uri(RedirectUri + "?state=forged"));
            await first.WithTimeout();

            var again = await client.SignInAsync().WithTimeout();
            Assert.IsType<OAuth2Exception>(again.Exception);
        }

        [Theory]
        [DisplayName("Facebook receives the fb<app id> redirect URI with a trailing slash in the token request, and unchanged in the authorization request")]
        [InlineData("fb1234567890://authorize", "fb1234567890://authorize/")]
        [InlineData("fb1234567890://authorize/", "fb1234567890://authorize/")]
        [InlineData("fb1234567890://authorize/path", "fb1234567890://authorize/path")]
        [InlineData("com.example.app:/oauth2redirect", "com.example.app:/oauth2redirect")]
        [InlineData("fbx://authorize", "fbx://authorize")]
        public async Task Facebook_SignInAsync_AddsTrailingSlashToAppScheme(string redirectUri, string tokenRedirectUri)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeAuthenticator("?code=abc&state={state}");
            var client = new AppOAuth2Client(CreateOptions("Facebook", redirectUri: redirectUri), browser.AuthenticateAsync, handler.CreateClient());

            await client.SignInAsync();

            Assert.Equal(redirectUri, LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "redirect_uri"));
            Assert.Equal(tokenRedirectUri, Assert.Single(handler.Requests).FormValue("redirect_uri"));
        }

        [Theory]
        [DisplayName("Every provider sends the custom scheme redirect URI, the state and the PKCE challenge in the authorization URL")]
        [InlineData("Google")]
        [InlineData("Facebook")]
        [InlineData("LINE")]
        [InlineData("Azure")]
        [InlineData("Auth0")]
        [InlineData("Okta")]
        public async Task SignInAsync_EachProvider_BuildsAuthorizationUrl(string providerName)
        {
            var browser = new FakeAuthenticator("?state=forged");

            await new AppOAuth2Client(CreateOptions(providerName), browser.AuthenticateAsync).SignInAsync();

            string url = browser.AuthorizationUrl!;
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(url, "redirect_uri"));
            Assert.Equal("client-id", LoopbackTestHttp.GetQueryValue(url, "client_id"));
            Assert.Equal("code", LoopbackTestHttp.GetQueryValue(url, "response_type"));
            Assert.NotNull(LoopbackTestHttp.GetQueryValue(url, "state"));
            Assert.Equal("S256", LoopbackTestHttp.GetQueryValue(url, "code_challenge_method"));
        }

        private static OAuth2Options CreateOptions(string providerName, string clientSecret = "", string redirectUri = RedirectUri)
        {
            OAuth2Options options = providerName switch
            {
                "Google" => new GoogleOAuth2Options(),
                "Facebook" => new FacebookOAuth2Options(),
                "LINE" => new LineOAuth2Options(),
                "Azure" => new AzureOAuth2Options(),
                "Auth0" => new Auth0OAuth2Options { Domain = "tenant.auth0.com" },
                "Okta" => new OktaOAuth2Options { Domain = "dev-123456.okta.com" },
                _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Unknown provider.")
            };
            options.ClientId = "client-id";
            options.ClientSecret = clientSecret;
            options.RedirectUri = redirectUri;
            return options;
        }

        /// <summary>
        /// Stands in for WebAuthenticator: records the URLs it receives and returns the redirect URI with the given
        /// parameters, where {state} is replaced by the state of the authorization URL.
        /// </summary>
        private sealed class FakeAuthenticator
        {
            private readonly string _parameters;

            public FakeAuthenticator(string parameters)
            {
                _parameters = parameters;
            }

            public string? AuthorizationUrl { get; private set; }

            public Uri? RedirectUri { get; private set; }

            public Task<Uri> AuthenticateAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken)
            {
                AuthorizationUrl = authorizationUrl.AbsoluteUri;
                RedirectUri = redirectUri;
                string state = LoopbackTestHttp.GetQueryValue(AuthorizationUrl, "state")!;
                return Task.FromResult(new Uri(redirectUri.OriginalString + _parameters.Replace("{state}", Uri.EscapeDataString(state))));
            }
        }
    }
}
