using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    // These tests pin how each provider maps its user information response, so that replacing the JSON library cannot
    // change the result unnoticed. They were first run against the Newtonsoft.Json implementation, which differed in two
    // cases on purpose: it turned a JSON null into an empty string, which also kept the fallback fields from applying.
    // See Google_ParseUserJson_NullField_MapsToNull and Azure_ParseUserJson_NullOid_FallsBackToSub.
    public class ProviderUserJsonTests
    {
        [Fact]
        [DisplayName("Google maps sub, name and email")]
        public void Google_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"107691503500061507151","name":"Ada Lovelace","email":"ada@example.com","email_verified":true}""";

            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options()).ParseUserJson(json);

            Assert.Equal("107691503500061507151", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
            Assert.Equal(json, user.RawJson);
        }

        [Fact]
        [DisplayName("A field missing from the response maps to null")]
        public void Google_ParseUserJson_MissingFields_MapToNull()
        {
            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options()).ParseUserJson("""{"sub":"1"}""");

            Assert.Null(user.UserName);
            Assert.Null(user.Email);
        }

        [Fact]
        [DisplayName("A field whose value is JSON null maps to null")]
        public void Google_ParseUserJson_NullField_MapsToNull()
        {
            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options()).ParseUserJson("""{"sub":"1","name":null}""");

            Assert.Null(user.UserName);
        }

        [Fact]
        [DisplayName("Facebook maps id, name and email")]
        public void Facebook_ParseUserJson_MapsIdNameAndEmail()
        {
            const string json = """{"id":"1234567890","name":"Ada Lovelace","email":"ada@example.com","picture":{"data":{"url":"https://example.com/a.png"}}}""";

            var user = new FacebookOAuth2Provider(new FacebookOAuth2Options()).ParseUserJson(json);

            Assert.Equal("1234567890", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("A numeric identifier maps to its digits")]
        public void Facebook_ParseUserJson_NumericId_MapsToDigits()
        {
            var user = new FacebookOAuth2Provider(new FacebookOAuth2Options()).ParseUserJson("""{"id":1234567890,"name":"Ada"}""");

            Assert.Equal("1234567890", user.UserId);
        }

        [Fact]
        [DisplayName("LINE maps userId and displayName and has no email address")]
        public void Line_ParseUserJson_MapsUserIdAndDisplayName()
        {
            const string json = """{"userId":"U4af4980629a1b2c3d4e5f60718293a4b","displayName":"Ada","pictureUrl":"https://example.com/a.png"}""";

            var user = new LineOAuth2Provider(new LineOAuth2Options()).ParseUserJson(json);

            Assert.Equal("U4af4980629a1b2c3d4e5f60718293a4b", user.UserId);
            Assert.Equal("Ada", user.UserName);
            Assert.Null(user.Email);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID prefers oid and email")]
        public void Azure_ParseUserJson_PrefersOidAndEmail()
        {
            const string json = """{"oid":"00000000-0000-0000-66f3-3332eca7ea81","sub":"AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ","name":"Ada Lovelace","email":"ada@contoso.com","userPrincipalName":"ada.upn@contoso.com"}""";

            var user = new AzureOAuth2Provider(new AzureOAuth2Options()).ParseUserJson(json);

            Assert.Equal("00000000-0000-0000-66f3-3332eca7ea81", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@contoso.com", user.Email);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID falls back to sub and userPrincipalName")]
        public void Azure_ParseUserJson_FallsBackToSubAndUserPrincipalName()
        {
            const string json = """{"sub":"AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ","name":"Ada Lovelace","userPrincipalName":"ada.upn@contoso.com"}""";

            var user = new AzureOAuth2Provider(new AzureOAuth2Options()).ParseUserJson(json);

            Assert.Equal("AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ", user.UserId);
            Assert.Equal("ada.upn@contoso.com", user.Email);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID falls back to sub when oid is JSON null")]
        public void Azure_ParseUserJson_NullOid_FallsBackToSub()
        {
            var user = new AzureOAuth2Provider(new AzureOAuth2Options()).ParseUserJson("""{"oid":null,"sub":"abc"}""");

            Assert.Equal("abc", user.UserId);
        }

        [Fact]
        [DisplayName("Auth0 maps sub, name and email")]
        public void Auth0_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"auth0|5f7c8ec7c33c6c004bbafe82","name":"Ada Lovelace","nickname":"ada","email":"ada@example.com"}""";

            var user = new Auth0OAuth2Provider(new Auth0OAuth2Options()).ParseUserJson(json);

            Assert.Equal("auth0|5f7c8ec7c33c6c004bbafe82", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Auth0 falls back to nickname when name is missing")]
        public void Auth0_ParseUserJson_MissingName_FallsBackToNickname()
        {
            var user = new Auth0OAuth2Provider(new Auth0OAuth2Options()).ParseUserJson("""{"sub":"auth0|1","nickname":"ada"}""");

            Assert.Equal("ada", user.UserName);
        }

        [Fact]
        [DisplayName("Okta maps sub, name and email")]
        public void Okta_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"00uid4BxXw6I6TV4m0g3","name":"Ada Lovelace","preferred_username":"ada@example.com","email":"ada@example.com"}""";

            var user = new OktaOAuth2Provider(new OktaOAuth2Options()).ParseUserJson(json);

            Assert.Equal("00uid4BxXw6I6TV4m0g3", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Okta falls back to preferred_username when name is missing")]
        public void Okta_ParseUserJson_MissingName_FallsBackToPreferredUsername()
        {
            var user = new OktaOAuth2Provider(new OktaOAuth2Options()).ParseUserJson("""{"sub":"00u1","preferred_username":"ada@example.com"}""");

            Assert.Equal("ada@example.com", user.UserName);
        }

        [Theory]
        [DisplayName("ParseUserJson rejects an empty response")]
        [InlineData(null)]
        [InlineData("")]
        public void ParseUserJson_EmptyJson_ThrowsArgumentNullException(string? json)
        {
            var provider = new GoogleOAuth2Provider(new GoogleOAuth2Options());

            Assert.Throws<ArgumentNullException>(() => provider.ParseUserJson(json!));
        }

        [Theory]
        [DisplayName("ParseUserJson rejects a response that is not a JSON object")]
        [InlineData("{")]
        [InlineData("[]")]
        public void ParseUserJson_NotJsonObject_ThrowsJsonException(string json)
        {
            var provider = new GoogleOAuth2Provider(new GoogleOAuth2Options());

            Assert.ThrowsAny<System.Text.Json.JsonException>(() => provider.ParseUserJson(json));
        }
    }
}
