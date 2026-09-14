using System.ComponentModel;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    public class LineIdTokenTests
    {
        private const string ClientId = "1234567890";

        [Fact]
        [DisplayName("ReadEmail returns the email claim of a token issued to the client")]
        public void ReadEmail_AudienceMatches_ReturnsEmail()
        {
            string idToken = CreateIdToken("""{"iss":"https://access.line.me","aud":"1234567890","email":"ada@example.com"}""");

            Assert.Equal("ada@example.com", LineIdToken.ReadEmail(idToken, ClientId));
        }

        [Fact]
        [DisplayName("ReadEmail accepts an audience array that contains the client ID")]
        public void ReadEmail_AudienceArrayContainsClient_ReturnsEmail()
        {
            string idToken = CreateIdToken("""{"aud":["other","1234567890"],"email":"ada@example.com"}""");

            Assert.Equal("ada@example.com", LineIdToken.ReadEmail(idToken, ClientId));
        }

        [Theory]
        [DisplayName("ReadEmail ignores a token issued to another client or without an audience")]
        [InlineData("""{"aud":"other","email":"ada@example.com"}""")]
        [InlineData("""{"aud":["other"],"email":"ada@example.com"}""")]
        [InlineData("""{"email":"ada@example.com"}""")]
        public void ReadEmail_AudienceDoesNotMatch_ReturnsNull(string payload)
        {
            Assert.Null(LineIdToken.ReadEmail(CreateIdToken(payload), ClientId));
        }

        [Fact]
        [DisplayName("ReadEmail returns null when the token has no email claim")]
        public void ReadEmail_NoEmailClaim_ReturnsNull()
        {
            Assert.Null(LineIdToken.ReadEmail(CreateIdToken("""{"aud":"1234567890"}"""), ClientId));
        }

        [Theory]
        [DisplayName("ReadEmail returns null for a malformed token")]
        [InlineData("not-a-token")]
        [InlineData("a.b")]
        [InlineData("a.b.c.d")]
        [InlineData("a.!!!.c")]
        public void ReadEmail_MalformedToken_ReturnsNull(string idToken)
        {
            Assert.Null(LineIdToken.ReadEmail(idToken, ClientId));
        }

        [Theory]
        [DisplayName("ReadEmail returns null when the payload is not a JSON object")]
        [InlineData("not json")]
        [InlineData("[]")]
        public void ReadEmail_PayloadNotJsonObject_ReturnsNull(string payload)
        {
            Assert.Null(LineIdToken.ReadEmail(CreateIdToken(payload), ClientId));
        }

        [Fact]
        [DisplayName("LINE takes the email address from the ID token")]
        public void Line_ParseUserJsonWithIdToken_MapsEmailFromIdToken()
        {
            var provider = new LineOAuth2Provider(new LineOAuth2Options { ClientId = ClientId });
            var token = new TokenResponse("access", idToken: CreateIdToken("""{"aud":"1234567890","email":"ada@example.com"}"""));

            var user = provider.ParseUserJson("""{"userId":"U1","displayName":"Ada"}""", token);

            Assert.Equal("ada@example.com", user.Email);
        }

        private static string CreateIdToken(string payload)
        {
            return "eyJhbGciOiJIUzI1NiJ9." + Base64Url.Encode(Encoding.UTF8.GetBytes(payload)) + ".signature";
        }
    }
}
