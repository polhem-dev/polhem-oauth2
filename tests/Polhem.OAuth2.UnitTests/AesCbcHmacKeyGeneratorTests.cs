using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class AesCbcHmacKeyGeneratorTests
    {
        [Fact]
        [DisplayName("FromCombinedKey returns the first 32 bytes as the AES key and the last 32 as the HMAC key")]
        public void FromCombinedKey_ValidKey_SplitsIntoAesAndHmacHalves()
        {
            byte[] combinedKey = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

            AesCbcHmacKeyGenerator.FromCombinedKey(combinedKey, out var aesKey, out var hmacKey);

            Assert.Equal(combinedKey.Take(32).ToArray(), aesKey);
            Assert.Equal(combinedKey.Skip(32).ToArray(), hmacKey);
        }

        [Theory]
        [DisplayName("FromCombinedKey rejects keys that are not 64 bytes long")]
        [InlineData(0)]
        [InlineData(48)]
        [InlineData(65)]
        public void FromCombinedKey_WrongLength_ThrowsArgumentException(int length)
        {
            Assert.Throws<ArgumentException>(() => AesCbcHmacKeyGenerator.FromCombinedKey(new byte[length], out _, out _));
        }

        [Fact]
        [DisplayName("FromCombinedKey rejects a null key")]
        public void FromCombinedKey_NullKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => AesCbcHmacKeyGenerator.FromCombinedKey(null!, out _, out _));
        }
    }
}
