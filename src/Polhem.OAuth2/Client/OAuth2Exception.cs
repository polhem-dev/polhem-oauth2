namespace Polhem.OAuth2
{
    /// <summary>
    /// The exception thrown when an OAuth2 exchange fails for a protocol reason, such as an empty authorization code
    /// or a token response without an access token.
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
    }
}
