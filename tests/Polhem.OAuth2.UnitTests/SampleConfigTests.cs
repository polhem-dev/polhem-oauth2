using System.ComponentModel;
using OAuthSamples;

namespace Polhem.OAuth2.UnitTests
{
    // The README describes how the samples read OAuthConfig.json. These tests hold the loader to it.
    public class SampleConfigTests
    {
        [Theory]
        [DisplayName("The sample settings refuse a client secret in an iOS or Android section, which an application cannot keep")]
        [InlineData("iOS")]
        [InlineData("Android")]
        public void Parse_SecretInAppSection_ThrowsInvalidDataException(string section)
        {
            string json = "{\"Providers\":{\"Google\":{\"" + section + "\":{\"ClientId\":\"id\",\"ClientSecret\":\"secret\",\"RedirectUri\":\"com.example.app:/oauth2redirect\"}}}}";

            var exception = Assert.Throws<InvalidDataException>(() => OAuthConfig.Parse(json, "test.json"));
            Assert.Contains("ClientSecret", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The sample settings skip a section whose ClientId is still empty, so the samples offer only the providers filled in")]
        public void GetClients_EmptyClientId_SkipsProvider()
        {
            const string json = """
                {"Providers":{
                  "Google":{"Desktop":{"ClientId":"","RedirectUri":"http://127.0.0.1:0/callback"}},
                  "Line":{"Desktop":{"ClientId":"line-id","RedirectUri":"http://127.0.0.1:0/callback"}}}}
                """;

            var clients = OAuthConfig.Parse(json, "test.json").GetClients(OAuthClientType.Desktop);

            var client = Assert.Single(clients);
            Assert.Equal("line-id", client.Options.ClientId);
        }

        [Fact]
        [DisplayName("The sample settings read a client authentication method written by name")]
        public void Parse_ClientAuthenticationByName_ReadsEnumValue()
        {
            const string json = """{"Providers":{"Okta":{"Domain":"dev-1.okta.com","Web":{"ClientId":"id","ClientSecret":"s","RedirectUri":"https://app.example.com/auth/callback","ClientAuthentication":"ClientSecretBasic"}}}}""";

            var options = OAuthConfig.Parse(json, "test.json").GetClient("Okta", OAuthClientType.Web);

            Assert.Equal(ClientAuthenticationMethod.ClientSecretBasic, options.ClientAuthentication);
        }

        [Fact]
        [DisplayName("The sample settings read the Sign in with Apple key from the file that PrivateKeyPath names, relative to the settings file")]
        public void Parse_ApplePrivateKeyPath_ReadsKeyFile()
        {
            string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "AuthKey_TEST.p8"), "key text");
                const string json = """{"Providers":{"Apple":{"Web":{"ClientId":"com.example.web","TeamId":"T","KeyId":"K","PrivateKeyPath":"AuthKey_TEST.p8","RedirectUri":"https://app.example.com/auth/callback"}}}}""";

                var options = (AppleOAuth2Options)OAuthConfig.Parse(json, Path.Combine(directory, "OAuthConfig.json")).GetClient("Apple", OAuthClientType.Web);

                Assert.Equal("key text", options.PrivateKey);
                Assert.Equal("K", options.KeyId);
                Assert.Equal("T", options.TeamId);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [DisplayName("The sample settings refuse a Sign in with Apple key written into the file, and a key file that does not exist")]
        [InlineData("\"PrivateKey\":\"-----BEGIN PRIVATE KEY-----\"", "PrivateKeyPath")]
        [InlineData("\"PrivateKeyPath\":\"missing.p8\"", "does not exist")]
        public void Parse_ApplePrivateKeyInline_ThrowsInvalidDataException(string field, string expectedMessage)
        {
            string json = "{\"Providers\":{\"Apple\":{\"Web\":{\"ClientId\":\"id\"," + field + "}}}}";

            var exception = Assert.Throws<InvalidDataException>(() => OAuthConfig.Parse(json, Path.Combine(Path.GetTempPath(), "OAuthConfig.json")));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("BEGIN", exception.Message, StringComparison.Ordinal);
        }
    }
}
