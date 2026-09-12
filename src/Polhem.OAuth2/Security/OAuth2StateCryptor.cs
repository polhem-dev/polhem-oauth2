using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Protects the client name carried in the OAuth2 <c>state</c> parameter.
    /// </summary>
    /// <remarks>
    /// When the <c>OAUTH2_STATE_KEY</c> environment variable holds a base64-encoded 64-byte key, the client name is
    /// encrypted with AES-CBC and authenticated with HMAC-SHA256. Without the key it is only base64-encoded, which
    /// neither hides it nor detects tampering.
    /// </remarks>
    public static class OAuth2StateCryptor
    {
        private static readonly byte[]? s_combinedKey = ReadCombinedKey();

        /// <summary>
        /// Converts a client name into a state value.
        /// </summary>
        /// <param name="clientName">The name the client was registered under.</param>
        /// <returns>The state value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clientName"/> is null, empty or white space.</exception>
        public static string EncryptClientName(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentNullException(nameof(clientName));

            var plainBytes = Encoding.UTF8.GetBytes(clientName);

            if (s_combinedKey != null)
            {
                AesCbcHmacKeyGenerator.FromCombinedKey(s_combinedKey, out var aesKey, out var hmacKey);
                var cipherBytes = AesCbcHmacCryptor.Encrypt(plainBytes, aesKey, hmacKey);
                return Convert.ToBase64String(cipherBytes);
            }
            else
            {
                return Convert.ToBase64String(plainBytes);
            }
        }

        /// <summary>
        /// Recovers the client name from a state value.
        /// </summary>
        /// <param name="state">The state value returned to the callback.</param>
        /// <returns>The client name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="state"/> is null, empty or white space.</exception>
        /// <exception cref="System.Security.Cryptography.CryptographicException">The key is set and the state fails authentication.</exception>
        public static string DecryptClientName(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentNullException(nameof(state));

            var cipherBytes = Convert.FromBase64String(state);

            if (s_combinedKey != null)
            {
                AesCbcHmacKeyGenerator.FromCombinedKey(s_combinedKey, out var aesKey, out var hmacKey);
                var plainBytes = AesCbcHmacCryptor.Decrypt(cipherBytes, aesKey, hmacKey);
                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                return Encoding.UTF8.GetString(cipherBytes);
            }
        }

        private static byte[]? ReadCombinedKey()
        {
            string? base64Key = Environment.GetEnvironmentVariable("OAUTH2_STATE_KEY");
            return base64Key is null || base64Key.Trim().Length == 0 ? null : Convert.FromBase64String(base64Key);
        }
    }
}
