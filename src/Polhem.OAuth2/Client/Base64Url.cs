namespace Polhem.OAuth2
{
    /// <summary>
    /// Encodes and decodes unpadded base64url (RFC 4648, section 5), the encoding used by PKCE and JSON Web Tokens.
    /// </summary>
    internal static class Base64Url
    {
        /// <summary>
        /// Encodes bytes as unpadded base64url text.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>The encoded text.</returns>
        public static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Decodes unpadded base64url text.
        /// </summary>
        /// <param name="text">The text to decode.</param>
        /// <returns>The decoded bytes.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not valid base64url.</exception>
        public static byte[] Decode(string text)
        {
            string base64 = text.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 1:
                    throw new FormatException("The text is not valid base64url.");
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
