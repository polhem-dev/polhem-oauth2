using System.ComponentModel;
using System.Net;

namespace Polhem.OAuth2.UnitTests
{
    public class BaseOAuth2ClientTests
    {
        [Theory]
        [DisplayName("ValidateAuthorization turns an empty authorization code into a failed result")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ValidateAuthorization_EmptyCode_ReturnsFailedResultWithOAuth2Exception(string? code)
        {
            var client = new TestClient(new GoogleOAuth2Options(), new MemoryStateStorage());

            var result = await client.ValidateAuthorization(code);

            Assert.False(result.IsSuccess);
            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        [Fact]
        [DisplayName("ValidateAuthorization turns a network failure into a failed result")]
        public async Task ValidateAuthorization_NetworkFailure_ReturnsFailedResultWithHttpRequestException()
        {
            var handler = new StubHttpMessageHandler().Fail(new HttpRequestException("Simulated network failure."));
            var client = new TestClient(new GoogleOAuth2Options(), new MemoryStateStorage(), handler);

            var result = await client.ValidateAuthorization("code");

            Assert.False(result.IsSuccess);
            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        [DisplayName("ValidateAuthorization turns an error from the token endpoint into a failed result with the error code")]
        public async Task ValidateAuthorization_TokenEndpointError_ReturnsFailedResultWithErrorCode()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            var client = new TestClient(new GoogleOAuth2Options(), new MemoryStateStorage(), handler);

            var result = await client.ValidateAuthorization("code");

            Assert.False(result.IsSuccess);
            Assert.Equal("invalid_grant", Assert.IsType<OAuth2Exception>(result.Exception).Error);
        }

        [Fact]
        [DisplayName("ValidateAuthorization returns the tokens and user information of a successful exchange")]
        public async Task ValidateAuthorization_SuccessfulExchange_ReturnsTokensAndUserInfo()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access","refresh_token":"refresh"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada","email":"ada@example.com"}""");
            var client = new TestClient(new GoogleOAuth2Options(), new MemoryStateStorage(), handler);

            var result = await client.ValidateAuthorization("code");

            Assert.True(result.IsSuccess);
            Assert.Equal("Google", result.ProviderName);
            Assert.Equal("access", result.Token?.AccessToken);
            Assert.Equal("refresh", result.Token?.RefreshToken);
            Assert.Equal("1", result.UserInfo?.UserId);
            Assert.Equal("access", handler.Requests[1].Authorization?.Parameter);
        }

        [Fact]
        [DisplayName("With PKCE the token request sends the verifier that matches the challenge of the authorization URL")]
        public async Task ValidateAuthorization_WithPkce_SendsVerifierMatchingChallenge()
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, string.Empty);
            var storage = new MemoryStateStorage();
            var client = new TestClient(new GoogleOAuth2Options { UsePkce = true }, storage, handler);

            string url = client.GetAuthorizationUrl("state");
            await client.ValidateAuthorization("code");

            string? verifier = Assert.Single(handler.Requests).FormValue("code_verifier");
            Assert.NotNull(verifier);
            Assert.Equal(Pkce.GenerateCodeChallenge(verifier), LoopbackTestHttp.GetQueryValue(url, "code_challenge"));
            Assert.Null(storage.GetCodeVerifier());
        }

        [Fact]
        [DisplayName("ValidateAuthorization lets an unexpected exception propagate")]
        public async Task ValidateAuthorization_UnexpectedException_Propagates()
        {
            var options = new GoogleOAuth2Options { UsePkce = true };
            var client = new TestClient(options, new ThrowingStateStorage());

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ValidateAuthorization("code"));
        }

        [Theory]
        [DisplayName("ValidateState rejects a missing state even when no state is stored")]
        [InlineData(null)]
        [InlineData("")]
        public void ValidateState_MissingStateAndNothingStored_ReturnsFalse(string? returnedState)
        {
            var client = new TestClient(new GoogleOAuth2Options(), new MemoryStateStorage());

            Assert.False(client.ValidateState(returnedState));
        }

        [Fact]
        [DisplayName("ValidateState rejects an empty state even when an empty state is stored")]
        public void ValidateState_EmptyStateStored_ReturnsFalse()
        {
            var storage = new MemoryStateStorage();
            storage.SaveState(string.Empty);
            var client = new TestClient(new GoogleOAuth2Options(), storage);

            Assert.False(client.ValidateState(string.Empty));
        }

        [Fact]
        [DisplayName("ValidateState accepts the state that was stored")]
        public void ValidateState_MatchingState_ReturnsTrue()
        {
            var storage = new MemoryStateStorage();
            storage.SaveState("expected-state");
            var client = new TestClient(new GoogleOAuth2Options(), storage);

            Assert.True(client.ValidateState("expected-state"));
        }

        [Fact]
        [DisplayName("ValidateState rejects a state that differs from the stored state")]
        public void ValidateState_DifferentState_ReturnsFalse()
        {
            var storage = new MemoryStateStorage();
            storage.SaveState("expected-state");
            var client = new TestClient(new GoogleOAuth2Options(), storage);

            Assert.False(client.ValidateState("other-state"));
        }

        [Fact]
        [DisplayName("ValidateState accepts a stored state only once")]
        public void ValidateState_SameStateTwice_SecondCallReturnsFalse()
        {
            var storage = new MemoryStateStorage();
            storage.SaveState("expected-state");
            var client = new TestClient(new GoogleOAuth2Options(), storage);
            client.ValidateState("expected-state");

            Assert.False(client.ValidateState("expected-state"));
        }

        private sealed class TestClient : BaseOAuth2Client
        {
            public TestClient(OAuth2Options options, IStateStorage stateStorage, StubHttpMessageHandler? handler = null)
                : base(options, handler?.CreateClient())
            {
                StateStorage = stateStorage;
            }

            public override IStateStorage StateStorage { get; }
        }

        private sealed class MemoryStateStorage : IStateStorage
        {
            private string? _state;
            private string? _codeVerifier;

            public void SaveState(string value) => _state = value;

            public string? GetState() => _state;

            public void RemoveState() => _state = null;

            public void SaveCodeVerifier(string codeVerifier) => _codeVerifier = codeVerifier;

            public string? GetCodeVerifier() => _codeVerifier;

            public void RemoveCodeVerifier() => _codeVerifier = null;
        }

        // Reading the code verifier happens before any HTTP request, so this failure is raised without network access.
        private sealed class ThrowingStateStorage : IStateStorage
        {
            public void SaveState(string value)
            {
            }

            public string? GetState() => null;

            public void RemoveState()
            {
            }

            public void SaveCodeVerifier(string codeVerifier)
            {
            }

            public string? GetCodeVerifier() => throw new InvalidOperationException("Simulated storage failure.");

            public void RemoveCodeVerifier()
            {
            }
        }
    }
}
