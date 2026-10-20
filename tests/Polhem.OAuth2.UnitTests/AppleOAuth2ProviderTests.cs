using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class AppleOAuth2ProviderTests
    {
        private const string ClientId = "com.example.web";
        private const string TeamId = "TEAM123456";
        private const string KeyId = "KEY1234567";
        private const string RedirectUri = "https://app.example.com/auth/callback";

        private static readonly string[] s_defaultScopes = ["name", "email"];

        [Fact]
        [DisplayName("The options ask for the name and email scopes and have the Apple endpoints and no user information endpoint")]
        public void Options_Defaults_UseAppleEndpoints()
        {
            var options = new AppleOAuth2Options();

            Assert.Equal(s_defaultScopes, options.Scopes);
            Assert.Equal("https://appleid.apple.com/auth/authorize", options.AuthorizationEndpoint);
            Assert.Equal("https://appleid.apple.com/auth/token", options.TokenEndpoint);
            Assert.Empty(options.UserInfoEndpoint);
            Assert.Empty(options.TeamId);
            Assert.Empty(options.KeyId);
            Assert.Empty(options.PrivateKey);
        }

        [Fact]
        [DisplayName("The client accepts a private key with its key ID and team ID, or a client secret signed in advance")]
        public void Client_PrivateKeyOrClientSecret_IsCreated()
        {
            using var key = new TestAppleKey();

            Assert.Equal("Apple", new OAuth2Client(CreateOptions(key)).ProviderName);
            Assert.Equal("Apple", new OAuth2Client(CreateOptions(privateKey: null, clientSecret: "signed")).ProviderName);
        }

        [Theory]
        [DisplayName("The client rejects options without a private key or client secret, a private key without its key ID or team ID, or a user information endpoint")]
        [InlineData(false, "", "", "", "", "PrivateKey")]
        [InlineData(true, " ", TeamId, "", "", nameof(AppleOAuth2Options.KeyId))]
        [InlineData(true, KeyId, "", "", "", nameof(AppleOAuth2Options.TeamId))]
        [InlineData(true, KeyId, TeamId, "", "https://appleid.apple.com/userinfo", nameof(OAuth2Options.UserInfoEndpoint))]
        [InlineData(false, "", "", "signed", "https://appleid.apple.com/userinfo", nameof(OAuth2Options.UserInfoEndpoint))]
        public void Client_InvalidSettings_ThrowsArgumentException(
            bool withKey, string keyId, string teamId, string clientSecret, string userInfoEndpoint, string expectedProperty)
        {
            using var key = new TestAppleKey();
            var options = CreateOptions(withKey ? key : null, clientSecret);
            options.KeyId = keyId;
            options.TeamId = teamId;
            options.UserInfoEndpoint = userInfoEndpoint;

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            Assert.Contains(expectedProperty, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The client rejects a key ID or team ID with leading or trailing white space, as read from a secret store")]
        [InlineData(KeyId + "\n", TeamId, nameof(AppleOAuth2Options.KeyId))]
        [InlineData(KeyId, " " + TeamId, nameof(AppleOAuth2Options.TeamId))]
        public void Client_IdentifierWithWhiteSpace_ThrowsArgumentException(string keyId, string teamId, string expectedProperty)
        {
            using var key = new TestAppleKey();
            var options = CreateOptions(key);
            options.KeyId = keyId;
            options.TeamId = teamId;

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            Assert.StartsWith(expectedProperty, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The client accepts a null private key when a client secret is set")]
        public void Client_NullPrivateKeyWithClientSecret_IsCreated()
        {
            var options = CreateOptions(privateKey: null, clientSecret: "signed");
            options.PrivateKey = null!;

            Assert.Equal("Apple", new OAuth2Client(options).ProviderName);
        }

        [Fact]
        [DisplayName("The client rejects a key file whose private key does not match its public key")]
        public void Client_MismatchedKeyPair_ThrowsArgumentException()
        {
            using var key = new TestAppleKey();
            using var other = new TestAppleKey();
            var options = CreateOptions(key);
            options.PrivateKey = key.PemWithPublicKeyOf(other);

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            // Some platforms refuse the pair when it is imported, and the others when the signature check fails.
            Assert.StartsWith(nameof(AppleOAuth2Options.PrivateKey), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The client rejects client_secret_basic, which Apple does not accept")]
        public void Client_ClientSecretBasic_ThrowsArgumentException()
        {
            var options = CreateOptions(privateKey: null, clientSecret: "signed");
            options.ClientAuthentication = ClientAuthenticationMethod.ClientSecretBasic;

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            Assert.Contains(nameof(OAuth2Options.ClientAuthentication), exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The client rejects a private key that is not the PKCS#8 P-256 key of a .p8 file, without quoting the key")]
        [InlineData("not a key")]
        [InlineData("-----BEGIN PRIVATE KEY-----\n%%%%\n-----END PRIVATE KEY-----")]
        [InlineData("-----BEGIN PRIVATE KEY-----\nMAA=\n-----END PRIVATE KEY-----")]
        [InlineData("-----BEGIN EC PRIVATE KEY-----\nMHcCAQEEIA==\n-----END EC PRIVATE KEY-----")]
        public void Client_InvalidPrivateKey_ThrowsArgumentException(string privateKey)
        {
            var options = CreateOptions(privateKey: null);
            options.PrivateKey = privateKey;
            options.KeyId = KeyId;
            options.TeamId = TeamId;

            var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

            Assert.StartsWith(nameof(AppleOAuth2Options.PrivateKey), exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(privateKey, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The client rejects a key for another curve and a key without its public key, without quoting the key")]
        public void Client_KeyOfOtherCurveOrWithoutPublicKey_ThrowsArgumentException()
        {
            using var key = new TestAppleKey();
            foreach (string pem in new[] { key.PemWithOtherCurve, key.PemWithoutPublicKey })
            {
                var options = CreateOptions(key);
                options.PrivateKey = pem;

                var exception = Assert.Throws<ArgumentException>(() => new OAuth2Client(options));

                string body = pem.Split('\n')[1];
                Assert.DoesNotContain(body.Substring(0, 20), exception.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        [DisplayName("The loopback and application clients refuse the Apple options and point to the back end")]
        public void PublicClients_AppleOptions_ThrowArgumentException()
        {
            using var key = new TestAppleKey();
            var loopbackOptions = CreateOptions(key);
            loopbackOptions.RedirectUri = "http://127.0.0.1:0/callback";
            var appOptions = CreateOptions(key);
            appOptions.RedirectUri = "com.example.app:/oauth2redirect";

            var loopback = Assert.Throws<ArgumentException>(() => new LoopbackOAuth2Client(loopbackOptions));
            var app = Assert.Throws<ArgumentException>(() => new AppOAuth2Client(appOptions, (_, _, _) => Task.FromResult(new Uri(RedirectUri))));

            Assert.Contains("back end", loopback.Message, StringComparison.Ordinal);
            Assert.Contains("back end", app.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The authorization URL asks for form_post only when the name or email scope is requested, and keeps PKCE")]
        [InlineData(new[] { "name", "email" }, true)]
        [InlineData(new[] { "email" }, true)]
        [InlineData(new[] { "openid name" }, true)]
        [InlineData(new[] { "openid" }, false)]
        [InlineData(new string[0], false)]
        public void CreateAuthorizationRequest_Scopes_SetResponseMode(string[] scopes, bool formPost)
        {
            using var key = new TestAppleKey();
            var options = CreateOptions(key);
            options.Scopes = scopes;
            var client = new OAuth2Client(options);

            string url = client.CreateAuthorizationRequest().Url;

            Assert.Equal(formPost, client.UsesFormPostResponse);
            Assert.Equal(formPost ? "form_post" : null, LoopbackTestHttp.GetParameter(new Uri(url).Query.TrimStart('?'), "response_mode"));
            Assert.Equal("S256", LoopbackTestHttp.GetParameter(new Uri(url).Query.TrimStart('?'), "code_challenge_method"));
        }

        [Fact]
        [DisplayName("Other providers return to the redirect URI with a GET redirect")]
        public void UsesFormPostResponse_OtherProvider_IsFalse()
        {
            var options = new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = RedirectUri };

            Assert.False(new OAuth2Client(options).UsesFormPostResponse);
        }

        [Fact]
        [DisplayName("A sign-in sends an ES256 client secret for the client, and returns the user of the ID token with the name of the user parameter")]
        public async Task CompleteAuthorizationAsync_FirstSignIn_SignsSecretAndReadsUser()
        {
            using var key = new TestAppleKey();
            const string payload = """{"iss":"https://appleid.apple.com","aud":"com.example.web","sub":"001234.abcd","email":"x@privaterelay.appleid.com","email_verified":true,"is_private_email":true}""";
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, TokenJson(payload));
            var client = new OAuth2Client(CreateOptions(key), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;
            DateTimeOffset before = DateTimeOffset.UtcNow;

            var result = await client.CompleteAuthorizationAsync(
                new AuthorizationCallback("code-value", pending.State, null, null, """{"name":{"firstName":"Ada","lastName":"Lovelace"},"email":"other@example.com"}"""),
                pending);

            Assert.True(result.IsSuccess, result.Exception?.ToString());
            Assert.Equal("Apple", result.ProviderName);
            Assert.Equal("001234.abcd", result.UserInfo!.UserId);
            Assert.Equal("Ada Lovelace", result.UserInfo.UserName);
            Assert.Equal("x@privaterelay.appleid.com", result.UserInfo.Email);
            Assert.Equal(payload, result.UserInfo.RawJson);

            var request = Assert.Single(handler.Requests);
            Assert.Null(request.Authorization);
            Assert.Equal(ClientId, request.FormValue("client_id"));
            Assert.Equal(pending.CodeVerifier, request.FormValue("code_verifier"));
            string secret = request.FormValue("client_secret")!;
            AssertClientSecret(secret, key.Key, before);
        }

        [Fact]
        [DisplayName("A later sign-in without the user parameter has no user name")]
        public async Task CompleteAuthorizationAsync_NoUserParameter_HasNoName()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, TokenJson(IdTokenPayload("u1")));
            var client = new OAuth2Client(CreateOptions(privateKey: null, clientSecret: "signed"), handler.CreateClient());
            var pending = client.CreateAuthorizationRequest().Pending;

            var result = await client.CompleteAuthorizationAsync(new AuthorizationCallback("code-value", pending.State, null, null), pending);

            Assert.True(result.IsSuccess, result.Exception?.ToString());
            Assert.Equal("u1", result.UserInfo!.UserId);
            Assert.Null(result.UserInfo.UserName);
            Assert.Equal("signed", Assert.Single(handler.Requests).FormValue("client_secret"));
        }

        [Theory]
        [DisplayName("A user parameter that is not a JSON object with name parts, or is too long, leaves the user name empty")]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("""{"name":"Ada Lovelace"}""")]
        [InlineData("""{"name":{"firstName":" ","lastName":""}}""")]
        [InlineData("""{"name":{"firstName":1,"lastName":true}}""")]
        [InlineData("""{"email":"ada@example.com"}""")]
        public void AddCallbackDetails_InvalidUserParameter_LeavesNameEmpty(string user)
        {
            var provider = CreateProvider(CreateOptions(privateKey: null, clientSecret: "signed"));

            var result = provider.AddCallbackDetails(new UserInfo("u1", null, null, "{}"), new AuthorizationCallback("c", "s", null, null, user));

            Assert.Null(result.UserName);
        }

        [Fact]
        [DisplayName("A user parameter longer than 4096 characters is ignored, and a single name part is used alone")]
        public void AddCallbackDetails_LongOrPartialName_IsHandled()
        {
            var provider = CreateProvider(CreateOptions(privateKey: null, clientSecret: "signed"));
            var user = new UserInfo("u1", null, null, "{}");
            string tooLong = "{\"name\":{\"firstName\":\"Ada\",\"lastName\":\"Lovelace\"},\"pad\":\"" + new string('x', 4096) + "\"}";

            Assert.Null(provider.AddCallbackDetails(user, new AuthorizationCallback("c", "s", null, null, tooLong)).UserName);
            Assert.Equal("Ada", provider.AddCallbackDetails(user, new AuthorizationCallback("c", "s", null, null, """{"name":{"firstName":"Ada"}}""")).UserName);
        }

        [Fact]
        [DisplayName("A token response without an ID token fails with OAuth2Exception")]
        public async Task GetUserInfoAsync_NoIdToken_ThrowsOAuth2Exception()
        {
            var provider = CreateProvider(CreateOptions(privateKey: null, clientSecret: "signed"));

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => provider.GetUserInfoAsync(new TokenResponse("access"), CancellationToken.None));

            Assert.Contains("no ID token", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("An ID token for another client, from another issuer or without a subject fails with OAuth2Exception")]
        [InlineData("""{"iss":"https://appleid.apple.com","aud":"com.example.other","sub":"u1"}""")]
        [InlineData("""{"iss":"https://appleid.apple.com/","aud":"com.example.web","sub":"u1"}""")]
        [InlineData("""{"aud":"com.example.web","sub":"u1"}""")]
        [InlineData("""{"iss":"https://appleid.apple.com","aud":"com.example.web"}""")]
        [InlineData("""{"iss":"https://appleid.apple.com","aud":["com.example.web","com.example.app"],"sub":"u1"}""")]
        public async Task GetUserInfoAsync_InvalidIdToken_ThrowsOAuth2Exception(string payload)
        {
            var provider = CreateProvider(CreateOptions(privateKey: null, clientSecret: "signed"));

            await Assert.ThrowsAsync<OAuth2Exception>(
                () => provider.GetUserInfoAsync(new TokenResponse("access", "Bearer", null, null, TestIdToken.Create(payload), null, "{}"), CancellationToken.None));
        }

        [Fact]
        [DisplayName("The private key takes precedence over a client secret, and the signed secret is kept until shortly before it expires")]
        public async Task ExchangeCodeAsync_SignedSecret_IsReusedAndRenewed()
        {
            using var key = new TestAppleKey();
            var handler = new StubHttpMessageHandler();
            for (int i = 0; i < 4; i++)
                handler.Respond(HttpStatusCode.OK, TokenJson(IdTokenPayload("u1")));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var provider = new AppleOAuth2Provider(CreateOptions(key, clientSecret: "ignored"), OAuth2Provider.HttpClientFactoryFor(handler.CreateClient()), () => now);

            await provider.ExchangeCodeAsync("c", RedirectUri, null, publicClient: false, CancellationToken.None);
            now += AppleClientSecret.Lifetime - AppleClientSecret.RenewalMargin - TimeSpan.FromSeconds(1);
            await provider.RefreshTokenAsync("r", publicClient: false, CancellationToken.None);
            now += TimeSpan.FromSeconds(2);
            await provider.RefreshTokenAsync("r", publicClient: false, CancellationToken.None);
            now -= TimeSpan.FromHours(2);
            await provider.RefreshTokenAsync("r", publicClient: false, CancellationToken.None);

            string?[] secrets = handler.Requests.Select(request => request.FormValue("client_secret")).ToArray();
            Assert.DoesNotContain("ignored", secrets);
            Assert.Equal(secrets[0], secrets[1]);
            Assert.NotEqual(secrets[1], secrets[2]);
            Assert.NotEqual(secrets[2], secrets[3]);
            Assert.All(secrets, secret => AssertSignature(secret!, key.Key));
        }

        [Fact]
        [DisplayName("The user information of an access token from a front end is not supported")]
        public async Task GetUserInfoAsync_AccessToken_ThrowsNotSupportedException()
        {
            var client = new OAuth2Client(CreateOptions(privateKey: null, clientSecret: "signed"));

            await Assert.ThrowsAsync<NotSupportedException>(() => client.GetUserInfoAsync("access"));
        }

        private static AppleOAuth2Options CreateOptions(TestAppleKey? privateKey, string clientSecret = "")
        {
            var options = new AppleOAuth2Options { ClientId = ClientId, RedirectUri = RedirectUri, ClientSecret = clientSecret };
            if (privateKey is not null)
            {
                options.PrivateKey = privateKey.Pem;
                options.KeyId = KeyId;
                options.TeamId = TeamId;
            }
            return options;
        }

        private static AppleOAuth2Provider CreateProvider(AppleOAuth2Options options)
        {
            return new AppleOAuth2Provider(options, OAuth2Provider.HttpClientFactoryFor(new StubHttpMessageHandler().CreateClient()));
        }

        private static string IdTokenPayload(string subject)
        {
            return $$"""{"iss":"https://appleid.apple.com","aud":"{{ClientId}}","sub":"{{subject}}","email":"ada@example.com"}""";
        }

        private static string TokenJson(string idTokenPayload)
        {
            return $$"""{"access_token":"access","token_type":"Bearer","expires_in":3600,"refresh_token":"refresh","id_token":"{{TestIdToken.Create(idTokenPayload)}}"}""";
        }

        private static void AssertClientSecret(string secret, ECDsa key, DateTimeOffset notBefore)
        {
            string[] segments = secret.Split('.');
            Assert.Equal(3, segments.Length);
            using (var header = JsonDocument.Parse(Base64Url.Decode(segments[0])))
            {
                Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
                Assert.Equal(KeyId, header.RootElement.GetProperty("kid").GetString());
            }
            using (var payload = JsonDocument.Parse(Base64Url.Decode(segments[1])))
            {
                var claims = payload.RootElement;
                Assert.Equal(TeamId, claims.GetProperty("iss").GetString());
                Assert.Equal(ClientId, claims.GetProperty("sub").GetString());
                Assert.Equal("https://appleid.apple.com", claims.GetProperty("aud").GetString());
                long issuedAt = claims.GetProperty("iat").GetInt64();
                Assert.InRange(issuedAt, notBefore.ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Assert.Equal(issuedAt + (long)AppleClientSecret.Lifetime.TotalSeconds, claims.GetProperty("exp").GetInt64());
            }
            AssertSignature(secret, key);
        }

        // RFC 7518, section 3.4: the signature is r and s of 32 bytes each, not DER, and verifies against the signing input.
        private static void AssertSignature(string secret, ECDsa key)
        {
            int lastDot = secret.LastIndexOf('.');
            byte[] signature = Base64Url.Decode(secret.Substring(lastDot + 1));
            Assert.Equal(64, signature.Length);
            Assert.True(key.VerifyData(Encoding.ASCII.GetBytes(secret.Substring(0, lastDot)), signature, HashAlgorithmName.SHA256));
        }
    }
}
