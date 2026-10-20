namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Creates the options of each provider for tests.
    /// </summary>
    internal static class TestOptions
    {
        /// <summary>
        /// Creates valid options of the provider that a name selects, with the client ID <c>client-id</c>.
        /// </summary>
        /// <param name="providerName">Google, Facebook, LINE, Azure, Auth0, Okta, GitHub or OpenID Connect.</param>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <param name="clientSecret">The client secret, or an empty string for none.</param>
        /// <returns>The options.</returns>
        public static OAuth2Options Create(string providerName, string redirectUri, string clientSecret = "")
        {
            OAuth2Options options = providerName switch
            {
                "Google" => new GoogleOAuth2Options(),
                "Facebook" => new FacebookOAuth2Options(),
                "LINE" => new LineOAuth2Options(),
                "Azure" => new AzureOAuth2Options(),
                "Auth0" => new Auth0OAuth2Options { Domain = "tenant.auth0.com" },
                "Okta" => new OktaOAuth2Options { Domain = "dev-123456.okta.com" },
                "GitHub" => new GitHubOAuth2Options(),
                "OpenID Connect" => new OidcOAuth2Options
                {
                    ProviderName = "OpenID Connect",
                    Issuer = "https://idp.example.com",
                    AuthorizationEndpoint = "https://idp.example.com/authorize",
                    TokenEndpoint = "https://idp.example.com/token",
                    UserInfoEndpoint = "https://idp.example.com/userinfo"
                },
                _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Unknown provider.")
            };
            options.ClientId = "client-id";
            options.ClientSecret = clientSecret;
            options.RedirectUri = redirectUri;
            return options;
        }
    }
}
