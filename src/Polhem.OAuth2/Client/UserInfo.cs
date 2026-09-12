namespace Polhem.OAuth2
{
    /// <summary>
    /// The user information returned by a provider.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Gets the user identifier issued by the provider, or null if the response does not include one.
        /// </summary>
        public string? UserId { get; protected internal set; }

        /// <summary>
        /// Gets the user's display name, or null if the response does not include one.
        /// </summary>
        public string? UserName { get; protected internal set; }

        /// <summary>
        /// Gets the user's email address, or null if the response does not include one.
        /// </summary>
        public string? Email { get; protected internal set; }

        /// <summary>
        /// Gets the raw JSON returned by the user information endpoint.
        /// </summary>
        public string RawJson { get; protected internal set; } = string.Empty;
    }
}
