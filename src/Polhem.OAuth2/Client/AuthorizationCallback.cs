namespace Polhem.OAuth2
{
    /// <summary>
    /// The query parameters of the redirect back to the application after the user signs in at the provider.
    /// </summary>
    /// <remarks>
    /// Anyone who sends the user a link can set these values. <see cref="OAuth2Client.CompleteAuthorizationAsync"/> compares
    /// the state with the pending authorization before it uses any other value.
    /// </remarks>
    public sealed class AuthorizationCallback
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationCallback"/> class.
        /// </summary>
        /// <param name="code">The <c>code</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="state">The <c>state</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="error">The <c>error</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="errorDescription">The <c>error_description</c> parameter, or null if the redirect does not include one.</param>
        public AuthorizationCallback(string? code, string? state, string? error, string? errorDescription)
        {
            Code = code;
            State = state;
            Error = error;
            ErrorDescription = errorDescription;
        }

        /// <summary>
        /// Gets the authorization code, or null if the redirect does not include one.
        /// </summary>
        public string? Code { get; }

        /// <summary>
        /// Gets the state, or null if the redirect does not include one.
        /// </summary>
        public string? State { get; }

        /// <summary>
        /// Gets the error code the provider returned instead of an authorization code, or null if there is none.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets the description returned with <see cref="Error"/>, or null if there is none.
        /// </summary>
        public string? ErrorDescription { get; }
    }
}
