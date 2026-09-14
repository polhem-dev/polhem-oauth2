using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class AuthorizationResultTests
    {
        [Fact]
        [DisplayName("Success creates a successful result with the provider name, tokens and user information")]
        public void Success_ValidArguments_CreatesSuccessfulResult()
        {
            var token = new TokenResponse("access");
            var user = new UserInfo("1", "Ada", null, "{}");

            var result = AuthorizationResult.Success("Google", token, user);

            Assert.True(result.IsSuccess);
            Assert.Equal("Google", result.ProviderName);
            Assert.Same(token, result.Token);
            Assert.Same(user, result.UserInfo);
            Assert.Null(result.Exception);
        }

        [Fact]
        [DisplayName("Failure creates a failed result that carries only the exception")]
        public void Failure_Exception_CreatesFailedResult()
        {
            var exception = new OAuth2Exception("Failed.");

            var result = AuthorizationResult.Failure(exception);

            Assert.False(result.IsSuccess);
            Assert.Same(exception, result.Exception);
            Assert.Null(result.ProviderName);
            Assert.Null(result.Token);
            Assert.Null(result.UserInfo);
        }

        [Fact]
        [DisplayName("Success and Failure reject null arguments")]
        public void Factories_NullArguments_ThrowArgumentNullException()
        {
            var token = new TokenResponse("access");
            var user = new UserInfo("1", null, null, "{}");

            Assert.Throws<ArgumentNullException>(() => AuthorizationResult.Success(null!, token, user));
            Assert.Throws<ArgumentNullException>(() => AuthorizationResult.Success("Google", null!, user));
            Assert.Throws<ArgumentNullException>(() => AuthorizationResult.Success("Google", token, null!));
            Assert.Throws<ArgumentNullException>(() => AuthorizationResult.Failure(null!));
        }
    }
}
