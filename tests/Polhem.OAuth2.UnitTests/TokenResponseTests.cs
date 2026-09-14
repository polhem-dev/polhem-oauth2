using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class TokenResponseTests
    {
        [Fact]
        [DisplayName("The constructor rejects a null access token")]
        public void Constructor_NullAccessToken_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TokenResponse(null!));
        }

        [Fact]
        [DisplayName("The constructor rejects an empty access token")]
        public void Constructor_EmptyAccessToken_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new TokenResponse(string.Empty));
        }

        [Fact]
        [DisplayName("The constructor keeps every value it is given")]
        public void Constructor_AllValues_KeepsValues()
        {
            var token = new TokenResponse("access", "Bearer", TimeSpan.FromMinutes(5), "refresh", "id", "openid", "{}");

            Assert.Equal("access", token.AccessToken);
            Assert.Equal("Bearer", token.TokenType);
            Assert.Equal(TimeSpan.FromMinutes(5), token.ExpiresIn);
            Assert.Equal("refresh", token.RefreshToken);
            Assert.Equal("id", token.IdToken);
            Assert.Equal("openid", token.Scope);
            Assert.Equal("{}", token.RawJson);
        }
    }
}
