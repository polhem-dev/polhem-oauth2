using System.ComponentModel;
using System.Security.Cryptography;

namespace Polhem.OAuth2.UnitTests
{
    // OAuth2StateCryptor reads OAUTH2_STATE_KEY once, when the type is first used. These tests hold both with and
    // without a valid key in that variable, so they do not depend on the environment of the test run.
    public class OAuth2StateCryptorTests
    {
        [Fact]
        [DisplayName("DecryptClientName recovers the client name that EncryptClientName protected")]
        public void DecryptClientName_StateFromEncryptClientName_ReturnsClientName()
        {
            string state = OAuth2StateCryptor.EncryptClientName("Google");

            Assert.Equal("Google", OAuth2StateCryptor.DecryptClientName(state));
        }

        [Theory]
        [DisplayName("DecryptClientName reports a state that is not base64 as a cryptographic failure")]
        [InlineData("not base64!")]
        [InlineData("abc")]
        public void DecryptClientName_NotBase64_ThrowsCryptographicException(string state)
        {
            Assert.Throws<CryptographicException>(() => OAuth2StateCryptor.DecryptClientName(state));
        }
    }
}
