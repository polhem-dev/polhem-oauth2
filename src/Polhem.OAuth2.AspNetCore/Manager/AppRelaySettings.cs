namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// The relay settings, validated and copied when they are registered, so later changes to the options have no effect.
    /// </summary>
    internal sealed class AppRelaySettings
    {
        // RFC 6749, section 4.1.2, recommends at most 10 minutes for an authorization code, which a relay code stands in for.
        private static readonly TimeSpan s_maxCodeLifetime = TimeSpan.FromMinutes(10);

        private readonly HashSet<string> _appRedirectUris;

        private AppRelaySettings(HashSet<string> appRedirectUris, TimeSpan codeLifetime)
        {
            _appRedirectUris = appRedirectUris;
            CodeLifetime = codeLifetime;
        }

        /// <summary>
        /// Gets how long a relay code can be redeemed.
        /// </summary>
        public TimeSpan CodeLifetime { get; }

        /// <summary>
        /// Validates and copies the options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="paramName">The name of the parameter of the caller that produced the options, for the exception.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentException">
        /// No application redirect URI is registered, one of them is not valid, or the code lifetime is not positive or is
        /// longer than 10 minutes.
        /// </exception>
        public static AppRelaySettings Create(OAuth2AppRelayOptions options, string paramName)
        {
            if (options.AppRedirectUris.Count == 0)
                throw new ArgumentException("Register at least one application redirect URI.", paramName);
            foreach (string uri in options.AppRedirectUris)
            {
                if (!OAuth2Options.IsAppRedirectUri(uri))
                    throw new ArgumentException($"'{uri}' is not an absolute https URI or custom scheme URI without a fragment.", paramName);
            }
            if (options.CodeLifetime <= TimeSpan.Zero || options.CodeLifetime > s_maxCodeLifetime)
                throw new ArgumentException($"The code lifetime must be positive and at most {s_maxCodeLifetime.TotalMinutes:0} minutes.", paramName);

            return new AppRelaySettings(new HashSet<string>(options.AppRedirectUris, StringComparer.Ordinal), options.CodeLifetime);
        }

        /// <summary>
        /// Checks whether a relayed sign-in may return to an application redirect URI.
        /// </summary>
        /// <param name="appRedirectUri">The requested application redirect URI.</param>
        /// <returns>True if the URI is registered.</returns>
        public bool IsRegistered(string appRedirectUri)
        {
            return _appRedirectUris.Contains(appRedirectUri);
        }
    }
}
