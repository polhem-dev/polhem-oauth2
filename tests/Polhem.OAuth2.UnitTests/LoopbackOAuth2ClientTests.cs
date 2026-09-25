using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

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
            var options = new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = redirectUri };

            Assert.Throws<ArgumentException>(() => new LoopbackOAuth2Client(options));
        }

        [Fact]
        [DisplayName("The constructor rejects null options")]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoopbackOAuth2Client(null!));
        }

        [Fact]
        [DisplayName("SignInAsync exchanges the code with the redirect URI of the bound port and leaves the options unchanged")]
        public async Task SignInAsync_CallbackWithState_ExchangesCodeWithBoundRedirectUri()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var options = CreateOptions();
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            string? redirectUriWhileSigningIn = null;
            var client = new LoopbackOAuth2Client(options, handler.CreateClient())
            {
                OpenBrowser = url =>
                {
                    redirectUriWhileSigningIn = options.RedirectUri;
                    return browser.Open(url);
                }
            };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            // The stub token endpoint rejects the request without an error code, so the exchange ends as an HTTP failure.
            Assert.IsType<HttpRequestException>(result.Exception);
            var tokenRequest = Assert.Single(handler.Requests);
            string boundRedirectUri = LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "redirect_uri")!;
            Assert.NotEqual(0, new Uri(boundRedirectUri).Port);
            Assert.Equal(boundRedirectUri, tokenRequest.FormValue("redirect_uri"));
            Assert.Equal("abc", tokenRequest.FormValue("code"));
            Assert.StartsWith("HTTP/1.1 200 OK", browser.Responses[0], StringComparison.Ordinal);
            Assert.Equal("http://127.0.0.1:0/callback", redirectUriWhileSigningIn);
            Assert.Equal("http://127.0.0.1:0/callback", options.RedirectUri);
        }

        [Fact]
        [DisplayName("SignInAsync with a fixed port sends the configured redirect URI unchanged to the provider and the token endpoint")]
        public async Task SignInAsync_FixedPort_SendsConfiguredRedirectUri()
        {
            int port = FreePort();
            var options = CreateOptions();
            options.RedirectUri = $"http://127.0.0.1:{port}/callback";
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1"}""");
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(options, handler.CreateClient()) { OpenBrowser = browser.Open };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.True(result.IsSuccess);
            Assert.Equal(options.RedirectUri, LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "redirect_uri"));
            Assert.Equal(options.RedirectUri, handler.Requests[0].FormValue("redirect_uri"));
        }

        [Fact]
        [DisplayName("SignInAsync turns a redirect without a code into a failed result, without a request to the provider")]
        public async Task SignInAsync_RedirectWithoutCode_ReturnsFailedResult()
        {
            var handler = new StubHttpMessageHandler();
            var browser = new FakeBrowser("{path}?state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Contains("did not complete", browser.Responses[0], StringComparison.Ordinal);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("SignInAsync ignores requests without the state of the sign-in and uses the genuine redirect")]
        public async Task SignInAsync_OtherRequestsBeforeRedirect_UsesGenuineRedirect()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeBrowser("/favicon.ico", "{path}?code=forged&state=forged", "{path}?code=genuine&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.Equal("genuine", Assert.Single(handler.Requests).FormValue("code"));
            Assert.StartsWith("HTTP/1.1 404", browser.Responses[0], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 400", browser.Responses[1], StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 200", browser.Responses[2], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync ignores a request whose Host header names another host, even with the state of the sign-in")]
        public async Task SignInAsync_RequestWithOtherHost_IsIgnored()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeBrowser("Host=attacker.example {path}?code=forged&state={state}", "{path}?code=genuine&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.Equal("genuine", Assert.Single(handler.Requests).FormValue("code"));
            Assert.StartsWith("HTTP/1.1 400", browser.Responses[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAsync accepts a redirect whose path is percent-encoded")]
        public async Task SignInAsync_PercentEncodedPath_ReceivesRedirect()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeBrowser("/call%62ack?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.Equal("abc", Assert.Single(handler.Requests).FormValue("code"));
        }

        [Fact]
        [DisplayName("SignInAsync receives the redirect while a connection that sends nothing is still open")]
        public async Task SignInAsync_IdleConnectionBeforeRedirect_ReceivesRedirect()
        {
            // A connection may stay silent for five seconds before it is given up. The timeout is shorter, so the redirect only
            // arrives in time when it does not wait behind the idle connection.
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var browser = new FakeBrowser("idle", "{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient())
            {
                OpenBrowser = browser.Open,
                Timeout = TimeSpan.FromSeconds(3)
            };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns an error redirect into a failed result with the error code")]
        public async Task SignInAsync_ProviderError_ReturnsFailedResultWithErrorCode()
        {
            var handler = new StubHttpMessageHandler();
            var browser = new FakeBrowser("{path}?error=access_denied&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.False(result.IsSuccess);
            Assert.Equal("access_denied", Assert.IsType<OAuth2Exception>(result.Exception).Error);
            Assert.Contains("did not complete", browser.Responses[0], StringComparison.Ordinal);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("SignInAsync turns a missing redirect into a failed result with TimeoutException")]
        public async Task SignInAsync_OnlyForgedRequest_ReturnsFailedResultWithTimeoutException()
        {
            var browser = new FakeBrowser("{path}?code=forged&state=forged");
            var client = new LoopbackOAuth2Client(CreateOptions(), new StubHttpMessageHandler().CreateClient())
            {
                OpenBrowser = browser.Open,
                Timeout = TimeSpan.FromSeconds(1)
            };

            var result = await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.IsType<TimeoutException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation while waiting into a failed result with OperationCanceledException")]
        public async Task SignInAsync_CanceledWhileWaiting_ReturnsFailedResultWithOperationCanceledException()
        {
            using var cancellation = new CancellationTokenSource();
            var client = new LoopbackOAuth2Client(CreateOptions(), new StubHttpMessageHandler().CreateClient())
            {
                OpenBrowser = _ =>
                {
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }
            };

            var result = await client.SignInAsync(cancellation.Token).WithTimeout();

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync turns cancellation during the code exchange into a failed result with OperationCanceledException")]
        public async Task SignInAsync_CanceledDuringExchange_ReturnsFailedResultWithOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Hang();
            using var cancellation = new CancellationTokenSource();
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(CreateOptions(), handler.CreateClient()) { OpenBrowser = browser.Open };

            var signIn = client.SignInAsync(cancellation.Token);
            await handler.Hanging.WithTimeout();
            cancellation.Cancel();
            var result = await signIn.WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync does not open the browser when the token is already canceled")]
        public async Task SignInAsync_AlreadyCanceled_DoesNotOpenBrowser()
        {
            bool opened = false;
            var client = new LoopbackOAuth2Client(CreateOptions(), new StubHttpMessageHandler().CreateClient())
            {
                OpenBrowser = _ =>
                {
                    opened = true;
                    return Task.CompletedTask;
                }
            };

            var result = await client.SignInAsync(new CancellationToken(canceled: true)).WithTimeout();

            Assert.False(opened);
            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        [DisplayName("Timeout is 5 minutes by default, as its documentation says, and rejects a value that is not positive or is too long")]
        public void Timeout_DefaultAndInvalidValues()
        {
            var client = new LoopbackOAuth2Client(CreateOptions());

            Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.FromSeconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1));
        }

        [Fact]
        [DisplayName("SignInAsync lets an exception from OpenBrowser propagate, and the client can sign in again afterwards")]
        public async Task SignInAsync_OpenBrowserThrows_PropagatesAndEndsSignIn()
        {
            var client = new LoopbackOAuth2Client(CreateOptions(), new StubHttpMessageHandler().CreateClient())
            {
                OpenBrowser = _ => Task.FromException(new InvalidOperationException("no browser"))
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());

            Assert.Equal("no browser", ex.Message);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var next = await client.SignInAsync(canceled.Token);
            Assert.IsAssignableFrom<OperationCanceledException>(next.Exception);
        }

        [Fact]
        [DisplayName("SignInAsync refuses to start a second sign-in on the same client")]
        public async Task SignInAsync_WhileInProgress_ThrowsInvalidOperationException()
        {
            var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            var client = new LoopbackOAuth2Client(CreateOptions(), new StubHttpMessageHandler().CreateClient())
            {
                OpenBrowser = _ =>
                {
                    opened.TrySetResult(true);
                    return Task.CompletedTask;
                }
            };

            var first = client.SignInAsync(cancellation.Token);
            await opened.Task.WithTimeout();

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignInAsync());
            cancellation.Cancel();
            Assert.IsAssignableFrom<OperationCanceledException>((await first.WithTimeout()).Exception);
        }

        [Fact]
        [DisplayName("SignInAsync uses PKCE even when the options turn it off, and sends no client secret")]
        public async Task SignInAsync_UsePkceFalse_SendsCodeVerifierWithoutClientSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var options = new FacebookOAuth2Options
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "http://127.0.0.1:0/callback",
                UsePkce = false
            };
            var browser = new FakeBrowser("{path}?code=abc&state={state}");
            var client = new LoopbackOAuth2Client(options, handler.CreateClient()) { OpenBrowser = browser.Open };

            await client.SignInAsync().WithTimeout();
            await browser.Completed.WithTimeout();

            var tokenRequest = Assert.Single(handler.Requests);
            string? verifier = tokenRequest.FormValue("code_verifier");
            Assert.NotNull(verifier);
            Assert.Equal(Pkce.GenerateCodeChallenge(verifier), LoopbackTestHttp.GetQueryValue(browser.AuthorizationUrl!, "code_challenge"));
            Assert.Null(tokenRequest.FormValue("client_secret"));
        }

        [Fact]
        [DisplayName("RefreshTokenAsync does not send the client secret")]
        public async Task RefreshTokenAsync_PublicClient_OmitsClientSecret()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"access_token":"new-access"}""");
            var options = new LineOAuth2Options
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "http://127.0.0.1:0/callback"
            };
            var client = new LoopbackOAuth2Client(options, handler.CreateClient());

            var token = await client.RefreshTokenAsync("refresh");

            var request = Assert.Single(handler.Requests);
            Assert.Equal("refresh", request.FormValue("refresh_token"));
            Assert.Null(request.FormValue("client_secret"));
            Assert.Equal("new-access", token.AccessToken);
        }

        // A port that was free a moment ago. Another program could take it before the test binds it, which would fail the test.
        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private static GoogleOAuth2Options CreateOptions()
        {
            return new GoogleOAuth2Options
            {
                ClientId = "client-id",
                RedirectUri = "http://127.0.0.1:0/callback"
            };
        }

        /// <summary>
        /// Stands in for the browser. It follows the redirect URI of the authorization URL once for each request template,
        /// where <c>{path}</c> is the redirect path and <c>{state}</c> is the state of the sign-in. A template that starts with
        /// <c>Host=</c> and a value sends that Host header instead, and the template <c>idle</c> opens a connection that sends nothing.
        /// </summary>
        private sealed class FakeBrowser
        {
            private const string HostPrefix = "Host=";

            private readonly string[] _requests;

            public FakeBrowser(params string[] requests)
            {
                _requests = requests;
            }

            public string? AuthorizationUrl { get; private set; }

            public List<string> Responses { get; } = [];

            public Task Completed { get; private set; } = Task.CompletedTask;

            public Task Open(Uri url)
            {
                AuthorizationUrl = url.AbsoluteUri;
                var redirectUri = new Uri(LoopbackTestHttp.GetQueryValue(AuthorizationUrl, "redirect_uri")!);
                string state = Uri.EscapeDataString(LoopbackTestHttp.GetQueryValue(AuthorizationUrl, "state")!);
                Completed = Task.Run(async () =>
                {
                    var idleConnections = new List<TcpClient>();
                    try
                    {
                        foreach (string template in _requests)
                        {
                            if (template == "idle")
                            {
                                var idle = new TcpClient(AddressFamily.InterNetwork);
                                idleConnections.Add(idle);
                                await idle.ConnectAsync(IPAddress.Loopback, redirectUri.Port);
                                continue;
                            }

                            string? host = null;
                            string request = template;
                            if (template.StartsWith(HostPrefix, StringComparison.Ordinal))
                            {
                                int space = template.IndexOf(' ');
                                host = template.Substring(HostPrefix.Length, space - HostPrefix.Length);
                                request = template.Substring(space + 1);
                            }

                            string pathAndQuery = request.Replace("{path}", redirectUri.AbsolutePath).Replace("{state}", state);
                            Responses.Add(await LoopbackTestHttp.GetAsync(redirectUri, pathAndQuery, host));
                        }
                    }
                    finally
                    {
                        idleConnections.ForEach(connection => connection.Dispose());
                    }
                });
                return Task.CompletedTask;
            }
        }
    }
}
