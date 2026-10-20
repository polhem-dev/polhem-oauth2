using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Creates ID tokens for tests. The library does not verify the signature of an ID token from the token endpoint, so the
    /// signature segment is a placeholder.
    /// </summary>
    internal static class TestIdToken
    {
        /// <summary>
        /// Creates an ID token with a payload.
        /// </summary>
        /// <param name="payload">The payload, usually a JSON object.</param>
        /// <returns>The token, a JSON Web Token with three segments.</returns>
        public static string Create(string payload)
        {
            return "eyJhbGciOiJSUzI1NiJ9." + Base64Url.Encode(Encoding.UTF8.GetBytes(payload)) + ".signature";
        }
    }
}
