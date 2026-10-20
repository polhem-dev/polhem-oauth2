using System.ComponentModel;
using System.Net;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class OidcDiscoveryTests
    {
        private const string Authority = "https://idp.example.com/realms/demo";

        [Fact]
        [DisplayName("FromDiscoveryAsync requests the document under the authority and fills in the endpoints and the issuer")]
        public async Task FromDiscoveryAsync_ValidDocument_ReturnsOptions()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document());

            var options = await OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient());

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://idp.example.com/realms/demo/.well-known/openid-configuration", request.Uri.AbsoluteUri);
            Assert.Equal("application/json", request.Header("Accept"));
            Assert.StartsWith("Polhem.OAuth2", request.Header("User-Agent"), StringComparison.Ordinal);
            Assert.Equal(Authority, options.Issuer);
            Assert.Equal(Authority + "/protocol/openid-connect/auth", options.AuthorizationEndpoint);
            Assert.Equal(Authority + "/protocol/openid-connect/token", options.TokenEndpoint);
            Assert.Equal(Authority + "/protocol/openid-connect/userinfo", options.UserInfoEndpoint);
            Assert.Equal(ClientAuthenticationMethod.ClientSecretPost, options.ClientAuthentication);
            Assert.Empty(options.ProviderName);
            Assert.Equal(new[] { "openid", "profile", "email" }, options.Scopes);
            Assert.Empty(options.ClientId);
        }

        [Theory]
        [DisplayName("A single trailing slash on the authority or the issuer is ignored, and the issuer is kept as the document writes it")]
        [InlineData("https://tenant.auth0.com", "https://tenant.auth0.com/", "https://tenant.auth0.com/.well-known/openid-configuration")]
        [InlineData("https://tenant.auth0.com/", "https://tenant.auth0.com/", "https://tenant.auth0.com/.well-known/openid-configuration")]
        [InlineData("https://gitlab.com/", "https://gitlab.com", "https://gitlab.com/.well-known/openid-configuration")]
        [InlineData("https://gitlab.com", "https://gitlab.com", "https://gitlab.com/.well-known/openid-configuration")]
        public async Task FromDiscoveryAsync_TrailingSlash_MatchesIssuer(string authority, string issuer, string documentUrl)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => fields["issuer"] = issuer));

            var options = await OidcOAuth2Options.FromDiscoveryAsync(authority, handler.CreateClient());

            Assert.Equal(documentUrl, Assert.Single(handler.Requests).Uri.AbsoluteUri);
            Assert.Equal(issuer, options.Issuer);
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync refuses a document whose issuer is not the authority")]
        [InlineData("https://evil.example.com/realms/demo")]
        [InlineData("https://idp.example.com/realms/demo2")]
        [InlineData("https://idp.example.com/other")]
        [InlineData("https://IDP.example.com/realms/demo")]
        [InlineData("https://idp.example.com/realms/demo//")]
        [InlineData("https://login.microsoftonline.com/{tenantid}/v2.0")]
        public async Task FromDiscoveryAsync_IssuerDiffers_ThrowsOAuth2Exception(string issuer)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => fields["issuer"] = issuer));

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));

            Assert.Contains("does not match the authority", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(issuer, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync refuses a document without an issuer, an authorization endpoint or a token endpoint string")]
        [InlineData("issuer")]
        [InlineData("authorization_endpoint")]
        [InlineData("token_endpoint")]
        public async Task FromDiscoveryAsync_MissingField_ThrowsOAuth2Exception(string field)
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, Document(fields => fields.Remove(field)))
                .Respond(HttpStatusCode.OK, Document(fields => fields[field] = 1));

            await Assert.ThrowsAsync<OAuth2Exception>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));
            await Assert.ThrowsAsync<OAuth2Exception>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync refuses an endpoint that is not an absolute https URI without a fragment, naming the property")]
        [InlineData("authorization_endpoint", "http://idp.example.com/auth", nameof(OAuth2Options.AuthorizationEndpoint))]
        [InlineData("token_endpoint", "/token", nameof(OAuth2Options.TokenEndpoint))]
        [InlineData("token_endpoint", "", nameof(OAuth2Options.TokenEndpoint))]
        [InlineData("userinfo_endpoint", "https://idp.example.com/userinfo#x", nameof(OAuth2Options.UserInfoEndpoint))]
        [InlineData("userinfo_endpoint", "javascript:alert(1)", nameof(OAuth2Options.UserInfoEndpoint))]
        public async Task FromDiscoveryAsync_InvalidEndpoint_ThrowsOAuth2Exception(string field, string value, string property)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => fields[field] = value));

            var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));

            Assert.Contains(property, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A document without a user information endpoint leaves it empty, so the user comes from the ID token")]
        public async Task FromDiscoveryAsync_NoUserInfoEndpoint_LeavesItEmpty()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => fields.Remove("userinfo_endpoint")));

            var options = await OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient());
            options.ProviderName = "Keycloak";
            options.ClientId = "client-id";
            options.RedirectUri = "https://app.example.com/auth/callback";

            Assert.Empty(options.UserInfoEndpoint);
            Assert.Equal("Keycloak", new OAuth2Client(options).ProviderName);
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync refuses a provider whose listed code challenge methods leave out S256, and accepts one that lists none")]
        [InlineData("""["plain"]""", false)]
        [InlineData("""[]""", false)]
        [InlineData("""["s256"]""", false)]
        [InlineData("""["plain","S256"]""", true)]
        [InlineData(null, true)]
        public async Task FromDiscoveryAsync_CodeChallengeMethods_RequireS256(string? methods, bool accepted)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => SetOrRemove(fields, "code_challenge_methods_supported", methods)));

            var discovery = OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient());

            if (accepted)
            {
                Assert.NotNull(await discovery);
            }
            else
            {
                var exception = await Assert.ThrowsAsync<OAuth2Exception>(() => discovery);
                Assert.Contains("S256", exception.Message, StringComparison.Ordinal);
            }
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync refuses a provider whose listed response types leave out code")]
        [InlineData("""["id_token","token id_token"]""", false)]
        [InlineData("""["code","id_token"]""", true)]
        [InlineData(null, true)]
        public async Task FromDiscoveryAsync_ResponseTypes_RequireCode(string? responseTypes, bool accepted)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => SetOrRemove(fields, "response_types_supported", responseTypes)));

            var discovery = OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient());

            if (accepted)
                Assert.NotNull(await discovery);
            else
                await Assert.ThrowsAsync<OAuth2Exception>(() => discovery);
        }

        [Theory]
        [DisplayName("A list that is not an array of strings makes the document invalid instead of turning its check off")]
        [InlineData("code_challenge_methods_supported", "\"S256\"")]
        [InlineData("code_challenge_methods_supported", "[\"S256\",1]")]
        [InlineData("response_types_supported", "\"code\"")]
        [InlineData("token_endpoint_auth_methods_supported", "{}")]
        public async Task FromDiscoveryAsync_MalformedList_ThrowsOAuth2Exception(string field, string value)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => SetOrRemove(fields, field, value)));

            await Assert.ThrowsAsync<OAuth2Exception>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));
        }

        [Theory]
        [DisplayName("ClientAuthentication is ClientSecretPost when the provider lists it, and ClientSecretBasic otherwise")]
        [InlineData("""["client_secret_basic","client_secret_post"]""", ClientAuthenticationMethod.ClientSecretPost)]
        [InlineData("""["client_secret_basic","private_key_jwt"]""", ClientAuthenticationMethod.ClientSecretBasic)]
        [InlineData(null, ClientAuthenticationMethod.ClientSecretBasic)]
        public async Task FromDiscoveryAsync_AuthMethods_SelectClientAuthentication(string? methods, ClientAuthenticationMethod expected)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => SetOrRemove(fields, "token_endpoint_auth_methods_supported", methods)));

            var options = await OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient());

            Assert.Equal(expected, options.ClientAuthentication);
        }

        [Theory]
        [DisplayName("FromDiscoveryAsync rejects an authority that is not an absolute https URI without a query or a fragment, before any request")]
        [InlineData("http://idp.example.com")]
        [InlineData("idp.example.com")]
        [InlineData("https://idp.example.com?x=1")]
        [InlineData("https://idp.example.com#x")]
        [InlineData("https://user:secret@idp.example.com")]
        [InlineData("")]
        public async Task FromDiscoveryAsync_InvalidAuthority_ThrowsArgumentException(string authority)
        {
            var handler = new StubHttpMessageHandler();

            await Assert.ThrowsAsync<ArgumentException>(() => OidcOAuth2Options.FromDiscoveryAsync(authority, handler.CreateClient()));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("FromDiscoveryAsync rejects a null authority")]
        public async Task FromDiscoveryAsync_NullAuthority_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => OidcOAuth2Options.FromDiscoveryAsync(null!));
        }

        [Theory]
        [DisplayName("An unsuccessful status, including a redirect, throws HttpRequestException with the status code")]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Found)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task FromDiscoveryAsync_UnsuccessfulStatus_ThrowsHttpRequestException(HttpStatusCode status)
        {
            var handler = new StubHttpMessageHandler().Respond(status, Document());

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));

            Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A document larger than 1 MiB throws HttpRequestException")]
        public async Task FromDiscoveryAsync_HugeDocument_ThrowsHttpRequestException()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Document(fields => fields["padding"] = new string('x', 1024 * 1024)));

            await Assert.ThrowsAsync<HttpRequestException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));
        }

        [Fact]
        [DisplayName("A document larger than 1 MiB without a stated length throws HttpRequestException")]
        public async Task FromDiscoveryAsync_HugeChunkedDocument_ThrowsHttpRequestException()
        {
            using var http = new HttpClient(new StreamHandler(() => new EndlessStream()));

            await Assert.ThrowsAsync<HttpRequestException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, http));
        }

        [Fact]
        [DisplayName("A document without a stated length within 1 MiB is read")]
        public async Task FromDiscoveryAsync_ChunkedDocument_ReturnsOptions()
        {
            using var http = new HttpClient(new StreamHandler(() => new UnseekableStream(System.Text.Encoding.UTF8.GetBytes(Document()))));

            var options = await OidcOAuth2Options.FromDiscoveryAsync(Authority, http);

            Assert.Equal(Authority, options.Issuer);
        }

        [Fact]
        [DisplayName("The timeout of the HTTP client also ends a body that stops arriving after the headers")]
        public async Task FromDiscoveryAsync_StalledBody_TimesOut()
        {
            using var http = new HttpClient(new StreamHandler(() => new StalledStream())) { Timeout = TimeSpan.FromMilliseconds(200) };

            var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, http)).WithTimeout();

            Assert.IsType<TimeoutException>(exception.InnerException);
        }

        [Fact]
        [DisplayName("A caller that cancels before the headers arrive gets OperationCanceledException with its own token")]
        public async Task FromDiscoveryAsync_CanceledBeforeHeaders_CarriesCallerToken()
        {
            var handler = new StubHttpMessageHandler().Hang();
            using var cancellation = new CancellationTokenSource();

            var discovery = OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient(), cancellation.Token);
            await handler.Hanging;
            await cancellation.CancelAsync();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => discovery);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }

        [Fact]
        [DisplayName("A connection that fails while the body is read throws HttpRequestException")]
        public async Task FromDiscoveryAsync_BrokenBody_ThrowsHttpRequestException()
        {
            using var http = new HttpClient(new StreamHandler(() => new BrokenStream()));

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, http));

            Assert.IsType<IOException>(exception.InnerException);
        }

        [Fact]
        [DisplayName("A caller that cancels while the body is read gets OperationCanceledException with its own token")]
        public async Task FromDiscoveryAsync_CanceledDuringBody_CarriesCallerToken()
        {
            using var http = new HttpClient(new StreamHandler(() => new StalledStream()));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, http, cancellation.Token)).WithTimeout();

            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }

        [Theory]
        [DisplayName("A response that is not a JSON object throws JsonException")]
        [InlineData("<html></html>")]
        [InlineData("[]")]
        public async Task FromDiscoveryAsync_NotJsonObject_ThrowsJsonException(string body)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, body);

            await Assert.ThrowsAnyAsync<JsonException>(() => OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient()));
        }

        [Fact]
        [DisplayName("Cancellation propagates as OperationCanceledException")]
        public async Task FromDiscoveryAsync_Canceled_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Hang();
            using var cancellation = new CancellationTokenSource();

            var discovery = OidcOAuth2Options.FromDiscoveryAsync(Authority, handler.CreateClient(), cancellation.Token);
            await handler.Hanging;
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => discovery);
        }

        // Answers every request with status 200 and a body without a stated length.
        private sealed class StreamHandler : HttpMessageHandler
        {
            private readonly Func<Stream> _body;

            public StreamHandler(Func<Stream> body)
            {
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(_body()) });
            }
        }

        private class UnseekableStream : MemoryStream
        {
            public UnseekableStream(byte[] buffer) : base(buffer)
            {
            }

            public override bool CanSeek => false;
        }

        private sealed class EndlessStream : UnseekableStream
        {
            public EndlessStream() : base([])
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                for (int i = offset; i < offset + count; i++)
                    buffer[i] = (byte)' ';
                return count;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(Read(buffer, offset, count));
            }

