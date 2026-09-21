namespace Polhem.OAuth2
{
    /// <summary>
    /// The exception thrown when an OAuth2 exchange fails for a protocol reason, such as an error returned by the provider,
    /// an empty authorization code or a token response without an access token.
    /// </summary>
    public class OAuth2Exception : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class.
        /// </summary>
        public OAuth2Exception()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class with a message.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        public OAuth2Exception(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        /// <param name="innerException">The exception that caused the failure.</param>
        public OAuth2Exception(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class for an error returned by the provider.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        /// <param name="error">The error code returned by the provider, such as <c>access_denied</c>.</param>
        /// <param name="errorDescription">The description returned with the error code, or null if there is none.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public OAuth2Exception(string message, string error, string? errorDescription) : base(message)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            ErrorDescription = errorDescription;
        }

        /// <summary>
        /// Gets the error code returned by the provider, such as <c>access_denied</c> or <c>invalid_grant</c>, or null when
        /// the failure is not an error returned by the provider.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An error in a redirect back to the application arrives in the query string, which anyone who sends the user a link
        /// can set. Encode the value before showing it in a page.
        /// </para>
        /// <para>
        /// The token endpoint of Facebook reports a Graph API error instead of these codes. Its numeric code, such as
        /// <c>100</c>, is the value here, and its message is <see cref="ErrorDescription"/>.
        /// </para>
        /// </remarks>
        public string? Error { get; }

        /// <summary>
        /// Gets the human-readable description returned with <see cref="Error"/>, or null if there is none.
        /// </summary>
        /// <remarks>
        /// The provider writes this text, and in a redirect back to the application anyone who sends the user a link can set
        /// it. It is not part of <see cref="Exception.Message"/>. Encode it before showing it in a page.
        /// </remarks>
        public string? ErrorDescription { get; }

        /// <summary>
        /// Creates the exception for an error code returned by the provider.
        /// </summary>
        /// <param name="error">The error code.</param>
        /// <param name="errorDescription">The description returned with the error code, or null if there is none.</param>
        /// <returns>The exception.</returns>
        internal static OAuth2Exception FromProviderError(string error, string? errorDescription)
        {
            return new OAuth2Exception($"The provider returned the error '{error}'.", error, errorDescription);
        }
    }
}
