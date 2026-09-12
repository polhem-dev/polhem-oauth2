using System.Security.Cryptography;
using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Generates the values used by PKCE (Proof Key for Code Exchange, RFC 7636).
    /// </summary>
    public static class Pkce
    {
        /// <summary>
        /// Generates a random <c>code_verifier</c>: 32 random bytes encoded as unpadded base64url, which is 43 characters long.
        /// </summary>
        /// <returns>The code verifier.</returns>
        public static string GenerateCodeVerifier()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                return ToBase64Url(bytes);
            }
        }

        /// <summary>
        /// Derives the <c>code_challenge</c> for a code verifier with the S256 method: the base64url-encoded SHA-256 hash.
        /// </summary>
        /// <param name="codeVerifier">The code verifier sent later with the token request.</param>
        /// <returns>The code challenge.</returns>
        public static string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return ToBase64Url(hash);
            }
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
