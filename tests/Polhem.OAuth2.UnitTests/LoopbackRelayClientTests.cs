using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class LoopbackRelayClientTests
    {
        [Theory]
        [DisplayName("The constructor rejects a redirect URI that is not an http URI of 127.0.0.1 or [::1] with a path and nothing else")]
        [InlineData("")]
        [InlineData("callback")]
        [InlineData("http://localhost:0/callback")]
        [InlineData("https://127.0.0.1:0/callback")]
        [InlineData("http://127.0.0.2:0/callback")]
        [InlineData("http://example.com/callback")]
        [InlineData("http://127.0.0.1:0")]
        [InlineData("http://127.0.0.1:0/")]
        [InlineData("http://127.0.0.1:0/callback?source=desktop")]
        [InlineData("http://127.0.0.1:0/callback#fragment")]
        [InlineData("http://user@127.0.0.1:0/callback")]
        public void Constructor_InvalidRedirectUri_ThrowsArgumentException(string redirectUri)
        {
            Assert.Throws<ArgumentException>(() => new LoopbackRelayClient(redirectUri));
        }

        [Theory]
        [DisplayName("The constructor accepts a loopback redirect URI of 127.0.0.1 or [::1], with port 0 or a fixed port")]
        [InlineData("http://127.0.0.1:0/callback")]
        [InlineData("http://127.0.0.1:53682/callback")]
        [InlineData("http://[::1]:0/callback")]
        public void Constructor_LoopbackRedirectUri_Succeeds(string redirectUri)
        {
            Assert.Null(Record.Exception(() => new LoopbackRelayClient(redirectUri)));
        }

        [Fact]
        [DisplayName("The constructor and SignInAsync reject null arguments")]
        public async Task NullArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoopbackRelayClient(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => new LoopbackRelayClient("http://127.0.0.1:0/callback").SignInAsync(null!));
        }

        [Fact]
        [DisplayName("SignInAsync returns the code of the redirect with the verifier of the challenge it passed to the sign-in URL")]
        public async Task SignInAsync_RedirectWithCode_ReturnsCodeAndVerifier()
        {
            var browser = new FakeRelayBrowser("{redirect}&code=abc");
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback") { OpenBrowser = browser.Open };

            var result = await client.SignInAsync(CreateSignInUrl).WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.True(result.IsSuccess);
            Assert.Equal("abc", result.Code);
            Assert.Equal(browser.CodeChallenge, Pkce.GenerateCodeChallenge(result.CodeVerifier));
            var redirectUri = new Uri(browser.RedirectUri!);
            Assert.NotEqual(0, redirectUri.Port);
            Assert.Equal("/callback", redirectUri.AbsolutePath);
            Assert.Matches("^\\?state=[A-Za-z0-9_-]{43}$", redirectUri.Query);
            Assert.StartsWith("HTTP/1.1 200 OK", browser.Responses[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync ignores requests without the state of the sign-in, as a web page could send, and uses the genuine redirect")]
        public async Task SignInAsync_RequestsWithoutState_AreIgnored()
        {
            var browser = new FakeRelayBrowser("/favicon.ico", "{path}?code=forged", "{path}?state=forged&code=forged", "{redirect}&code=genuine");
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback") { OpenBrowser = browser.Open };

            var result = await client.SignInAsync(CreateSignInUrl).WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.Equal("genuine", result.Code);
            Assert.StartsWith("HTTP/1.1 404", browser.Responses[0], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 400", browser.Responses[1], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 400", browser.Responses[2], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync turns an error redirect into a failed result whose OAuth2Exception carries the error")]
        public async Task SignInAsync_ErrorRedirect_ReturnsFailure()
        {
            var browser = new FakeRelayBrowser("{redirect}&error=access_denied");
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback") { OpenBrowser = browser.Open };

            var result = await client.SignInAsync(CreateSignInUrl).WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.False(result.IsSuccess);
            Assert.Equal("access_denied", Assert.IsType<OAuth2Exception>(result.Exception).Error);
            Assert.Null(result.Code);
            Assert.Null(result.CodeVerifier);
            Assert.Contains("did not complete", browser.Responses[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync turns a redirect without a code into a failed result")]
        public async Task SignInAsync_RedirectWithoutCode_ReturnsFailure()
        {
            var browser = new FakeRelayBrowser("{redirect}");
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback") { OpenBrowser = browser.Open };

            var result = await client.SignInAsync(CreateSignInUrl).WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns a missing redirect into a failed result with TimeoutException")]
        public async Task SignInAsync_NoRedirect_ReturnsTimeout()
        {
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback")
            {
                OpenBrowser = _ => Task.CompletedTask,
                Timeout = TimeSpan.FromMilliseconds(100)
            };

            var result = await client.SignInAsync(CreateSignInUrl).WithTimeout();

            Assert.IsType<TimeoutException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation while waiting into a failed result, and does not open the browser when already canceled")]
        public async Task SignInAsync_Canceled_ReturnsOperationCanceled()
        {
            using var cancellation = new CancellationTokenSource();
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback")
            {
                OpenBrowser = _ =>
                {
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }
            };

            var whileWaiting = await client.SignInAsync(CreateSignInUrl, cancellation.Token).WithTimeout();
            bool opened = false;
            client.OpenBrowser = _ =>
            {
                opened = true;
                return Task.CompletedTask;
            };
            var beforeStart = await client.SignInAsync(CreateSignInUrl, cancellation.Token).WithTimeout();

            Assert.IsType<OperationCanceledException>(whileWaiting.Exception, exactMatch: false);
            Assert.IsType<OperationCanceledException>(beforeStart.Exception, exactMatch: false);
            Assert.False(opened);
        }

        [Fact]
        [DisplayName("SignInAsync rejects a sign-in URL that is not an absolute http or https URL before opening it")]
        public async Task SignInAsync_InvalidSignInUrl_ThrowsInvalidOperationException()
        {
            bool opened = false;
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback")
            {
                OpenBrowser = _ =>
                {
                    opened = true;
                    return Task.CompletedTask;
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync((_, _) => new Uri("file:///tmp/signin")));
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync((_, _) => null!));
            Assert.False(opened);
        }

        [Fact]
        [DisplayName("SignInAsync rejects a second sign-in while one is in progress, and accepts one after it ends")]
        public async Task SignInAsync_Concurrent_ThrowsInvalidOperationException()
        {
            var opened = new TaskCompletionSource<bool>();
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback")
            {
                OpenBrowser = _ =>
                {
                    opened.TrySetResult(true);
                    return Task.CompletedTask;
                },
                Timeout = TimeSpan.FromMilliseconds(500)
            };

            var first = client.SignInAsync(CreateSignInUrl);
            await opened.Task.WithTimeout();

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync(CreateSignInUrl));
            Assert.IsType<TimeoutException>((await first.WithTimeout()).Exception);
            client.Timeout = TimeSpan.FromMilliseconds(50);
            Assert.IsType<TimeoutException>((await client.SignInAsync(CreateSignInUrl).WithTimeout()).Exception);
        }

        private static Uri CreateSignInUrl(Uri redirectUri, string codeChallenge)
        {
            return FakeRelayBrowser.CreateSignInUrl(redirectUri, codeChallenge);
        }
    }
}
