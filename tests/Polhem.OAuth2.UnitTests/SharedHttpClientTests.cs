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

            // SocketsHttpHandler on .NET and HttpClientHandler on .NET Framework both have the property.
            object? allowAutoRedirect = handler.GetType().GetProperty("AllowAutoRedirect")?.GetValue(handler);

            Assert.Equal(false, allowAutoRedirect);
        }

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
