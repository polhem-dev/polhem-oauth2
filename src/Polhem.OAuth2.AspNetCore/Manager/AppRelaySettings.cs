namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// The relay settings, validated and copied when they are registered, so later changes to the options have no effect.
    /// </summary>
    internal sealed class AppRelaySettings
    {
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
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentException">
        /// No application redirect URI is registered, one of them is not valid, or the code lifetime is not positive.
        /// </exception>
        public static AppRelaySettings Create(OAuth2AppRelayOptions options)
        {
            if (options.AppRedirectUris.Count == 0)
                throw new ArgumentException("Register at least one application redirect URI.", nameof(options));
            foreach (string uri in options.AppRedirectUris)
            {
                if (!OAuth2Options.IsAppRedirectUri(uri))
                    throw new ArgumentException($"'{uri}' is not an absolute https URI or custom scheme URI without a fragment.", nameof(options));
            }
            if (options.CodeLifetime <= TimeSpan.Zero)
                throw new ArgumentException("The code lifetime must be positive.", nameof(options));

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
