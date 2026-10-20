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
            : this(code, state, error, errorDescription, user: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationCallback"/> class with the <c>user</c> parameter that
        /// Sign in with Apple sends.
        /// </summary>
        /// <param name="code">The <c>code</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="state">The <c>state</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="error">The <c>error</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="errorDescription">The <c>error_description</c> parameter, or null if the redirect does not include one.</param>
        /// <param name="user">The <c>user</c> parameter, or null if the redirect does not include one.</param>
        public AuthorizationCallback(string? code, string? state, string? error, string? errorDescription, string? user)
        {
            Code = code;
            State = state;
            Error = error;
            ErrorDescription = errorDescription;
            User = user;
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

        /// <summary>
        /// Gets the <c>user</c> parameter, or null if the redirect does not include one. Sign in with Apple sends it, as a JSON
        /// object with the name of the user, only the first time the user authorizes the application.
        /// </summary>
        /// <remarks>
        /// The browser posts the value, so the user can change it. <see cref="AppleOAuth2Options"/> takes only the name from
        /// it, into <see cref="UserInfo.UserName"/>.
        /// </remarks>
        public string? User { get; }
    }
}
