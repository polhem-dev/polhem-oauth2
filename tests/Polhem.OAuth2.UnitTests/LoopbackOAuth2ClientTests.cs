using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class LoopbackOAuth2ClientTests
    {
        [Theory]
        [DisplayName("The constructor rejects a redirect URI that a loopback listener cannot receive")]
        [InlineData("")]
        [InlineData("https://127.0.0.1:53682/callback")]
        [InlineData("https://login.microsoftonline.com/common/oauth2/nativeclient")]
        [InlineData("http://example.com/callback")]
        public void Constructor_NonLoopbackRedirectUri_ThrowsArgumentException(string redirectUri)
        {
            var options = new GoogleOAuth2Options { RedirectUri = redirectUri };

            Assert.Throws<ArgumentException>(() => new LoopbackOAuth2Client(options));
        }

        [Fact]
        [DisplayName("SignInAsync exchanges the code with the redirect URI of the bound port, then restores the configured URI")]
        public async Task SignInAsync_CallbackWithState_ExchangesCodeWithBoundRedirectUri()
        {
            using var tokenEndpoint = new FakeTokenEndpoint();
            var options = CreateOptions(tokenEndpoint.Url);
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(options) { OpenBrowser = browser.Open };

            var result = await client.SignInAsync();
            await browser.Completed;

            // The fake token endpoint rejects the request, so the exchange ends as an HTTP failure.
            Assert.IsType<HttpRequestException>(result.Exception);
            string tokenRequest = await tokenEndpoint.RequestBody;
            string boundRedirectUri = LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "redirect_uri")!;
            Assert.NotEqual(0, new Uri(boundRedirectUri).Port);
            Assert.Equal(boundRedirectUri, LoopbackTestHttp.GetParameter(tokenRequest, "redirect_uri"));
            Assert.Equal("abc", LoopbackTestHttp.GetParameter(tokenRequest, "code"));
            Assert.StartsWith("HTTP/1.1 200 OK", browser.Responses[0], StringComparison.Ordinal);
            Assert.Equal("http://127.0.0.1:0/callback", options.RedirectUri);
        }

        [Fact]
        [DisplayName("SignInAsync ignores requests without the state of the sign-in and uses the genuine redirect")]
        public async Task SignInAsync_OtherRequestsBeforeRedirect_UsesGenuineRedirect()
        {
            using var tokenEndpoint = new FakeTokenEndpoint();
            var browser = new FakeBrowser("/favicon.ico", "{path}?code=forged&state=forged", "{path}?code=genuine&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(tokenEndpoint.Url)) { OpenBrowser = browser.Open };

            await client.SignInAsync();
            await browser.Completed;

            Assert.Equal("genuine", LoopbackTestHttp.GetParameter(await tokenEndpoint.RequestBody, "code"));
            Assert.StartsWith("HTTP/1.1 404", browser.Responses[0], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 400", browser.Responses[1], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 200", browser.Responses[2], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync turns an error redirect into a failed result with OAuth2Exception")]
        public async Task SignInAsync_ProviderError_ReturnsFailedResultWithOAuth2Exception()
        {
            var browser = new FakeBrowser("{path}?error=access_denied&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions("http://127.0.0.1:1/token")) { OpenBrowser = browser.Open };

            var result = await client.SignInAsync();
            await browser.Completed;

            Assert.False(result.IsSuccess);
            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Contains("did not complete", browser.Responses[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync turns a missing redirect into a failed result with TimeoutException")]
        public async Task SignInAsync_OnlyForgedRequest_ReturnsFailedResultWithTimeoutException()
        {
            var browser = new FakeBrowser("{path}?code=forged&state=forged");
            var client = new LoopbackOAuth2Client(CreateOptions("http://127.0.0.1:1/token"))
            {
                OpenBrowser = browser.Open,
                Timeout = TimeSpan.FromSeconds(1)
            };

            var result = await client.SignInAsync();
            await browser.Completed;

            Assert.IsType<TimeoutException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation into a failed result with OperationCanceledException")]
        public async Task SignInAsync_CanceledWhileWaiting_ReturnsFailedResultWithOperationCanceledException()
        {
            using var cancellation = new CancellationTokenSource();
            var client = new LoopbackOAuth2Client(CreateOptions("http://127.0.0.1:1/token")) { OpenBrowser = _ => cancellation.Cancel() };

            var result = await client.SignInAsync(cancellation.Token);

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync does not open the browser when the token is already canceled")]
        public async Task SignInAsync_AlreadyCanceled_DoesNotOpenBrowser()
        {
            bool opened = false;
            var client = new LoopbackOAuth2Client(CreateOptions("http://127.0.0.1:1/token")) { OpenBrowser = _ => opened = true };

            var result = await client.SignInAsync(new CancellationToken(canceled: true));

            Assert.False(opened);
            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync refuses to start a second sign-in on the same client")]
        public async Task SignInAsync_WhileInProgress_ThrowsInvalidOperationException()
        {
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            var client = new LoopbackOAuth2Client(CreateOptions("http://127.0.0.1:1/token")) { OpenBrowser = _ => opened.TrySetResult() };

            var first = client.SignInAsync(cancellation.Token);
            await opened.Task;

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());
            await cancellation.CancelAsync();
            Assert.IsAssignableFrom<OperationCanceledException>((await first).Exception);
        }

        [Fact]
        [DisplayName("SignInAsync uses PKCE even when the options turn it off, and sends no client secret")]
        public async Task SignInAsync_UsePkceFalse_SendsCodeVerifierWithoutClientSecret()
        {
            using var tokenEndpoint = new FakeTokenEndpoint();
            var options = new FacebookOAuth2Options
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "http://127.0.0.1:0/callback",
                TokenEndpoint = tokenEndpoint.Url,
                UsePkce = false
            };
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(options) { OpenBrowser = browser.Open };

            await client.SignInAsync();
            await browser.Completed;

            string tokenRequest = await tokenEndpoint.RequestBody;
            Assert.NotNull(LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "code_challenge"));
            Assert.NotNull(LoopbackTestHttp.GetParameter(tokenRequest, "code_verifier"));
            Assert.Null(LoopbackTestHttp.GetParameter(tokenRequest, "client_secret"));
        }

        private static GoogleOAuth2Options CreateOptions(string tokenEndpoint)
        {
            return new GoogleOAuth2Options
            {
                ClientId = "client-id",
                RedirectUri = "http://127.0.0.1:0/callback",
                TokenEndpoint = tokenEndpoint
            };
        }

        /// <summary>
        /// Stands in for the browser. It follows the redirect URI of the authorization URL once for each request template,
        /// where <c>{path}</c> is the redirect path and <c>{state}</c> is the state of the sign-in.
        /// </summary>
        private sealed class FakeBrowser
        {
            private readonly string[] _requests;

            public FakeBrowser(params string[] requests)
            {
                _requests = requests;
            }

            public string? AuthorizationUrl { get; private set; }

            public List<string> Responses { get; } = [];

            public Task Completed { get; private set; } = Task.CompletedTask;

            public void Open(string url)
            {
                AuthorizationUrl = url;
                var redirectUri = new Uri(LoopbackTestHttp.GetQueryValue(url, "redirect_uri")!);
                string state = Uri.EscapeDataString(LoopbackTestHttp.GetQueryValue(url, "state")!);
                Completed = Task.Run(async () =>
                {
                    foreach (string template in _requests)
                    {
                        string pathAndQuery = template
                            .Replace("{path}", redirectUri.AbsolutePath, StringComparison.Ordinal)
                            .Replace("{state}", state, StringComparison.Ordinal);
                        Responses.Add(await LoopbackTestHttp.GetAsync(redirectUri, pathAndQuery));
                    }
                });
            }
        }
    }
}
