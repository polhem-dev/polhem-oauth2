using System.ComponentModel;
using System.Net;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class OidcOAuth2ProviderTests
    {
        private static readonly string[] s_defaultScopes = ["openid", "profile", "email"];

        private const string Issuer = "https://idp.example.com/realms/demo";
        private const string ClientId = "client-id";
        private const string RedirectUri = "https://app.example.com/auth/callback";

        [Fact]
        [DisplayName("The options ask for the openid, profile and email scopes and have no provider name by default")]
        public void Options_Defaults_RequestOidcScopes()
        {
            var options = new OidcOAuth2Options();

            Assert.Equal(s_defaultScopes, options.Scopes);
            Assert.Empty(options.ProviderName);
            Assert.Empty(options.Issuer);
            Assert.Empty(options.UserInfoEndpoint);
        }

        [Fact]
        [DisplayName("The client reports the provider name of the options, copied when the client is created")]
        public void Client_ProviderName_IsCopiedFromOptions()
        {
            var options = CreateOptions(withUserInfo: false);
            options.ProviderName = "Keycloak";
            var client = new OAuth2Client(options);

            options.ProviderName = "Changed";
            options.Issuer = "https://changed.example.com";

            Assert.Equal("Keycloak", client.ProviderName);
        }

        [Fact]
        [DisplayName("The client accepts an empty user information endpoint when the issuer is set")]
        public void Client_NoUserInfoEndpointWithIssuer_IsCreated()
        {
            var client = new OAuth2Client(CreateOptions(withUserInfo: false));

            Assert.Equal("Keycloak", client.ProviderName);
        }

        [Theory]
        [DisplayName("The client rejects a missing issuer or openid scope without a user information endpoint, an invalid issuer, or an empty provider name")]
        [InlineData("", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData(Issuer, "profile", "Keycloak", nameof(OAuth2Options.Scopes))]
        [InlineData(Issuer, "OpenID", "Keycloak", nameof(OAuth2Options.Scopes))]
        [InlineData("http://idp.example.com", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData("https://idp.example.com?tenant=1", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData("https://idp.example.com#x", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData("idp.example.com", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData("https://user:secret@idp.example.com", "openid", "Keycloak", nameof(OidcOAuth2Options.Issuer))]
        [InlineData(Issuer, "openid", " ", nameof(OidcOAuth2Options.ProviderName))]
        [InlineData(Issuer, "openid", "", nameof(OidcOAuth2Options.ProviderName))]
        public void Client_InvalidOidcSettings_ThrowsArgumentException(string issuer, string scope, string providerName, string property)
        {
            var options = CreateOptions(withUserInfo: false);
            options.Issuer = issuer;
            options.Scopes = new[] { scope };
            options.ProviderName = providerName;

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            Assert.StartsWith(property, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The openid scope may share an element of Scopes with other scopes, separated by spaces")]
        public void Client_OpenIdInSpaceSeparatedScope_IsCreated()
        {
            var options = CreateOptions(withUserInfo: false);
            options.Scopes = new[] { "profile openid", "email" };

            Assert.Equal("Keycloak", new OAuth2Client(options).ProviderName);
        }

        [Fact]
        [DisplayName("The client rejects an invalid issuer also when the user information endpoint is set")]
        public void Client_InvalidIssuerWithUserInfo_ThrowsArgumentException()
        {
            var options = CreateOptions(withUserInfo: true);
            options.Issuer = "http://idp.example.com";

            Assert.Throws<ArgumentException>(() => new OAuth2Client(options));
        }

        [Fact]
        [DisplayName("Without a user information endpoint, the user comes from the ID token and no request is sent for it")]
        public async Task GetUserInfoAsync_NoUserInfoEndpoint_ReadsIdToken()
        {
            const string payload = """{"iss":"https://idp.example.com/realms/demo","aud":"client-id","sub":"u1","name":"Ada Lovelace","email":"ada@example.com"}""";
            var handler = new StubHttpMessageHandler();
            var provider = CreateProvider(withUserInfo: false, handler);

            var user = await provider.GetUserInfoAsync(Token(TestIdToken.Create(payload)), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
            Assert.Equal("Ada Lovelace", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
            Assert.Equal(payload, user.RawJson);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("The user name falls back to the given and family names, then preferred_username, then nickname")]
        [InlineData("""{"given_name":"Ada","family_name":"Lovelace","preferred_username":"ada"}""", "Ada Lovelace")]
        [InlineData("""{"preferred_username":"ada","nickname":"Countess"}""", "ada")]
        [InlineData("""{"name":null,"nickname":"Countess"}""", "Countess")]
        [InlineData("""{"email":"ada@example.com"}""", null)]
        public void ParseUserJson_NameFallbacks_MapUserName(string json, string? expectedName)
        {
            var provider = CreateProvider(withUserInfo: true, new StubHttpMessageHandler());

            Assert.Equal(expectedName, provider.ParseUserJson(json, null).UserName);
        }

        [Fact]
        [DisplayName("Without a user information endpoint, a token response without an ID token fails with OAuth2Exception")]
        public async Task GetUserInfoAsync_NoUserInfoEndpointNoIdToken_ThrowsOAuth2Exception()
        {
            var provider = CreateProvider(withUserInfo: false, new StubHttpMessageHandler());

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None));

            Assert.Contains("no ID token", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("Without a user information endpoint, an ID token for another client, from another issuer or without a subject fails with OAuth2Exception")]
        [InlineData("""{"iss":"https://idp.example.com/realms/demo","aud":"other","sub":"u1"}""")]
        [InlineData("""{"iss":"https://idp.example.com/realms/other","aud":"client-id","sub":"u1"}""")]
        [InlineData("""{"aud":"client-id","sub":"u1"}""")]
        [InlineData("""{"iss":"https://idp.example.com/realms/demo","aud":"client-id"}""")]
        [InlineData("""{"iss":"https://idp.example.com/realms/demo","aud":"client-id","sub":""}""")]
        [InlineData("""{"iss":"https://idp.example.com/realms/demo","aud":"client-id","sub":1}""")]
        public async Task GetUserInfoAsync_NoUserInfoEndpointInvalidIdToken_ThrowsOAuth2Exception(string payload)
        {
            var provider = CreateProvider(withUserInfo: false, new StubHttpMessageHandler());

            await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(TestIdToken.Create(payload)), CancellationToken.None));
        }

        [Fact]
        [DisplayName("With a user information endpoint, the user comes from it, and a missing ID token is accepted")]
        public async Task GetUserInfoAsync_UserInfoEndpointNoIdToken_ReadsUserInfo()
        {
            const string json = """{"sub":"u1","preferred_username":"ada","email":"ada@example.com"}""";
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider(withUserInfo: true, handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
            Assert.Equal("ada", user.UserName);
            Assert.Equal(json, user.RawJson);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("https://idp.example.com/userinfo", request.Uri.AbsoluteUri);
            Assert.Equal("Bearer access", request.Authorization?.ToString());
        }

        [Fact]
        [DisplayName("With a user information endpoint, the subject of the user information must equal that of the ID token")]
        public async Task GetUserInfoAsync_SubjectMatchesIdToken_ReturnsUser()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1","name":"Ada"}""");
            var provider = CreateProvider(withUserInfo: true, handler);

            var user = await provider.GetUserInfoAsync(Token(IdTokenFor("u1")), CancellationToken.None);

            Assert.Equal("Ada", user.UserName);
        }

        [Theory]
        [DisplayName("With a user information endpoint, user information for another subject than the ID token fails with OAuth2Exception")]
        [InlineData("""{"sub":"u2","name":"Mallory"}""")]
        [InlineData("""{"name":"Mallory"}""")]
        [InlineData("""{"sub":"U1"}""")]
        public async Task GetUserInfoAsync_SubjectDiffersFromIdToken_ThrowsOAuth2Exception(string json)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
            var provider = CreateProvider(withUserInfo: true, handler);

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(IdTokenFor("u1")), CancellationToken.None));

            Assert.Contains("another subject", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With a user information endpoint, an ID token for another client fails before the user information is requested")]
        public async Task GetUserInfoAsync_IdTokenForOtherClient_ThrowsBeforeRequest()
        {
            var handler = new StubHttpMessageHandler();
            var provider = CreateProvider(withUserInfo: true, handler);
            string idToken = TestIdToken.Create("""{"iss":"https://idp.example.com/realms/demo","aud":"other","sub":"u1"}""");

            await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(idToken), CancellationToken.None));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("With a user information endpoint and no issuer, the issuer of the ID token is not checked")]
        public async Task GetUserInfoAsync_NoIssuer_AcceptsAnyIssuer()
        {
            var options = CreateOptions(withUserInfo: true);
            options.Issuer = string.Empty;
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1"}""");
            var provider = OAuth2Provider.Create(options, handler.CreateClient());
            string idToken = TestIdToken.Create("""{"iss":"https://other.example.com","aud":"client-id","sub":"u1"}""");

            var user = await provider.GetUserInfoAsync(Token(idToken), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
        }

        [Fact]
        [DisplayName("A sign-in whose ID token is for another client returns a failed result that holds OAuth2Exception")]
        public async Task CompleteAuthorizationAsync_IdTokenForOtherClient_ReturnsFailedResult()
        {
            string idToken = TestIdToken.Create("""{"iss":"https://idp.example.com/realms/demo","aud":"other","sub":"u1"}""");
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new Dictionary<string, string> { ["access_token"] = "access", ["id_token"] = idToken }));
            var client = new OAuth2Client(CreateOptions(withUserInfo: false), handler.CreateClient());
            var kept = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code", kept.State, null, null), kept);

            Assert.False(result.IsSuccess);
            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        [Fact]
        [DisplayName("A sign-in without a user information endpoint returns the user of the ID token")]
        public async Task CompleteAuthorizationAsync_NoUserInfoEndpoint_ReturnsUserOfIdToken()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new Dictionary<string, string> { ["access_token"] = "access", ["id_token"] = IdTokenFor("u1") }));
            var options = CreateOptions(withUserInfo: false);
            options.ProviderName = "Corporate SSO";
            var client = new OAuth2Client(options, handler.CreateClient());
            var kept = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code", kept.State, null, null), kept);

            Assert.True(result.IsSuccess, result.Exception?.ToString());
            Assert.Equal("Corporate SSO", result.ProviderName);
            Assert.Equal("u1", result.UserInfo.UserId);
            Assert.Single(handler.Requests);
        }

        private static OidcOAuth2Options CreateOptions(bool withUserInfo)
        {
            return new OidcOAuth2Options
            {
                ProviderName = "Keycloak",
                Issuer = Issuer,
                AuthorizationEndpoint = "https://idp.example.com/authorize",
                TokenEndpoint = "https://idp.example.com/token",
                UserInfoEndpoint = withUserInfo ? "https://idp.example.com/userinfo" : string.Empty,
                ClientId = ClientId,
                RedirectUri = RedirectUri
            };
        }

        private static OAuth2Provider CreateProvider(bool withUserInfo, StubHttpMessageHandler handler)
        {
            return OAuth2Provider.Create(CreateOptions(withUserInfo), handler.CreateClient());
        }

        private static string IdTokenFor(string subject)
        {
            return TestIdToken.Create($$"""{"iss":"{{Issuer}}","aud":"{{ClientId}}","sub":"{{subject}}"}""");
        }

        private static TokenResponse Token(string idToken)
        {
            return new TokenResponse("access", idToken: idToken);
        }
    }
}
