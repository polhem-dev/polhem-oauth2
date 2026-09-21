using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads claims from a LINE ID token.
    /// </summary>
    /// <remarks>
    /// Only the audience is compared with the client ID. The signature is not verified: the token arrives straight from the
    /// token endpoint over TLS, which item 6 of OpenID Connect Core 1.0, section 3.1.3.7, accepts in place of checking the
    /// signature. The issuer and the expiry, which that section also asks for, are not checked, because the claim read here
    /// only fills in the email address of the user. Do not pass a token that arrived any other way, such as in a redirect.
    /// </remarks>
    internal static class LineIdToken
    {
        private static readonly char[] s_segmentSeparator = { '.' };

        /// <summary>
        /// Gets the email claim of an ID token issued to a client.
        /// </summary>
        /// <param name="idToken">The ID token, a JSON Web Token.</param>
        /// <param name="clientId">The client ID the token must be issued to.</param>
        /// <returns>
        /// The email address, or null when the token is malformed, is issued to another client, or has no email claim.
        /// </returns>
        public static string? ReadEmail(string idToken, string clientId)
        {
            string[] segments = idToken.Split(s_segmentSeparator);
            if (segments.Length != 3 || clientId.Length == 0)
                return null;

            byte[] payload;
            try
            {
                payload = Base64Url.Decode(segments[1]);
            }
            catch (FormatException)
            {
                return null;
            }

            try
            {
                using (var document = OAuth2Json.ParseObject(Encoding.UTF8.GetString(payload)))
                {
                    var claims = document.RootElement;
                    return HasAudience(claims, clientId) ? OAuth2Json.GetProtocolString(claims, "email") : null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // The aud claim is either one string or an array of strings (RFC 7519, section 4.1.3).
        private static bool HasAudience(JsonElement claims, string clientId)
        {
            if (!claims.TryGetProperty("aud", out var audience))
                return false;

            switch (audience.ValueKind)
            {
                case JsonValueKind.String:
                    return string.Equals(audience.GetString(), clientId, StringComparison.Ordinal);
                case JsonValueKind.Array:
                    return audience.EnumerateArray().Any(item =>
                        item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), clientId, StringComparison.Ordinal));
                default:
                    return false;
            }
        }
    }
}
