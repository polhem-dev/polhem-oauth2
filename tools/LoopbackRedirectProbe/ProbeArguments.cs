using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// The command-line arguments of the probe.
    /// </summary>
    internal sealed class ProbeArguments
    {
        public const string Usage =
            "Usage: LoopbackRedirectProbe --provider <Google|Facebook|Line|Azure|Auth0|Okta> [--redirect <loopback URI>]" +
            " [--settings <path>] [--timeout <seconds>]";

        private static readonly string[] s_providers = { "Google", "Facebook", "Line", "Azure", "Auth0", "Okta" };

        private ProbeArguments(string provider, Uri? redirectUri, string settingsPath, int timeoutSeconds)
        {
            Provider = provider;
            RedirectUri = redirectUri;
            SettingsPath = settingsPath;
            TimeoutSeconds = timeoutSeconds;
        }

        public string Provider { get; }

        /// <summary>
        /// Gets the redirect URI that replaces the one in the settings file, or null to use the settings file.
        /// </summary>
        public Uri? RedirectUri { get; }

        public string SettingsPath { get; }

        public int TimeoutSeconds { get; }

        public static bool TryParse(string[] args, [NotNullWhen(true)] out ProbeArguments? arguments, out string error)
        {
            arguments = null;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Unexpected argument '{args[i]}'.";
                    return false;
                }
                values[args[i][2..]] = args[i + 1];
            }

            string? provider = values.TryGetValue("provider", out var requestedProvider)
                ? s_providers.FirstOrDefault(name => string.Equals(name, requestedProvider, StringComparison.OrdinalIgnoreCase))
                : null;
            if (provider is null)
            {
                error = "--provider must be one of: " + string.Join(", ", s_providers) + ".";
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

            string settingsPath = values.TryGetValue("settings", out var settings) ? settings : "probe.settings.json";

            arguments = new ProbeArguments(provider, redirectUri, settingsPath, timeoutSeconds);
            error = string.Empty;
            return true;
        }
    }
}
