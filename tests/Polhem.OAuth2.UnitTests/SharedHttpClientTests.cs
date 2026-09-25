using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class SharedHttpClientTests
    {
        [Fact]
        [DisplayName("The shared HTTP client does not follow redirects, which would send the body of a token request to another location")]
        public void CreateHandler_Always_TurnsOffAutomaticRedirects()
        {
            using var handler = SharedHttpClient.CreateHandler();

            // Read without reflection: a trimmed .NET MAUI application, where these tests also run, drops the unused getter.
#if NET
            bool allowAutoRedirect = Assert.IsType<SocketsHttpHandler>(handler).AllowAutoRedirect;
#else
            bool allowAutoRedirect = Assert.IsType<HttpClientHandler>(handler).AllowAutoRedirect;
#endif

            Assert.False(allowAutoRedirect);
        }

#if NET
        [Fact]
        [DisplayName("On .NET the shared HTTP client replaces a pooled connection after a while, so that it follows DNS changes")]
        public void CreateHandler_OnNet_SetsPooledConnectionLifetime()
        {
            using var handler = Assert.IsType<SocketsHttpHandler>(SharedHttpClient.CreateHandler());

            Assert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
        }
#endif

#if NETFRAMEWORK
        [Fact]
        [DisplayName("On .NET Framework a client on the shared HTTP client limits how long a connection to the token and user information hosts is reused")]
        public void Constructor_SharedClientOnNetFramework_SetsConnectionLeaseTimeout()
        {
            var options = new Auth0OAuth2Options { Domain = "lease-test.auth0.com", ClientId = "client-id", RedirectUri = "https://app.example.com/auth/callback" };

            _ = new OAuth2Client(options);

            Assert.Equal(300000, System.Net.ServicePointManager.FindServicePoint(new Uri(options.TokenEndpoint)).ConnectionLeaseTimeout);
            Assert.Equal(300000, System.Net.ServicePointManager.FindServicePoint(new Uri(options.UserInfoEndpoint)).ConnectionLeaseTimeout);
        }
#endif
    }
}
