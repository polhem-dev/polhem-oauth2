namespace Polhem.OAuth2
{
    /// <summary>
    /// The provider name and the issuer of a client registered with <see cref="OidcOAuth2Options"/>, which a web package
    /// compares with the clients registered before it.
    /// </summary>
    /// <remarks>
    /// A user is identified by the provider name together with the user ID, so two OpenID Connect providers that share a name
    /// share their users' identities. Both web packages compile this file and refuse such a pair when it is registered.
    /// The issuer stands for the provider, or the token endpoint when the options name no issuer, so two clients of the same
    /// provider, one with its issuer and one without, are refused as well; set the issuer on both.
    /// </remarks>
    internal sealed class OidcClientIdentity
    {
        private OidcClientIdentity(string clientName, string providerName, string issuer)
        {
            ClientName = clientName;
            ProviderName = providerName;
            Issuer = issuer;
        }

        /// <summary>
        /// Gets the name the client is registered under.
        /// </summary>
        public string ClientName { get; }

        /// <summary>
        /// Gets the provider name of the client.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Gets the issuer of the client, or its token endpoint when the options name no issuer.
        /// </summary>
        public string Issuer { get; }

        /// <summary>
        /// Reads the identity of a client from its options.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="options">The options of the client.</param>
        /// <returns>The identity, or null when the options are not <see cref="OidcOAuth2Options"/>.</returns>
        public static OidcClientIdentity? From(string clientName, OAuth2Options options)
        {
            return options is OidcOAuth2Options oidc
                ? new OidcClientIdentity(clientName, oidc.ProviderName, string.IsNullOrEmpty(oidc.Issuer) ? oidc.TokenEndpoint : oidc.Issuer)
                : null;
        }

        /// <summary>
        /// Returns the error to report when this client has the provider name of another client but another issuer.
        /// </summary>
        /// <param name="other">A client registered before this one.</param>
        /// <returns>The error, or null when the two clients do not conflict.</returns>
        public string? GetConflictError(OidcClientIdentity other)
        {
            // The names are compared ignoring case, as a database that stores them may do. A trailing slash of the issuer does
            // not count, as discovery does not count it.
            if (!string.Equals(ProviderName, other.ProviderName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Issuer.TrimEnd('/'), other.Issuer.TrimEnd('/'), StringComparison.Ordinal))
            {
                return null;
            }
            return $"The OAuth2 clients '{other.ClientName}' and '{ClientName}' sign in with different OpenID Connect providers under the "
                + $"same provider name '{ProviderName}'. A user is identified by the provider name together with the user ID, so give "
                + $"each provider a {nameof(OidcOAuth2Options.ProviderName)} of its own.";
        }
    }
}
