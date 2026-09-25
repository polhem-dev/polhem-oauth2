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
    }
}
