namespace LoopbackRedirectProbe
{
    /// <summary>
    /// The query parameters a provider sends to the redirect URI.
    /// </summary>
    internal sealed class LoopbackCallback
    {
        public string? Code { get; private init; }

        public string? State { get; private init; }

        public string? Error { get; private init; }

        public string? ErrorDescription { get; private init; }

        public static LoopbackCallback FromRequestTarget(string target)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            int queryStart = target.IndexOf('?', StringComparison.Ordinal);
            if (queryStart >= 0)
            {
                foreach (string pair in target[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    int equals = pair.IndexOf('=', StringComparison.Ordinal);
                    string name = Decode(equals >= 0 ? pair[..equals] : pair);
                    string value = equals >= 0 ? Decode(pair[(equals + 1)..]) : string.Empty;
                    values.TryAdd(name, value);
                }
            }

            return new LoopbackCallback
            {
                Code = values.GetValueOrDefault("code"),
                State = values.GetValueOrDefault("state"),
                Error = values.GetValueOrDefault("error"),
                ErrorDescription = values.GetValueOrDefault("error_description")
            };
        }

        // Query strings encode a space as "+", which UnescapeDataString leaves alone.
        private static string Decode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
