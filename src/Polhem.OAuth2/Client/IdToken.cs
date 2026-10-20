using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the claims of an ID token that the token endpoint returned, after checking that it was issued to the client and,
    /// when the issuer is known, by that issuer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The signature is not verified: the token arrives straight from the token endpoint over TLS, which item 6 of OpenID
    /// Connect Core 1.0, section 3.1.3.7, accepts in place of checking the signature. The expiry is not checked either, because
    /// the token was issued in answer to this request. Do not pass a token that arrived any other way, such as in a redirect
    /// or from a front end; a back end validates such a token itself, for example with JwtBearer authentication.
    /// </para>
    /// <para>
    /// The audience and the issuer are still checked, so a token endpoint that answers with the token of another client or
    /// another issuer cannot sign a user in.
    /// </para>
    /// </remarks>
    internal static class IdToken
    {
        private static readonly char[] s_segmentSeparator = { '.' };

        /// <summary>
        /// Reads the payload of an ID token issued to a client.
        /// </summary>
        /// <param name="idToken">The ID token, a JSON Web Token with three segments.</param>
        /// <param name="clientId">The client ID that the <c>aud</c> claim must contain.</param>
        /// <param name="issuer">
        /// The value that the <c>iss</c> claim must equal, compared ordinally as OpenID Connect Core 1.0, section 3.1.3.7,
        /// requires, or null or empty to leave the issuer unchecked.
        /// </param>
        /// <returns>The payload, a parsed JSON object, which the caller disposes.</returns>
        /// <exception cref="OAuth2Exception">
        /// The token is not a JSON Web Token with a JSON object as its payload, is issued to another client, or names another
        /// issuer.
        /// </exception>
        public static JsonDocument ReadClaims(string idToken, string clientId, string? issuer)
        {
            ArgumentNullException.ThrowIfNull(idToken);
            ArgumentNullException.ThrowIfNull(clientId);

            // An encrypted token (JSON Web Encryption) has five segments and is not supported.
            string[] segments = idToken.Split(s_segmentSeparator);
            if (segments.Length != 3)
                throw new OAuth2Exception("The ID token is not a signed JSON Web Token.");

            JsonDocument document;
            try
            {
                document = OAuth2Json.ParseObject(Encoding.UTF8.GetString(Base64Url.Decode(segments[1])));
            }
            catch (FormatException exception)
            {
                throw new OAuth2Exception("The payload of the ID token is not valid base64url.", exception);
            }
            catch (JsonException exception)
            {
                throw new OAuth2Exception("The payload of the ID token is not a JSON object.", exception);
            }

            try
            {
                var claims = document.RootElement;
                // Items 3 and 5 of section 3.1.3.7: the audience must contain the client, and an authorized party, which a
                // token with several audiences names, must be the client.
                if (clientId.Length == 0 || !HasAudience(claims, clientId)
                    || (claims.TryGetProperty("azp", out _) && !string.Equals(OAuth2Json.GetProtocolString(claims, "azp"), clientId, StringComparison.Ordinal)))
                {
                    throw new OAuth2Exception("The ID token was issued to another client.");
                }
                if (!string.IsNullOrEmpty(issuer)
                    && !string.Equals(OAuth2Json.GetProtocolString(claims, "iss"), issuer, StringComparison.Ordinal))
                {
                    throw new OAuth2Exception("The ID token was issued by another issuer.");
                }
                return document;
            }
            catch (OAuth2Exception)
            {
                document.Dispose();
                throw;
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
