namespace Polhem.OAuth2
{
    /// <summary>
    /// Checks text against the alphabet of unpadded base64url (RFC 4648, section 5), in which a state, a PKCE code challenge
    /// and a relay code are written.
    /// </summary>
    /// <remarks>Both web packages compile this file.</remarks>
    internal static class Base64UrlText
    {
        /// <summary>
        /// Checks whether every character of a value is in the base64url alphabet. An empty value has none that is not, so
        /// callers check the length as well.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True if no character is outside the alphabet.</returns>
        public static bool IsBase64Url(string value)
        {
            foreach (char c in value)
            {
                if (!IsBase64UrlCharacter(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether a character is in the base64url alphabet.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True for a letter or digit of ASCII, a hyphen or an underscore.</returns>
        public static bool IsBase64UrlCharacter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
        }
    }
}
