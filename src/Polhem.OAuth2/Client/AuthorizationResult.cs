namespace Polhem.OAuth2
{
    /// <summary>
    /// The outcome of exchanging an authorization code.
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// Gets or sets the provider name. Set only for a successful result.
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the exchange succeeded.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the user information. Set only for a successful result.
        /// </summary>
        public UserInfo? UserInfo { get; set; }

        /// <summary>
        /// Gets or sets the tokens returned by the token endpoint. Set only for a successful result.
        /// </summary>
        public TokenResponse? Token { get; set; }

        /// <summary>
        /// Gets or sets the exception that made the exchange fail. Set only for a failed result.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
