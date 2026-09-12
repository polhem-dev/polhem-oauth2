using System.Security.Cryptography;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Encrypts data with AES-CBC and authenticates it with HMAC-SHA256.
    /// </summary>
    /// <remarks>
    /// The byte layout matches <c>Bee.Base.AesCbcHmacCryptor</c> 3.4.0, the implementation Bee.OAuth2 used:
    /// a length-prefixed IV, a length-prefixed ciphertext, then a 32-byte HMAC over everything before it.
    /// The golden vectors in <c>AesCbcHmacCryptorTests</c> pin that compatibility.
    /// </remarks>
    internal static class AesCbcHmacCryptor
    {
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
        /// <exception cref="CryptographicException">The HMAC does not match.</exception>
        public static byte[] Decrypt(byte[] encryptedData, byte[] aesKey, byte[] hmacKey)
        {
            using (var ms = new MemoryStream(encryptedData))
            using (var reader = new BinaryReader(ms))
            {
                int ivLength = reader.ReadInt32();
                byte[] iv = reader.ReadBytes(ivLength);
                int cipherLength = reader.ReadInt32();
                byte[] cipherBytes = reader.ReadBytes(cipherLength);
                byte[] hmacBytes = reader.ReadBytes(32);

                byte[] dataToVerify = new byte[ivLength + cipherLength + 8];
                Array.Copy(encryptedData, 0, dataToVerify, 0, dataToVerify.Length);

                using (var hmac = new HMACSHA256(hmacKey))
                {
                    byte[] computedHmac = hmac.ComputeHash(dataToVerify);
                    if (!CompareBytes(hmacBytes, computedHmac))
                        throw new CryptographicException("HMAC validation failed.");
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    }
                }
            }
        }

        private static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        // Constant-time comparison, so the time taken does not reveal how many leading HMAC bytes matched.
        private static bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }
    }
}
