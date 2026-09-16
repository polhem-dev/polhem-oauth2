using Polhem.OAuth2;

namespace OAuthSamples
{
    /// <summary>
    /// One provider's client of one client type, as <see cref="OAuthConfig"/> read it.
    /// </summary>
    public sealed class OAuthClientEntry
    {
        internal OAuthClientEntry(string providerName, OAuth2Options options)
        {
            ProviderName = providerName;
            Options = options;
        }

        /// <summary>
        /// Gets the provider name, spelled as <see cref="OAuthConfig.ProviderNames"/> spells it whatever the settings
        /// file used.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Gets the options that the provider's shared fields and the client type section produced together.
        /// </summary>
        public OAuth2Options Options { get; }
    }
}
