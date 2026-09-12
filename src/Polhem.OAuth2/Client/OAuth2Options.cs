namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for OAuth2 options: the client credentials, the redirect URI and the provider endpoints.
    /// </summary>
    public abstract class OAuth2Options
    {
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
        /// Gets or sets the authorization endpoint.
        /// </summary>
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token endpoint, where the authorization code is exchanged for an access token.
        /// </summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user information endpoint, which returns details such as the user's name and email address.
        /// </summary>
        public string UserInfoEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the flow uses PKCE.
        /// </summary>
        public bool UsePkce { get; set; } = false;
    }
}
