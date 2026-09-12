using System.ComponentModel;

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
        [DisplayName("ValidateAuthorization turns an unreachable token endpoint into a failed result")]
        public async Task ValidateAuthorization_UnreachableTokenEndpoint_ReturnsFailedResultWithHttpRequestException()
        {
            // Port 1 on the loopback interface is normally closed, so the request fails without leaving the machine.
            var options = new GoogleOAuth2Options { TokenEndpoint = "http://127.0.0.1:1/token" };
            var client = new TestClient(options, new MemoryStateStorage());

            var result = await client.ValidateAuthorization("code");

            Assert.False(result.IsSuccess);
            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        [DisplayName("ValidateAuthorization lets an unexpected exception propagate")]
        public async Task ValidateAuthorization_UnexpectedException_Propagates()
        {
            var options = new GoogleOAuth2Options { UsePkce = true };
            var client = new TestClient(options, new ThrowingStateStorage());

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ValidateAuthorization("code"));
        }

        private sealed class TestClient : BaseOAuth2Client
        {
            public TestClient(OAuth2Options options, IStateStorage stateStorage) : base(options)
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
