using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    public class AesCbcHmacCryptorTests
    {
        private const int CipherLengthOffset = 20;
        private const int CipherOffset = 24;

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
            byte[] encrypted = EncryptSample();

            // The trailing 32 bytes are the HMAC, so this flips bits inside it.
            encrypted[encrypted.Length - 10] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(encrypted, s_aesKey, s_hmacKey));
        }

        [Fact]
        [DisplayName("Decrypt rejects data whose ciphertext was tampered with")]
        public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
        {
            byte[] encrypted = EncryptSample();
            encrypted[CipherOffset] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(encrypted, s_aesKey, s_hmacKey));
        }

        [Fact]
        [DisplayName("Decrypt rejects an empty buffer")]
        public void Decrypt_EmptyData_ThrowsCryptographicException()
        {
            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(Array.Empty<byte>(), s_aesKey, s_hmacKey));
        }

        [Theory]
        [DisplayName("Decrypt rejects data with bytes missing from the end")]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(33)]
        public void Decrypt_TruncatedData_ThrowsCryptographicException(int bytesRemoved)
        {
            byte[] encrypted = EncryptSample();
            byte[] truncated = encrypted.Take(encrypted.Length - bytesRemoved).ToArray();

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(truncated, s_aesKey, s_hmacKey));
        }

        [Fact]
        [DisplayName("Decrypt rejects data with extra bytes after the HMAC")]
        public void Decrypt_TrailingBytes_ThrowsCryptographicException()
        {
            byte[] extended = EncryptSample().Concat(new byte[] { 0 }).ToArray();

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(extended, s_aesKey, s_hmacKey));
        }

        [Theory]
        [DisplayName("Decrypt rejects data whose IV length field was altered")]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(int.MaxValue)]
        [InlineData(-1)]
        public void Decrypt_AlteredIvLength_ThrowsCryptographicException(int ivLength)
        {
            byte[] encrypted = EncryptSample();
            WriteInt32LittleEndian(encrypted, 0, ivLength);

            Assert.Throws<CryptographicException>(() => AesCbcHmacCryptor.Decrypt(encrypted, s_aesKey, s_hmacKey));
        }

        [Theory]
        [DisplayName("Decrypt rejects data whose ciphertext length field was altered")]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(32)]
        [InlineData(int.MaxValue)]
        [InlineData(-16)]
        public void Decrypt_AlteredCipherLength_ThrowsCryptographicException(int cipherLength)
        {
            byte[] encrypted = EncryptSample();
            WriteInt32LittleEndian(encrypted, CipherLengthOffset, cipherLength);

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

        // A nine-byte plaintext pads to one 16-byte block, so the sample is 72 bytes: the IV length (offset 0), the IV,
        // the ciphertext length (offset 20), the ciphertext (offset 24) and the 32-byte HMAC.
        private static byte[] EncryptSample()
        {
            return AesCbcHmacCryptor.Encrypt(Encoding.UTF8.GetBytes("sensitive"), s_aesKey, s_hmacKey);
        }

        private static void WriteInt32LittleEndian(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}
