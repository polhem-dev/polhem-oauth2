using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    // These tests pin how each provider maps its user information response, so that replacing the JSON library cannot
    // change the result unnoticed. They were first run against the Newtonsoft.Json implementation, which differed on purpose
    // in one respect: it turned a JSON null into an empty string, which also kept the fallback fields from applying.
    // See Google_ParseUserJson_NullField_MapsToNull and Auth0_ParseUserJson_NullName_FallsBackToNickname.
    public class ProviderUserJsonTests
    {
        [Fact]
        [DisplayName("Google maps sub, name and email")]
        public void Google_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"107691503500061507151","name":"Ada Lovelace","email":"ada@example.com","email_verified":true}""";

            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("107691503500061507151", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
            Assert.Equal(json, user.RawJson);
        }

        [Fact]
        [DisplayName("A field missing from the response maps to null")]
        public void Google_ParseUserJson_MissingFields_MapToNull()
        {
            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null).ParseUserJson("""{"sub":"1"}""", null);

            Assert.Null(user.UserName);
            Assert.Null(user.Email);
        }

        [Fact]
        [DisplayName("A field whose value is JSON null maps to null")]
        public void Google_ParseUserJson_NullField_MapsToNull()
        {
            var user = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null).ParseUserJson("""{"sub":"1","name":null}""", null);

            Assert.Null(user.UserName);
        }

        [Theory]
        [DisplayName("Google falls back to given_name and family_name when name is missing or JSON null")]
        [InlineData("""{"sub":"1","given_name":"Ada","family_name":"Lovelace"}""", "Ada Lovelace")]
        [InlineData("""{"sub":"1","name":null,"given_name":"Ada","family_name":"Lovelace"}""", "Ada Lovelace")]
        [InlineData("""{"sub":"1","given_name":"Ada"}""", "Ada")]
        [InlineData("""{"sub":"1","name":"Ada L.","given_name":"Ada","family_name":"Lovelace"}""", "Ada L.")]
        public void Google_ParseUserJson_MissingName_FallsBackToNameParts(string json, string expectedName)
        {
            Assert.Equal(expectedName, new GoogleOAuth2Provider(new GoogleOAuth2Options(), null).ParseUserJson(json, null).UserName);
        }

        [Fact]
        [DisplayName("Facebook maps id, name and email")]
        public void Facebook_ParseUserJson_MapsIdNameAndEmail()
        {
            const string json = """{"id":"1234567890","name":"Ada Lovelace","email":"ada@example.com"}""";

            var user = new FacebookOAuth2Provider(new FacebookOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("1234567890", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Facebook falls back to first_name and last_name when name is missing")]
        public void Facebook_ParseUserJson_MissingName_FallsBackToNameParts()
        {
            var user = new FacebookOAuth2Provider(new FacebookOAuth2Options(), null).ParseUserJson("""{"id":"1","first_name":"Ada","last_name":"Lovelace"}""", null);

            Assert.Equal("Ada Lovelace", user.UserName);
        }

        [Fact]
        [DisplayName("A numeric identifier maps to its digits")]
        public void Facebook_ParseUserJson_NumericId_MapsToDigits()
        {
            var user = new FacebookOAuth2Provider(new FacebookOAuth2Options(), null).ParseUserJson("""{"id":1234567890,"name":"Ada"}""", null);

            Assert.Equal("1234567890", user.UserId);
        }

        [Fact]
        [DisplayName("LINE maps userId and displayName, and has no email address without an ID token")]
        public void Line_ParseUserJson_MapsUserIdAndDisplayName()
        {
            const string json = """{"userId":"U4af4980629a1b2c3d4e5f60718293a4b","displayName":"Ada","pictureUrl":"https://example.com/a.png"}""";

            var user = new LineOAuth2Provider(new LineOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("U4af4980629a1b2c3d4e5f60718293a4b", user.UserId);
            Assert.Equal("Ada", user.UserName);
            Assert.Null(user.Email);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID maps sub, name and email")]
        public void Azure_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ","name":"Ada Lovelace","family_name":"Lovelace","given_name":"Ada","email":"ada@contoso.com"}""";

            var user = new AzureOAuth2Provider(new AzureOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@contoso.com", user.Email);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID builds the name from givenname and familyname for a personal Microsoft account")]
        public void Azure_ParseUserJson_PersonalAccountWithoutName_JoinsNameParts()
        {
            const string json = """{"sub":"AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ","@odata.context":"https://substrate.office.com/profileB2/v2.0/me/$metadata#userinfo","givenname":"Ada","familyname":"Lovelace","email":"ada@outlook.com","locale":"en-GB"}""";

            var user = new AzureOAuth2Provider(new AzureOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@outlook.com", user.Email);
        }

        [Theory]
        [DisplayName("Microsoft Entra ID falls back to the name parts when name is missing or JSON null")]
        [InlineData("""{"sub":"1","given_name":"Ada","family_name":"Lovelace"}""", "Ada Lovelace")]
        [InlineData("""{"sub":"1","name":null,"givenname":"Ada","familyname":"Lovelace"}""", "Ada Lovelace")]
        [InlineData("""{"sub":"1","givenname":"Ada"}""", "Ada")]
        [InlineData("""{"sub":"1","familyname":"Lovelace"}""", "Lovelace")]
        [InlineData("""{"sub":"1","givenname":"","familyname":"Lovelace"}""", "Lovelace")]
        public void Azure_ParseUserJson_MissingName_FallsBackToNameParts(string json, string expected)
        {
            var user = new AzureOAuth2Provider(new AzureOAuth2Options(), null).ParseUserJson(json, null);

            Assert.Equal(expected, user.UserName);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID has no user name when neither name nor any name part is returned")]
        public void Azure_ParseUserJson_NoNameOrParts_MapsToNull()
        {
            var user = new AzureOAuth2Provider(new AzureOAuth2Options(), null).ParseUserJson("""{"sub":"1","givenname":" "}""", null);

            Assert.Null(user.UserName);
        }

        [Fact]
        [DisplayName("Auth0 maps sub, name and email")]
        public void Auth0_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"auth0|5f7c8ec7c33c6c004bbafe82","name":"Ada Lovelace","nickname":"ada","email":"ada@example.com"}""";

            var user = CreateAuth0Provider().ParseUserJson(json, null);

            Assert.Equal("auth0|5f7c8ec7c33c6c004bbafe82", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Auth0 falls back to nickname when name is missing")]
        public void Auth0_ParseUserJson_MissingName_FallsBackToNickname()
        {
            var user = CreateAuth0Provider().ParseUserJson("""{"sub":"auth0|1","nickname":"ada"}""", null);

            Assert.Equal("ada", user.UserName);
        }

        [Fact]
        [DisplayName("Auth0 prefers the given and family names to the nickname when name is missing")]
        public void Auth0_ParseUserJson_MissingName_PrefersNamePartsToNickname()
        {
            var user = CreateAuth0Provider().ParseUserJson("""{"sub":"auth0|1","given_name":"Ada","family_name":"Lovelace","nickname":"ada"}""", null);

            Assert.Equal("Ada Lovelace", user.UserName);
        }

        [Fact]
        [DisplayName("Auth0 falls back to nickname when name is JSON null")]
        public void Auth0_ParseUserJson_NullName_FallsBackToNickname()
        {
            var user = CreateAuth0Provider().ParseUserJson("""{"sub":"auth0|1","name":null,"nickname":"ada"}""", null);

            Assert.Equal("ada", user.UserName);
        }

        [Fact]
        [DisplayName("Okta maps sub, name and email")]
        public void Okta_ParseUserJson_MapsSubNameAndEmail()
        {
            const string json = """{"sub":"00uid4BxXw6I6TV4m0g3","name":"Ada Lovelace","preferred_username":"ada@example.com","email":"ada@example.com"}""";

            var user = CreateOktaProvider().ParseUserJson(json, null);

            Assert.Equal("00uid4BxXw6I6TV4m0g3", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Fact]
        [DisplayName("Okta falls back to preferred_username when name is missing")]
        public void Okta_ParseUserJson_MissingName_FallsBackToPreferredUsername()
        {
            var user = CreateOktaProvider().ParseUserJson("""{"sub":"00u1","preferred_username":"ada@example.com"}""", null);

            Assert.Equal("ada@example.com", user.UserName);
        }

        [Fact]
        [DisplayName("Okta prefers the given and family names to preferred_username when name is missing")]
        public void Okta_ParseUserJson_MissingName_PrefersNamePartsToPreferredUsername()
        {
            var user = CreateOktaProvider().ParseUserJson("""{"sub":"00u1","given_name":"Ada","family_name":"Lovelace","preferred_username":"ada@example.com"}""", null);

            Assert.Equal("Ada Lovelace", user.UserName);
        }

        [Fact]
        [DisplayName("ParseUserJson rejects a null response with ArgumentNullException")]
        public void ParseUserJson_NullJson_ThrowsArgumentNullException()
        {
            var provider = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null);

            Assert.Throws<ArgumentNullException>(() => provider.ParseUserJson(null!, null));
        }

        [Fact]
        [DisplayName("ParseUserJson rejects an empty response with ArgumentException, not ArgumentNullException")]
        public void ParseUserJson_EmptyJson_ThrowsArgumentException()
        {
            var provider = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null);

            var exception = Assert.Throws<ArgumentException>(() => provider.ParseUserJson(string.Empty, null));
            Assert.Equal("json", exception.ParamName);
        }

        [Theory]
        [DisplayName("ParseUserJson rejects a response that is not a JSON object")]
        [InlineData("{")]
        [InlineData("[]")]
        public void ParseUserJson_NotJsonObject_ThrowsJsonException(string json)
        {
            var provider = new GoogleOAuth2Provider(new GoogleOAuth2Options(), null);

            Assert.ThrowsAny<System.Text.Json.JsonException>(() => provider.ParseUserJson(json, null));
        }

        private static Auth0OAuth2Provider CreateAuth0Provider()
        {
            return new Auth0OAuth2Provider(new Auth0OAuth2Options { Domain = "tenant.auth0.com" }, null);
        }

        private static OktaOAuth2Provider CreateOktaProvider()
        {
            return new OktaOAuth2Provider(new OktaOAuth2Options { Domain = "dev-123456.okta.com" }, null);
        }
    }
}