#if NET
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                buffer.Span.Fill((byte)' ');
                return ValueTask.FromResult(buffer.Length);
            }
#endif
        }

        private sealed class BrokenStream : UnseekableStream
        {
            public BrokenStream() : base([])
            {
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromException<int>(new IOException("The connection was reset."));
            }
#if NET

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return ValueTask.FromException<int>(new IOException("The connection was reset."));
            }
#endif
        }

        private sealed class StalledStream : UnseekableStream
        {
            public StalledStream() : base([])
            {
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

#if NET
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }
#endif
        }

        private static void SetOrRemove(Dictionary<string, object?> fields, string name, string? json)
        {
            if (json is null)
                fields.Remove(name);
            else
                fields[name] = JsonDocument.Parse(json).RootElement.Clone();
        }

        private static string Document(Action<Dictionary<string, object?>>? change = null)
        {
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["issuer"] = Authority,
                ["authorization_endpoint"] = Authority + "/protocol/openid-connect/auth",
                ["token_endpoint"] = Authority + "/protocol/openid-connect/token",
                ["userinfo_endpoint"] = Authority + "/protocol/openid-connect/userinfo",
                ["jwks_uri"] = Authority + "/protocol/openid-connect/certs",
                ["response_types_supported"] = new[] { "code", "id_token", "code id_token" },
                ["code_challenge_methods_supported"] = new[] { "plain", "S256" },
                ["token_endpoint_auth_methods_supported"] = new[] { "private_key_jwt", "client_secret_basic", "client_secret_post" }
            };
            change?.Invoke(fields);
            return JsonSerializer.Serialize(fields);
        }
    }
}
