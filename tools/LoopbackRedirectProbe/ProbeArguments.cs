using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OAuthSamples;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// The command-line arguments of the probe.
    /// </summary>
    internal sealed class ProbeArguments
    {
        public static readonly string Usage =
            "Usage: LoopbackRedirectProbe --provider <" + string.Join("|", OAuthConfig.ProviderNames) + ">" +
            " [--redirect <loopback URI>] [--settings <path>] [--timeout <seconds>]" +
            " [--scopes \"<scope> <scope> ...\"] [--secret keep|omit] [--refresh no|yes]";

        private ProbeArguments(
            string provider, Uri? redirectUri, string settingsPath, int timeoutSeconds, string[]? scopes, bool omitSecret, bool refresh)
        {
            Provider = provider;
            RedirectUri = redirectUri;
            SettingsPath = settingsPath;
            TimeoutSeconds = timeoutSeconds;
            Scopes = scopes;
            OmitSecret = omitSecret;
            Refresh = refresh;
        }

        public string Provider { get; }

        /// <summary>
        /// Gets the redirect URI that replaces the one in the settings file, or null to use the settings file.
        /// </summary>
        public Uri? RedirectUri { get; }

        /// <summary>
        /// Gets the settings file to read. The default is the shared <c>OAuthConfig.json</c> in the output folder.
        /// </summary>
        public string SettingsPath { get; }

        public int TimeoutSeconds { get; }

        /// <summary>
        /// Gets the scopes that replace those in the settings file, or null to use the settings file.
        /// </summary>
        public string[]? Scopes { get; }

        /// <summary>
        /// Gets a value indicating whether the client secret of the settings file is left out, to test a provider without it.
        /// </summary>
        public bool OmitSecret { get; }

        /// <summary>
        /// Gets a value indicating whether a refresh token that the provider issues is used once after the sign-in.
        /// </summary>
        public bool Refresh { get; }

        public static bool TryParse(string[] args, [NotNullWhen(true)] out ProbeArguments? arguments, out string error)
        {
            arguments = null;

            if (!TryReadValues(args, out var values, out error))
                return false;

            string? provider = values.TryGetValue("provider", out var requestedProvider)
                ? OAuthConfig.ProviderNames.FirstOrDefault(name => string.Equals(name, requestedProvider, StringComparison.OrdinalIgnoreCase))
                : null;
            if (provider is null)
            {
                error = "--provider must be one of: " + string.Join(", ", OAuthConfig.ProviderNames) + ".";
                return false;
            }

            Uri? redirectUri = null;
            if (values.TryGetValue("redirect", out var redirect) && !Uri.TryCreate(redirect, UriKind.Absolute, out redirectUri))
            {
                error = "--redirect must be an absolute URI, for example http://127.0.0.1:0/callback.";
                return false;
            }

            int timeoutSeconds = 180;
            if (values.TryGetValue("timeout", out var timeout)
                && (!int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds <= 0))
            {
                error = "--timeout must be a positive number of seconds.";
                return false;
            }

            string settingsPath = values.TryGetValue("settings", out var settings) ? settings : OAuthConfig.DefaultFilePath;

            string[]? scopes = values.TryGetValue("scopes", out var scopeList)
                ? scopeList.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                : null;
            if (scopes is { Length: 0 })
            {
                error = "--scopes must name at least one scope.";
                return false;
            }

            if (!TryParseChoice(values, "secret", "keep", "omit", out bool omitSecret, out error)
                || !TryParseChoice(values, "refresh", "no", "yes", out bool refresh, out error))
            {
                return false;
            }

            arguments = new ProbeArguments(provider, redirectUri, settingsPath, timeoutSeconds, scopes, omitSecret, refresh);
            error = string.Empty;
            return true;
        }

        // Reads the arguments as pairs of --name value.
        private static bool TryReadValues(string[] args, out Dictionary<string, string> values, out string error)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Unexpected argument '{args[i]}'.";
                    return false;
                }
                values[args[i][2..]] = args[i + 1];
            }
            error = string.Empty;
            return true;
        }

        // Reads an option that takes one of two words, the first of which is the default.
        private static bool TryParseChoice(
            Dictionary<string, string> values, string name, string off, string on, out bool chosen, out string error)
        {
            chosen = false;
            error = string.Empty;
            if (!values.TryGetValue(name, out var value) || string.Equals(value, off, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, on, StringComparison.OrdinalIgnoreCase))
            {
                chosen = true;
                return true;
            }
            error = $"--{name} must be {off} or {on}.";
            return false;
        }
    }
}
