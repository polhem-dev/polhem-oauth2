namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for OAuth2 options: the client credentials, the redirect URI and the provider endpoints. Each supported
    /// provider has its own derived type.
    /// </summary>
    public abstract class OAuth2Options
    {
        private protected OAuth2Options()
        {
        }

        /// <summary>
        /// Gets or sets the client ID that identifies the application to the provider.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client secret that authenticates the application to the provider.
        /// </summary>
        /// <remarks>Keep this value out of source control and logs.</remarks>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URI the provider sends the user back to after sign-in. It must match a redirect URI
        /// registered with the provider.
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scopes to request.
        /// </summary>
        public string[] Scopes { get; set; } = new[] { "openid", "email", "profile" };

        /// <summary>
        /// Gets or sets the authorization endpoint. It must be an absolute https URI.
        /// </summary>
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token endpoint, where the authorization code is exchanged for tokens. It must be an absolute https URI.
        /// </summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user information endpoint, which returns details such as the user's name and email address.
        /// It must be an absolute https URI.
        /// </summary>
        public string UserInfoEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the flow uses PKCE.
        /// </summary>
        public bool UsePkce { get; set; } = false;

        /// <summary>
        /// Finds an endpoint that is not an absolute https URI. The client secret, authorization codes and tokens travel to
        /// these endpoints, so none of them may be sent unencrypted.
        /// </summary>
        /// <returns>The name of the first such endpoint property, or null if every endpoint is an absolute https URI.</returns>
        internal string? FindInsecureEndpoint()
        {
            if (!IsHttpsUri(AuthorizationEndpoint))
                return nameof(AuthorizationEndpoint);
            if (!IsHttpsUri(TokenEndpoint))
                return nameof(TokenEndpoint);
            if (!IsHttpsUri(UserInfoEndpoint))
                return nameof(UserInfoEndpoint);
            return null;
        }

        private static bool IsHttpsUri(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        }
    }
}
