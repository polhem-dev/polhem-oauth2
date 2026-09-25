using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

namespace Polhem.OAuth2.UnitTests
{
    public class LoopbackListenerTests
    {
        private static readonly TimeSpan s_readTimeout = TimeSpan.FromSeconds(5);

        [Theory]
        [DisplayName("IsLoopbackRedirectUri accepts only http URIs on localhost or a loopback address")]
        [InlineData("http://localhost:53682/callback", true)]
        [InlineData("http://127.0.0.1:0/callback", true)]
        [InlineData("http://[::1]:53682/callback", true)]
        [InlineData("https://localhost:53682/callback", false)]
        [InlineData("http://example.com:53682/callback", false)]
        [InlineData("http://192.168.1.1:53682/callback", false)]
        public void IsLoopbackRedirectUri_VariousUris_AcceptsOnlyLoopbackHttp(string redirectUri, bool expected)
        {
            Assert.Equal(expected, LoopbackListener.IsLoopbackRedirectUri(new Uri(redirectUri)));
        }

        [Fact]
        [DisplayName("Start rejects a redirect URI that is not on a loopback address")]
        public void Start_NonLoopbackRedirectUri_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LoopbackListener.Start(new Uri("http://example.com:53682/callback")));
        }

        [Fact]
        [DisplayName("Start with port 0 binds a free port and keeps the path")]
        public void Start_PortZero_BindsFreePort()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));

            Assert.NotEqual(0, listener.RedirectUri.Port);
            Assert.Equal("/callback", listener.RedirectUri.AbsolutePath);
        }

        [Fact]
        [DisplayName("Start fails when the IPv4 loopback port is already in use")]
        public void Start_PortInUse_ThrowsSocketException()
        {
            var blocker = new TcpListener(IPAddress.Loopback, 0);
            blocker.Start();
            try
            {
                int port = ((IPEndPoint)blocker.LocalEndpoint).Port;

                Assert.Throws<SocketException>(() => LoopbackListener.Start(new Uri($"http://localhost:{port}/callback")));
            }
            finally
            {
                // TcpListener is not IDisposable on .NET Framework.
                blocker.Stop();
            }
        }

        // The browser may resolve localhost to the IPv6 loopback, so that program could receive the authorization code.
        [Ipv6Fact]
        [DisplayName("Start for localhost fails when another program uses the port on the IPv6 loopback")]
        public void Start_LocalhostWithIpv6PortInUse_ThrowsSocketException()
        {
            var blocker = new TcpListener(IPAddress.IPv6Loopback, 0);
            blocker.Start();
            try
            {
                int port = ((IPEndPoint)blocker.LocalEndpoint).Port;

                Assert.Throws<SocketException>(() => LoopbackListener.Start(new Uri($"http://localhost:{port}/callback")));
            }
            finally
            {
                // TcpListener is not IDisposable on .NET Framework.
                blocker.Stop();
            }
        }

        [Ipv6Fact]
        [DisplayName("Start for localhost with port 0 also accepts connections on the IPv6 loopback")]
        public async Task Start_LocalhostPortZero_AcceptsIpv6Connection()
        {
            using var listener = LoopbackListener.Start(new Uri("http://localhost:0/callback"));
            var send = LoopbackTestHttp.SendAsync(IPAddress.IPv6Loopback, listener.RedirectUri.Port, "GET /callback?code=abc HTTP/1.1\r\n\r\n");

            using (var request = await AcceptRequestAsync(listener))
            {
                Assert.Equal("/callback?code=abc", request.Target);
                await request.RespondAsync("200 OK", "Done.");
            }

            Assert.StartsWith("HTTP/1.1 200 OK", await send.WithTimeout(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("ReadAsync returns the target and Host header of a GET request, and RespondAsync sends an HTML-encoded page")]
        public async Task ReadAsync_GetRequest_ReturnsTargetAndHost()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            var send = LoopbackTestHttp.GetAsync(listener.RedirectUri, "/callback?code=abc&state=xyz");

            using (var request = await AcceptRequestAsync(listener))
            {
                Assert.Equal("/callback?code=abc&state=xyz", request.Target);
                Assert.Equal(listener.RedirectUri.Authority, request.Host);
                await request.RespondAsync("200 OK", "<done>");
            }

            string response = await send.WithTimeout();
            Assert.StartsWith("HTTP/1.1 200 OK", response, StringComparison.Ordinal);
            Assert.Contains("&lt;done&gt;", response, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ReadAsync reads the Host header only when the request has exactly one")]
        [InlineData("GET /callback HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nAccept: */*\r\n\r\n", "127.0.0.1:1234")]
        [InlineData("GET /callback HTTP/1.1\nhost:  127.0.0.1:1234 \n\n", "127.0.0.1:1234")]
        [InlineData("GET /callback HTTP/1.1\r\nAccept: */*\r\n\r\n", null)]
        [InlineData("GET /callback HTTP/1.1\r\nHost: a.example\r\nHost: b.example\r\n\r\n", null)]
        public async Task ReadAsync_HostHeaders_ReadsSingleHost(string rawRequest, string? expectedHost)
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            var send = LoopbackTestHttp.SendAsync(IPAddress.Loopback, listener.RedirectUri.Port, rawRequest);

            using (var request = await AcceptRequestAsync(listener))
            {
                Assert.Equal("/callback", request.Target);
                Assert.Equal(expectedHost, request.Host);
                await request.RespondAsync("200 OK", "Done.");
            }

            await send.WithTimeout();
        }

        [Fact]
        [DisplayName("ReadAsync returns no target for a request that is not GET")]
        public async Task ReadAsync_PostRequest_ReturnsNullTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            var send = LoopbackTestHttp.SendAsync(IPAddress.Loopback, listener.RedirectUri.Port, "POST /callback?code=abc HTTP/1.1\r\nContent-Length: 0\r\n\r\n");

            using (var request = await AcceptRequestAsync(listener))
            {
                Assert.Null(request.Target);
            }

            await send.WithTimeout();
        }

        [Fact]
        [DisplayName("ReadAsync returns no target for a request whose head is longer than 16 KiB, so a page cannot hold the listener with one")]
        public async Task ReadAsync_HeadLongerThanLimit_ReturnsNullTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            string head = "GET /callback HTTP/1.1\r\nHost: x\r\nX-Filler: " + new string('a', 17 * 1024) + "\r\n\r\n";
            var send = LoopbackTestHttp.SendAsync(IPAddress.Loopback, listener.RedirectUri.Port, head);

            using (var request = await AcceptRequestAsync(listener))
            {
                Assert.Null(request.Target);
            }

            await send.WithTimeout();
        }

        [Fact]
        [DisplayName("ReadAsync stops waiting for a connection that sends nothing")]
        public async Task ReadAsync_IdleConnection_ReturnsNullTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            using var idle = new TcpClient(AddressFamily.InterNetwork);
            await idle.ConnectAsync(IPAddress.Loopback, listener.RedirectUri.Port);

            using var request = await AcceptRequestAsync(listener, TimeSpan.FromMilliseconds(200));

            Assert.Null(request.Target);
        }

        [Fact]
        [DisplayName("AcceptClientAsync throws OperationCanceledException when the wait is canceled")]
        public async Task AcceptClientAsync_Canceled_ThrowsOperationCanceledException()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.AcceptClientAsync(cancellation.Token).WithTimeout());
        }

        [Theory]
        [DisplayName("TryGetRedirectQuery matches only the redirect path, decoding percent-encoded characters, and returns the query string")]
        [InlineData("/callback?code=abc", true, "code=abc")]
        [InlineData("/callback", true, "")]
        [InlineData("/call%62ack?code=abc", true, "code=abc")]
        [InlineData("/favicon.ico", false, "")]
        [InlineData("/callback/other?code=abc", false, "code=abc")]
        public void TryGetRedirectQuery_VariousTargets_MatchesRedirectPath(string target, bool expected, string expectedQuery)
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"));

            bool matched = listener.TryGetRedirectQuery(target, out string query);

            Assert.Equal(expected, matched);
            Assert.Equal(expectedQuery, query);
        }

        [Fact]
        [DisplayName("IsRedirectHost accepts only the host and port of the redirect URI")]
        public void IsRedirectHost_VariousHosts_AcceptsOnlyRedirectAuthority()
        {
            using var listener = LoopbackListener.Start(new Uri("http://localhost:0/callback"));
            int port = listener.RedirectUri.Port;

            Assert.True(listener.IsRedirectHost($"localhost:{port}"));
            Assert.True(listener.IsRedirectHost($"LOCALHOST:{port}"));
            Assert.False(listener.IsRedirectHost($"127.0.0.1:{port}"));
            Assert.False(listener.IsRedirectHost($"localhost:{port + 1}"));
            Assert.False(listener.IsRedirectHost("attacker.example"));
            Assert.False(listener.IsRedirectHost(null));
        }

        private static async Task<LoopbackRequest> AcceptRequestAsync(LoopbackListener listener, TimeSpan? readTimeout = null)
        {
            TcpClient client = await listener.AcceptClientAsync(CancellationToken.None).WithTimeout();
            return await LoopbackRequest.ReadAsync(client, readTimeout ?? s_readTimeout, CancellationToken.None).WithTimeout();
        }
    }
}
