using System.Security.Cryptography;
using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Generates the values used by PKCE (Proof Key for Code Exchange, RFC 7636).
    /// </summary>
    internal static class Pkce
    {
        /// <summary>
        /// Generates a random <c>code_verifier</c>: 32 random bytes encoded as unpadded base64url, which is 43 characters long.
        /// </summary>
        /// <returns>The code verifier.</returns>
        public static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base64Url.Encode(bytes);
        }

        /// <summary>
        /// Derives the <c>code_challenge</c> for a code verifier with the S256 method: the base64url-encoded SHA-256 hash.
        /// </summary>
        /// <param name="codeVerifier">The code verifier sent later with the token request.</param>
        /// <returns>The code challenge.</returns>
        public static string GenerateCodeChallenge(string codeVerifier)
        {
            return Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }
    }
}
