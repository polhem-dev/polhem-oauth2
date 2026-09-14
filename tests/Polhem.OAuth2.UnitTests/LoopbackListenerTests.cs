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
            Assert.Throws<ArgumentException>(() => LoopbackListener.Start(new Uri("http://example.com:53682/callback"), s_readTimeout));
        }

        [Fact]
        [DisplayName("Start with port 0 binds a free port and keeps the path")]
        public void Start_PortZero_BindsFreePort()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), s_readTimeout);

            Assert.NotEqual(0, listener.RedirectUri.Port);
            Assert.Equal("/callback", listener.RedirectUri.AbsolutePath);
        }

        [Fact]
        [DisplayName("Start fails when the IPv4 loopback port is already in use")]
        public void Start_PortInUse_ThrowsSocketException()
        {
            using var blocker = new TcpListener(IPAddress.Loopback, 0);
            blocker.Start();
            int port = ((IPEndPoint)blocker.LocalEndpoint).Port;

            Assert.Throws<SocketException>(() => LoopbackListener.Start(new Uri($"http://localhost:{port}/callback"), s_readTimeout));
        }

        [Fact]
        [DisplayName("Start for localhost fails when another program uses the port on the IPv6 loopback")]
        public void Start_LocalhostWithIpv6PortInUse_ThrowsSocketException()
        {
            // The browser may resolve localhost to the IPv6 loopback, so that program could receive the authorization code.
            // On a machine without IPv6 the situation cannot arise.
            if (!Socket.OSSupportsIPv6)
                return;

            using var blocker = new TcpListener(IPAddress.IPv6Loopback, 0);
            blocker.Start();
            int port = ((IPEndPoint)blocker.LocalEndpoint).Port;

            Assert.Throws<SocketException>(() => LoopbackListener.Start(new Uri($"http://localhost:{port}/callback"), s_readTimeout));
        }

        [Fact]
        [DisplayName("Start for localhost with port 0 also accepts connections on the IPv6 loopback")]
        public async Task Start_LocalhostPortZero_AcceptsIpv6Connection()
        {
            if (!Socket.OSSupportsIPv6)
                return;

            using var listener = LoopbackListener.Start(new Uri("http://localhost:0/callback"), s_readTimeout);
            var send = LoopbackTestHttp.SendAsync(IPAddress.IPv6Loopback, listener.RedirectUri.Port, "GET /callback?code=abc HTTP/1.1\r\n\r\n");

            using (var request = await listener.AcceptAsync(CancellationToken.None))
            {
                Assert.Equal("/callback?code=abc", request.Target);
                await request.RespondAsync("200 OK", "Done.");
            }

            Assert.StartsWith("HTTP/1.1 200 OK", await send, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("AcceptAsync returns the target of a GET request, and RespondAsync sends an HTML-encoded page")]
        public async Task AcceptAsync_GetRequest_ReturnsTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), s_readTimeout);
            var send = LoopbackTestHttp.GetAsync(listener.RedirectUri, "/callback?code=abc&state=xyz");

            using (var request = await listener.AcceptAsync(CancellationToken.None))
            {
                Assert.Equal("/callback?code=abc&state=xyz", request.Target);
                await request.RespondAsync("200 OK", "<done>");
            }

            string response = await send;
            Assert.StartsWith("HTTP/1.1 200 OK", response, StringComparison.Ordinal);
            Assert.Contains("&lt;done&gt;", response, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("AcceptAsync returns no target for a request that is not GET")]
        public async Task AcceptAsync_PostRequest_ReturnsNullTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), s_readTimeout);
            var send = LoopbackTestHttp.SendAsync(IPAddress.Loopback, listener.RedirectUri.Port, "POST /callback?code=abc HTTP/1.1\r\nContent-Length: 0\r\n\r\n");

            using (var request = await listener.AcceptAsync(CancellationToken.None))
            {
                Assert.Null(request.Target);
            }

            await send;
        }

        [Fact]
        [DisplayName("AcceptAsync stops waiting for a connection that sends nothing")]
        public async Task AcceptAsync_IdleConnection_ReturnsNullTarget()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), TimeSpan.FromMilliseconds(200));
            using var idle = new TcpClient(AddressFamily.InterNetwork);
            await idle.ConnectAsync(IPAddress.Loopback, listener.RedirectUri.Port);

            using var request = await listener.AcceptAsync(CancellationToken.None);

            Assert.Null(request.Target);
        }

        [Fact]
        [DisplayName("AcceptAsync throws OperationCanceledException when the wait is canceled")]
        public async Task AcceptAsync_Canceled_ThrowsOperationCanceledException()
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), s_readTimeout);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.AcceptAsync(cancellation.Token));
        }

        [Theory]
        [DisplayName("TryGetRedirectQuery matches only the redirect path and returns the query string")]
        [InlineData("/callback?code=abc", true, "code=abc")]
        [InlineData("/callback", true, "")]
        [InlineData("/favicon.ico", false, "")]
        [InlineData("/callback/other?code=abc", false, "code=abc")]
        public void TryGetRedirectQuery_VariousTargets_MatchesRedirectPath(string target, bool expected, string expectedQuery)
        {
            using var listener = LoopbackListener.Start(new Uri("http://127.0.0.1:0/callback"), s_readTimeout);

            bool matched = listener.TryGetRedirectQuery(target, out string query);

            Assert.Equal(expected, matched);
            Assert.Equal(expectedQuery, query);
        }
    }
}
