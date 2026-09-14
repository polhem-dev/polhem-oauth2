namespace Polhem.OAuth2
{
    /// <summary>
    /// The tokens that a provider's token endpoint returns for an authorization code or a refresh token.
    /// </summary>
    /// <remarks>Every token grants access to the user's account. Keep them out of logs and error pages.</remarks>
    public sealed class TokenResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenResponse"/> class.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="tokenType">The token type, such as <c>Bearer</c>, or null if the response does not include one.</param>
        /// <param name="expiresIn">How long the access token stays valid after the response, or null if the response does not say.</param>
        /// <param name="refreshToken">The refresh token, or null if the provider did not issue one.</param>
        /// <param name="idToken">The OpenID Connect ID token, or null if the provider did not issue one.</param>
        /// <param name="scope">The granted scopes separated by spaces, or null if the response does not list them.</param>
        /// <param name="rawJson">The raw JSON returned by the token endpoint.</param>
        /// <exception cref="ArgumentNullException"><paramref name="accessToken"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="accessToken"/> is empty.</exception>
        public TokenResponse(
            string accessToken,
            string? tokenType = null,
            TimeSpan? expiresIn = null,
            string? refreshToken = null,
            string? idToken = null,
            string? scope = null,
            string rawJson = "")
        {
            if (accessToken is null)
                throw new ArgumentNullException(nameof(accessToken));
            if (accessToken.Length == 0)
                throw new ArgumentException("The access token cannot be empty.", nameof(accessToken));

            AccessToken = accessToken;
            TokenType = tokenType;
            ExpiresIn = expiresIn;
            RefreshToken = refreshToken;
            IdToken = idToken;
            Scope = scope;
            RawJson = rawJson ?? string.Empty;
        }

        /// <summary>
        /// Gets the access token.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Gets the token type, such as <c>Bearer</c>, or null if the response does not include one.
        /// </summary>
        public string? TokenType { get; }

        /// <summary>
        /// Gets how long the access token stays valid after the response was received, or null if the response does not say.
        /// </summary>
        public TimeSpan? ExpiresIn { get; }

        /// <summary>
        /// Gets the refresh token, or null if the provider did not issue one.
        /// </summary>
        /// <remarks>
        /// Some providers issue a new refresh token each time one is used and reject the old one afterwards. Keep the refresh
        /// token of the latest response.
        /// </remarks>
        public string? RefreshToken { get; }

        /// <summary>
        /// Gets the OpenID Connect ID token, or null if the provider did not issue one.
        /// </summary>
        /// <remarks>This library does not validate the ID token. Validate it before relying on its claims.</remarks>
        public string? IdToken { get; }

        /// <summary>
        /// Gets the granted scopes separated by spaces, or null if the response does not list them.
        /// </summary>
        public string? Scope { get; }

        /// <summary>
        /// Gets the raw JSON returned by the token endpoint, which includes the tokens.
        /// </summary>
        public string RawJson { get; }
    }
}
