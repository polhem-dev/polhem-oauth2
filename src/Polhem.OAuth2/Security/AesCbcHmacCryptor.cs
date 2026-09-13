using System.Security.Cryptography;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Encrypts data with AES-CBC and authenticates it with HMAC-SHA256.
    /// </summary>
    /// <remarks>
    /// The byte layout matches <c>Bee.Base.AesCbcHmacCryptor</c> 3.4.0, the implementation Bee.OAuth2 used:
    /// a length-prefixed IV, a length-prefixed ciphertext, then a 32-byte HMAC over everything before it. Both length
    /// prefixes are little-endian 32-bit integers. The golden vectors in <c>AesCbcHmacCryptorTests</c> pin that compatibility.
    /// </remarks>
    internal static class AesCbcHmacCryptor
    {
        private const int LengthPrefixSize = 4;
        private const int IvSize = 16;
        private const int BlockSize = 16;
        private const int HmacSize = 32;

        /// <summary>
        /// Encrypts the data with a random IV and appends an HMAC.
        /// </summary>
        /// <param name="plainBytes">The data to encrypt.</param>
        /// <param name="aesKey">The 32-byte AES key.</param>
        /// <param name="hmacKey">The 32-byte HMAC key.</param>
        /// <returns>The IV, ciphertext and HMAC in a single buffer.</returns>
        public static byte[] Encrypt(byte[] plainBytes, byte[] aesKey, byte[] hmacKey)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write(iv.Length);
                        writer.Write(iv);
                        writer.Write(cipherBytes.Length);
                        writer.Write(cipherBytes);

                        byte[] data = ms.ToArray();

                        using (var hmac = new HMACSHA256(hmacKey))
                        {
                            byte[] hmacBytes = hmac.ComputeHash(data);
                            return Combine(data, hmacBytes);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verifies the HMAC and decrypts the data.
        /// </summary>
        /// <param name="encryptedData">The buffer produced by <see cref="Encrypt"/>.</param>
        /// <param name="aesKey">The 32-byte AES key.</param>
        /// <param name="hmacKey">The 32-byte HMAC key.</param>
        /// <returns>The decrypted data.</returns>
        /// <remarks>
        /// The lengths recorded in the buffer are checked against its actual size before anything is copied, and the
        /// HMAC is verified before any decryption. Malformed and tampered input is therefore reported as a
        /// <see cref="CryptographicException"/>; <c>AesCbcHmacCryptorTests</c> covers truncated data, altered length fields
        /// and altered content.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="encryptedData"/> is null.</exception>
        /// <exception cref="CryptographicException">The data is malformed, fails authentication, or cannot be decrypted with the key.</exception>
        public static byte[] Decrypt(byte[] encryptedData, byte[] aesKey, byte[] hmacKey)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));

            const int cipherLengthOffset = LengthPrefixSize + IvSize;
            const int cipherOffset = cipherLengthOffset + LengthPrefixSize;

            if (encryptedData.Length < cipherOffset + BlockSize + HmacSize)
                throw MalformedData();

            if (ReadInt32LittleEndian(encryptedData, 0) != IvSize)
                throw MalformedData();

            int cipherLength = ReadInt32LittleEndian(encryptedData, cipherLengthOffset);
            if (cipherLength <= 0 || cipherLength % BlockSize != 0 || cipherLength != encryptedData.Length - cipherOffset - HmacSize)
                throw MalformedData();

            int authenticatedLength = cipherOffset + cipherLength;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                byte[] computedHmac = hmac.ComputeHash(encryptedData, 0, authenticatedLength);
                if (!FixedTimeEquals(computedHmac, encryptedData, authenticatedLength))
                    throw new CryptographicException("HMAC validation failed.");
            }

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(encryptedData, LengthPrefixSize, iv, 0, IvSize);

            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(encryptedData, cipherOffset, cipherLength);
                }
            }
        }

        private static CryptographicException MalformedData()
        {
            return new CryptographicException("The encrypted data is malformed.");
        }

        private static int ReadInt32LittleEndian(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        private static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        // Constant-time comparison, so the time taken does not reveal how many leading HMAC bytes matched.
        // The caller has already checked that the buffer holds a full HMAC at the offset.
        private static bool FixedTimeEquals(byte[] expected, byte[] data, int offset)
        {
            int result = 0;
            for (int i = 0; i < expected.Length; i++)
                result |= expected[i] ^ data[offset + i];
            return result == 0;
        }
    }
}
