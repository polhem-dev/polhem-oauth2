using System.ComponentModel;
using System.Net;

namespace Polhem.OAuth2.UnitTests
{
    // The dedicated OpenID Connect providers. The generic provider is covered by OidcOAuth2ProviderTests.
    public class OpenIdConnectProviderTests
    {
        private const string ClientId = "client-id";
        private const string RedirectUri = "https://app.example.com/auth/callback";

        public static TheoryData<string, string> ProvidersAndIssuers => new()
        {
            { "Google", "https://accounts.google.com" },
            { "Auth0", "https://tenant.auth0.com/" },
            { "Okta", "https://dev-123456.okta.com/oauth2/default" },
            { "Azure", "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0" }
        };

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("The user information is returned when its subject equals that of the ID token")]
        public async Task GetUserInfoAsync_SubjectMatchesIdToken_ReturnsUser(string providerName, string issuer)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1","name":"Ada","email":"ada@example.com"}""");
            var provider = CreateProvider(providerName, handler);

            var user = await provider.GetUserInfoAsync(Token(IdToken(issuer, ClientId, "u1")), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
            Assert.Equal("Ada", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
        }

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("User information for another subject than the ID token, or without one, fails with OAuth2Exception")]
        public async Task GetUserInfoAsync_SubjectDiffersFromIdToken_ThrowsOAuth2Exception(string providerName, string issuer)
        {
            foreach (string json in new[] { """{"sub":"u2"}""", """{"name":"Mallory"}""", """{"sub":"U1"}""" })
            {
                var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, json);
                var provider = CreateProvider(providerName, handler);

                var exception = await Assert.ThrowsAsync<OAuth2Exception>(
                    () => provider.GetUserInfoAsync(Token(IdToken(issuer, ClientId, "u1")), CancellationToken.None));

                Assert.Contains("another subject", exception.Message, StringComparison.Ordinal);
            }
        }

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("Without an ID token, the user information is returned unchecked, as before")]
        public async Task GetUserInfoAsync_NoIdToken_ReturnsUser(string providerName, string issuer)
        {
            _ = issuer;
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u2","name":"Ada"}""");
            var provider = CreateProvider(providerName, handler);

            var user = await provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None);

            Assert.Equal("u2", user.UserId);
            Assert.Single(handler.Requests);
        }

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("A malformed ID token, or one without a subject, fails with OAuth2Exception before the user information is requested")]
        public async Task GetUserInfoAsync_MalformedIdToken_ThrowsBeforeRequest(string providerName, string issuer)
        {
            string[] idTokens =
            {
                "not-a-token",
                "a.b.c.d.e",
                "eyJhbGciOiJSUzI1NiJ9.!!!.signature",
                TestIdToken.Create("[]"),
                TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":"client-id"}"""),
                TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":"client-id","sub":""}"""),
                TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":"client-id","sub":1}""")
            };
            foreach (string idToken in idTokens)
            {
                var handler = new StubHttpMessageHandler();
                var provider = CreateProvider(providerName, handler);

                await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(idToken), CancellationToken.None));
                Assert.Empty(handler.Requests);
            }
        }

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("An ID token for another client fails with OAuth2Exception before the user information is requested")]
        public async Task GetUserInfoAsync_IdTokenForOtherClient_ThrowsBeforeRequest(string providerName, string issuer)
        {
            foreach (string idToken in new[]
            {
                IdToken(issuer, "other", "u1"),
                TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":["other","{{ClientId}}"],"sub":"u1"}"""),
                TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":"{{ClientId}}","azp":"other","sub":"u1"}""")
            })
            {
                var handler = new StubHttpMessageHandler();
                var provider = CreateProvider(providerName, handler);

                var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(idToken), CancellationToken.None));

                Assert.Contains("another client", exception.Message, StringComparison.Ordinal);
                Assert.Empty(handler.Requests);
            }
        }

        [Theory]
        [DisplayName("Google and Auth0 accept only their own issuer in the ID token")]
        [InlineData("Google", "https://accounts.google.com", true)]
        [InlineData("Google", "accounts.google.com", true)]
        [InlineData("Google", "https://accounts.google.com/", false)]
        [InlineData("Google", "https://evil.example.com", false)]
        [InlineData("Auth0", "https://tenant.auth0.com/", true)]
        [InlineData("Auth0", "https://tenant.auth0.com", false)]
        [InlineData("Auth0", "https://other.auth0.com/", false)]
        [InlineData("Auth0", null, false)]
        public async Task GetUserInfoAsync_FixedIssuer_IsChecked(string providerName, string? issuer, bool accepted)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1"}""");
            var provider = CreateProvider(providerName, handler);
            string idToken = issuer is null
                ? TestIdToken.Create("""{"aud":"client-id","sub":"u1"}""")
                : IdToken(issuer, ClientId, "u1");

            if (accepted)
            {
                Assert.Equal("u1", (await provider.GetUserInfoAsync(Token(idToken), CancellationToken.None)).UserId);
                return;
            }
            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(Token(idToken), CancellationToken.None));
            Assert.Contains("another issuer", exception.Message, StringComparison.Ordinal);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("Auth0 expects the issuer of a custom domain set as Domain")]
        public async Task GetUserInfoAsync_Auth0CustomDomain_ExpectsCustomIssuer()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1"}""");
            var options = new Auth0OAuth2Options { Domain = "https://login.example.com/", ClientId = ClientId, RedirectUri = RedirectUri };
            var provider = OAuth2Provider.Create(options, handler.CreateClient());

            var user = await provider.GetUserInfoAsync(Token(IdToken("https://login.example.com/", ClientId, "u1")), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
        }

        [Theory]
        [DisplayName("Auth0 with endpoints set by hand, without Domain, expects the host of the authorization or the token endpoint")]
        [InlineData("https://login.example.com/", true)]
        [InlineData("https://tenant.auth0.com/", true)]
        [InlineData("https://other.auth0.com/", false)]
        public async Task GetUserInfoAsync_Auth0EndpointsWithoutDomain_ExpectsEndpointHost(string issuer, bool accepted)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1"}""");
            var options = new Auth0OAuth2Options
            {
                AuthorizationEndpoint = "https://login.example.com/authorize",
                TokenEndpoint = "https://tenant.auth0.com/oauth/token",
                UserInfoEndpoint = "https://tenant.auth0.com/userinfo",
                ClientId = ClientId,
                RedirectUri = RedirectUri
            };
            var provider = OAuth2Provider.Create(options, handler.CreateClient());
            var call = provider.GetUserInfoAsync(Token(IdToken(issuer, ClientId, "u1")), CancellationToken.None);

            if (accepted)
                Assert.Equal("u1", (await call).UserId);
            else
                await Assert.ThrowsAsync<OAuth2Exception>(() => call);
        }

        [Theory]
        [DisplayName("Okta and Microsoft Entra ID accept any issuer, because it is not fixed by the options")]
        [InlineData("Okta", "https://login.example.com/oauth2/default")]
        [InlineData("Okta", "https://dev-123456.okta.com")]
        [InlineData("Azure", "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0")]
        [InlineData("Azure", null)]
        public async Task GetUserInfoAsync_IssuerNotFixed_IsNotChecked(string providerName, string? issuer)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"sub":"u1"}""");
            var provider = CreateProvider(providerName, handler);
            string idToken = issuer is null
                ? TestIdToken.Create("""{"aud":"client-id","sub":"u1"}""")
                : IdToken(issuer, ClientId, "u1");

            var user = await provider.GetUserInfoAsync(Token(idToken), CancellationToken.None);

            Assert.Equal("u1", user.UserId);
        }

        [Theory]
        [MemberData(nameof(ProvidersAndIssuers))]
        [DisplayName("A sign-in whose user information names another subject than the ID token returns a failed result")]
        public async Task CompleteAuthorizationAsync_SubjectDiffers_ReturnsFailedResult(string providerName, string issuer)
        {
            string tokenJson = $$"""{"access_token":"access","token_type":"Bearer","id_token":"{{IdToken(issuer, ClientId, "u1")}}"}""";
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, tokenJson)
                .Respond(HttpStatusCode.OK, """{"sub":"u2"}""");
            var client = new OAuth2Client(TestOptions.Create(providerName, RedirectUri, "secret"), handler.CreateClient());
            var kept = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code", kept.State, null, null), kept);

            Assert.False(result.IsSuccess);
            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        private static OAuth2Provider CreateProvider(string providerName, StubHttpMessageHandler handler)
        {
            return OAuth2Provider.Create(TestOptions.Create(providerName, RedirectUri), handler.CreateClient());
        }

        private static string IdToken(string issuer, string audience, string subject)
        {
            return TestIdToken.Create($$"""{"iss":"{{issuer}}","aud":"{{audience}}","sub":"{{subject}}"}""");
        }

        private static TokenResponse Token(string idToken)
        {
            return new TokenResponse("access", idToken: idToken);
        }
    }
}
