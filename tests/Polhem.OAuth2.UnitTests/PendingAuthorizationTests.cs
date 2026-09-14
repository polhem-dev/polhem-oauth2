using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class PendingAuthorizationTests
    {
        [Fact]
        [DisplayName("The constructor rejects a null state or redirect URI")]
        public void Constructor_NullValues_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PendingAuthorization(null!, "verifier", "https://app.example.com/callback"));
            Assert.Throws<ArgumentNullException>(() => new PendingAuthorization("state", "verifier", null!));
        }

        [Fact]
        [DisplayName("The constructor rejects an empty state or redirect URI")]
        public void Constructor_EmptyValues_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new PendingAuthorization(string.Empty, "verifier", "https://app.example.com/callback"));
            Assert.Throws<ArgumentException>(() => new PendingAuthorization("state", "verifier", string.Empty));
        }

        [Fact]
        [DisplayName("The constructor treats an empty code verifier as no code verifier")]
        public void Constructor_EmptyCodeVerifier_StoresNull()
        {
            var pending = new PendingAuthorization("state", string.Empty, "https://app.example.com/callback");

            Assert.Null(pending.CodeVerifier);
        }
    }
}
