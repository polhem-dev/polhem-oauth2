using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    public class AesCbcHmacCryptorTests
    {
        private static readonly byte[] s_aesKey = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        private static readonly byte[] s_hmacKey = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789");

        [Fact]
        [DisplayName("Encrypt followed by Decrypt returns the original plaintext")]
        public void EncryptDecrypt_RoundTrip_ReturnsOriginalPlaintext()
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes("state payload: café");

            byte[] encrypted = AesCbcHmacCryptor.Encrypt(plainBytes, s_aesKey, s_hmacKey);
            byte[] decrypted = AesCbcHmacCryptor.Decrypt(encrypted, s_aesKey, s_hmacKey);

            Assert.Equal(plainBytes, decrypted);
        }

        [Fact]
        [DisplayName("Encrypting the same plaintext twice produces different ciphertext")]
        public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes("same content");

            byte[] first = AesCbcHmacCryptor.Encrypt(plainBytes, s_aesKey, s_hmacKey);
            byte[] second = AesCbcHmacCryptor.Encrypt(plainBytes, s_aesKey, s_hmacKey);

            Assert.NotEqual(first, second);
        }

        [Fact]
        [DisplayName("Decrypt rejects data whose HMAC was tampered with")]
        public void Decrypt_TamperedHmac_ThrowsCryptographicException()
        {
            byte[] encrypted = AesCbcHmacCryptor.Encrypt(Encoding.UTF8.GetBytes("sensitive"), s_aesKey, s_hmacKey);

            // The trailing 32 bytes are the HMAC, so this flips bits inside it.
            encrypted[encrypted.Length - 10] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(encrypted, s_aesKey, s_hmacKey));
        }

        // These ciphertexts were produced once by Bee.Base 3.4.0, the library Bee.OAuth2 depended on, with the
        // combined key made of the bytes 0 through 63. They stop decrypting if the port changes the byte layout.
        [Theory]
        [DisplayName("Decrypt recovers plaintext that Bee.Base 3.4.0 encrypted")]
        [InlineData("Google", "EAAAAE1xg3LHeK+cNi4HfEC94IUQAAAAAAcxha40fQOacTkyXnSd8Y5yeyQPc5+SPbO073NUToc5aOM7ZGfrlEp0ujYbDs6w")]
        [InlineData("an-oauth2-client-name-spanning-several-aes-blocks", "EAAAANNRj/5XDS16U1hwjgoiOMlAAAAA6WwSt5PHOtiSY28MYnPpF/3zNchsZOlLx4sC6fOxAY8HhjL0hDM3r8Q+e2WKQLJSt0reZcejXeSojnz5uEhdzMl+3/zyiILUVGytIJpjdkvHF10R5gr63VBUd25OVcMO")]
        public void Decrypt_CiphertextFromBeeBase340_ReturnsOriginalPlaintext(string expected, string cipherBase64)
        {
            byte[] combinedKey = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
            AesCbcHmacKeyGenerator.FromCombinedKey(combinedKey, out var aesKey, out var hmacKey);

            byte[] decrypted = AesCbcHmacCryptor.Decrypt(Convert.FromBase64String(cipherBase64), aesKey, hmacKey);

            Assert.Equal(expected, Encoding.UTF8.GetString(decrypted));
        }
    }
}
