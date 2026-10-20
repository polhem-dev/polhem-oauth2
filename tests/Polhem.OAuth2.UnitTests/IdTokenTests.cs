using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class IdTokenTests
    {
        private const string ClientId = "1234567890";
        private const string Issuer = "https://idp.example.com";

        [Fact]
        [DisplayName("ReadClaims returns the payload of a token issued to the client by the issuer")]
        public void ReadClaims_AudienceAndIssuerMatch_ReturnsPayload()
        {
            string idToken = TestIdToken.Create("""{"iss":"https://idp.example.com","aud":"1234567890","sub":"u1"}""");

            using var claims = IdToken.ReadClaims(idToken, ClientId, Issuer);

            Assert.Equal("u1", claims.RootElement.GetProperty("sub").GetString());
        }

        [Theory]
        [DisplayName("ReadClaims accepts an audience that contains the client ID, with an authorized party that is the client when there are other audiences")]
        [InlineData("""{"aud":["1234567890"]}""")]
        [InlineData("""{"aud":["other","1234567890"],"azp":"1234567890"}""")]
        [InlineData("""{"aud":"1234567890","azp":"1234567890"}""")]
        public void ReadClaims_AudienceContainsClient_ReturnsPayload(string payload)
        {
            using var claims = IdToken.ReadClaims(TestIdToken.Create(payload), ClientId, issuer: null);

            Assert.True(claims.RootElement.TryGetProperty("aud", out _));
        }

        [Theory]
        [DisplayName("ReadClaims refuses a token issued to another client, without an audience, or for another authorized party")]
        [InlineData("""{"aud":"other"}""")]
        [InlineData("""{"aud":["other"]}""")]
        [InlineData("""{"aud":1234567890}""")]
        [InlineData("""{"sub":"u1"}""")]
        [InlineData("""{"aud":["other","1234567890"],"azp":"other"}""")]
        [InlineData("""{"aud":["other","1234567890"]}""")]
        [InlineData("""{"aud":["1234567890",1]}""")]
        [InlineData("""{"aud":"1234567890","azp":null}""")]
        public void ReadClaims_AudienceDoesNotMatch_ThrowsOAuth2Exception(string payload)
        {
            var exception = Assert.Throws<OAuth2Exception>(() => IdToken.ReadClaims(TestIdToken.Create(payload), ClientId, issuer: null));

            Assert.Contains("another client", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("ReadClaims refuses every token when the client ID is empty")]
        public void ReadClaims_EmptyClientId_ThrowsOAuth2Exception()
        {
            Assert.Throws<OAuth2Exception>(() => IdToken.ReadClaims(TestIdToken.Create("""{"aud":""}"""), string.Empty, issuer: null));
        }

        [Theory]
        [DisplayName("ReadClaims refuses a token from another issuer, compared ordinally, or without an issuer when one is expected")]
        [InlineData("""{"aud":"1234567890","iss":"https://evil.example.com"}""")]
        [InlineData("""{"aud":"1234567890","iss":"https://idp.example.com/"}""")]
        [InlineData("""{"aud":"1234567890","iss":"https://IDP.example.com"}""")]
        [InlineData("""{"aud":"1234567890"}""")]
        [InlineData("""{"aud":"1234567890","iss":["https://idp.example.com"]}""")]
        public void ReadClaims_IssuerDoesNotMatch_ThrowsOAuth2Exception(string payload)
        {
            var exception = Assert.Throws<OAuth2Exception>(() => IdToken.ReadClaims(TestIdToken.Create(payload), ClientId, Issuer));

            Assert.Contains("another issuer", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ReadClaims leaves the issuer unchecked when none is expected")]
        [InlineData(null)]
        [InlineData("")]
        public void ReadClaims_NoExpectedIssuer_AcceptsAnyIssuer(string? issuer)
        {
            using var claims = IdToken.ReadClaims(TestIdToken.Create("""{"aud":"1234567890","iss":"https://other.example.com"}"""), ClientId, issuer);

            Assert.Equal("https://other.example.com", claims.RootElement.GetProperty("iss").GetString());
        }

        [Theory]
        [DisplayName("ReadClaims refuses a token that is not a signed JSON Web Token with a JSON object as its payload")]
        [InlineData("not-a-token")]
        [InlineData("a.b")]
        [InlineData("a.b.c.d.e")]
        [InlineData("a.!!!.c")]
        [InlineData("a.b.c")]
        public void ReadClaims_MalformedToken_ThrowsOAuth2Exception(string idToken)
        {
            Assert.Throws<OAuth2Exception>(() => IdToken.ReadClaims(idToken, ClientId, Issuer));
        }

        [Theory]
        [DisplayName("ReadClaims refuses a payload that is not a JSON object")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("\"1234567890\"")]
        public void ReadClaims_PayloadNotJsonObject_ThrowsOAuth2Exception(string payload)
        {
            Assert.Throws<OAuth2Exception>(() => IdToken.ReadClaims(TestIdToken.Create(payload), ClientId, issuer: null));
        }

        [Fact]
        [DisplayName("LINE takes the email address from the ID token")]
        public void Line_ParseUserJsonWithIdToken_MapsEmailFromIdToken()
        {
            var user = CreateLineProvider().ParseUserJson("""{"userId":"U1","displayName":"Ada"}""", LineToken("""{"aud":"1234567890","email":"ada@example.com"}"""));

            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("LINE does not check the issuer of the ID token, which only adds the email address")]
        public void Line_ParseUserJsonWithAnyIssuer_MapsEmailFromIdToken()
        {
            var user = CreateLineProvider().ParseUserJson("""{"userId":"U1"}""", LineToken("""{"iss":"https://other.example.com","aud":"1234567890","email":"ada@example.com"}"""));

            Assert.Equal("ada@example.com", user.Email);
        }

        [Theory]
        [DisplayName("LINE signs the user in without an email address when the ID token cannot be read, is for another client or has no email claim")]
        [InlineData("not-a-token")]
        [InlineData("a.!!!.c")]
        [InlineData("""{"aud":"other","email":"ada@example.com"}""")]
        [InlineData("""{"email":"ada@example.com"}""")]
        [InlineData("""{"aud":"1234567890"}""")]
        [InlineData("[]")]
        public void Line_UnreadableIdToken_MapsUserWithoutEmail(string tokenOrPayload)
        {
            string idToken = tokenOrPayload.Contains('.', StringComparison.Ordinal) && !tokenOrPayload.StartsWith('{') ? tokenOrPayload : TestIdToken.Create(tokenOrPayload);

            var user = CreateLineProvider().ParseUserJson("""{"userId":"U1","displayName":"Ada"}""", new TokenResponse("access", idToken: idToken));

            Assert.Equal("U1", user.UserId);
            Assert.Null(user.Email);
        }

        private static LineOAuth2Provider CreateLineProvider()
        {
            return new LineOAuth2Provider(new LineOAuth2Options { ClientId = ClientId }, null);
        }

        private static TokenResponse LineToken(string payload)
        {
            return new TokenResponse("access", idToken: TestIdToken.Create(payload));
        }
    }
}
