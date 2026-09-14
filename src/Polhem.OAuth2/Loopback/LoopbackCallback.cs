namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the query parameters that a provider sends to a loopback redirect URI.
    /// </summary>
    internal static class LoopbackCallback
    {
        private static readonly char[] s_pairSeparator = { '&' };

        /// <summary>
        /// Reads the callback parameters from a query string that has no leading question mark. When a name repeats, the first value is used.
        /// </summary>
        /// <param name="query">The query string.</param>
        /// <returns>The callback parameters.</returns>
        public static AuthorizationCallback FromQuery(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in query.Split(s_pairSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');
                string name = Decode(equals >= 0 ? pair.Substring(0, equals) : pair);
                if (!values.ContainsKey(name))
                    values[name] = equals >= 0 ? Decode(pair.Substring(equals + 1)) : string.Empty;
            }
            return new AuthorizationCallback(Find(values, "code"), Find(values, "state"), Find(values, "error"), Find(values, "error_description"));
        }

        private static string? Find(Dictionary<string, string> values, string name)
        {
            return values.TryGetValue(name, out var value) ? value : null;
        }

        // A query string encodes a space as "+", which UnescapeDataString leaves unchanged.
        private static string Decode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
