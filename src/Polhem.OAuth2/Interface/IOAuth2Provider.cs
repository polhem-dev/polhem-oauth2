namespace Polhem.OAuth2
{
    /// <summary>
    /// An OAuth2 provider.
    /// </summary>
    public interface IOAuth2Provider
    {
        /// <summary>
        /// Gets the provider name.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Builds the URL that sends the user to the provider to sign in and authorize the application.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <param name="codeChallenge">The PKCE <c>code_challenge</c>, or an empty string when PKCE is not used.</param>
        /// <returns>The authorization URL.</returns>
        string GetAuthorizationUrl(string state, string codeChallenge = "");

        /// <summary>
        /// Gets the URI the provider sends the user back to after sign-in.
        /// </summary>
        /// <returns>The redirect URI.</returns>
        string GetRedirectUrl();

        /// <summary>
        /// Exchanges an authorization code for an access token.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <param name="codeVerifier">The PKCE <c>code_verifier</c>, or an empty string when PKCE is not used.</param>
        /// <returns>The access token.</returns>
        Task<string> GetAccessTokenAsync(string authorizationCode, string codeVerifier = "");

        /// <summary>
        /// Retrieves user information with an access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The user information as a JSON string.</returns>
        Task<string> GetUserInfoAsync(string accessToken);

        /// <summary>
        /// Parses the JSON returned by the user information endpoint.
        /// </summary>
        /// <param name="json">The user information as a JSON string.</param>
        /// <returns>The parsed user information.</returns>
        UserInfo ParseUserJson(string json);

        /// <summary>
        /// Obtains a new access token with a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The new access token.</returns>
        Task<string> RefreshAccessTokenAsync(string refreshToken);
    }
}
