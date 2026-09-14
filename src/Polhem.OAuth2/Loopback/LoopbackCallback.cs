namespace Polhem.OAuth2
{
    /// <summary>
    /// The query parameters a provider sends to a loopback redirect URI.
    /// </summary>
    internal sealed class LoopbackCallback
    {
        private static readonly char[] s_pairSeparator = { '&' };

        private LoopbackCallback(Dictionary<string, string> values)
        {
            Code = Find(values, "code");
            State = Find(values, "state");
            Error = Find(values, "error");
        }

        /// <summary>
        /// Gets the authorization code, or null if the query does not include one.
        /// </summary>
        public string? Code { get; }

        /// <summary>
        /// Gets the state, or null if the query does not include one.
        /// </summary>
        public string? State { get; }

        /// <summary>
        /// Gets the error code the provider returned instead of an authorization code, or null if there is none.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Reads the parameters from a query string that has no leading question mark. When a name repeats, the first value is used.
        /// </summary>
        /// <param name="query">The query string.</param>
        /// <returns>The parameters.</returns>
        public static LoopbackCallback FromQuery(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in query.Split(s_pairSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');
                string name = Decode(equals >= 0 ? pair.Substring(0, equals) : pair);
                if (!values.ContainsKey(name))
                    values[name] = equals >= 0 ? Decode(pair.Substring(equals + 1)) : string.Empty;
            }
            return new LoopbackCallback(values);
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
