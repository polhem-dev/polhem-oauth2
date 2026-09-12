using System;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Handles the 64-byte combined key consumed by <see cref="AesCbcHmacCryptor"/>.
    /// </summary>
    internal static class AesCbcHmacKeyGenerator
    {
        /// <summary>
        /// Splits a combined key into its AES half and its HMAC half.
        /// </summary>
        /// <param name="combinedKey">A 64-byte key: the AES key followed by the HMAC key.</param>
        /// <param name="aesKey">The first 32 bytes.</param>
        /// <param name="hmacKey">The last 32 bytes.</param>
        /// <exception cref="ArgumentException">The combined key is null or not 64 bytes long.</exception>
        public static void FromCombinedKey(byte[] combinedKey, out byte[] aesKey, out byte[] hmacKey)
        {
            if (combinedKey == null || combinedKey.Length != 64)
                throw new ArgumentException("Combined key must be 64 bytes.");

            aesKey = new byte[32];
            hmacKey = new byte[32];
            Buffer.BlockCopy(combinedKey, 0, aesKey, 0, 32);
            Buffer.BlockCopy(combinedKey, 32, hmacKey, 0, 32);
        }
    }
}
