namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the parameters that a provider sends with the redirect back to the application.
    /// </summary>
    internal static class CallbackQuery
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
            AddParameters(values, query);
            return ToCallback(values);
        }

        /// <summary>
        /// Reads the callback parameters from the URI the provider redirected to: first from its query, then from its
        /// fragment. When a name repeats, the first value is used.
        /// </summary>
        /// <param name="callbackUri">The URI the provider redirected to.</param>
        /// <returns>The callback parameters.</returns>
        /// <remarks>
        /// The original string is split at the first <c>#</c> and <c>?</c> itself, because <see cref="Uri"/> does not parse
        /// the query and fragment of every custom scheme the same way.
        /// </remarks>
        public static AuthorizationCallback FromUri(Uri callbackUri)
        {
            string value = callbackUri.OriginalString;
            string fragment = string.Empty;
            int hash = value.IndexOf('#');
            if (hash >= 0)
            {
                fragment = value.Substring(hash + 1);
                value = value.Substring(0, hash);
            }

            int question = value.IndexOf('?');
            string query = question >= 0 ? value.Substring(question + 1) : string.Empty;

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            AddParameters(values, query);
            AddParameters(values, fragment);
            return ToCallback(values);
        }

        private static void AddParameters(Dictionary<string, string> values, string parameters)
        {
            foreach (string pair in parameters.Split(s_pairSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');
                string name = Decode(equals >= 0 ? pair.Substring(0, equals) : pair);
                if (!values.ContainsKey(name))
                    values[name] = equals >= 0 ? Decode(pair.Substring(equals + 1)) : string.Empty;
            }
        }

        private static AuthorizationCallback ToCallback(Dictionary<string, string> values)
        {
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
