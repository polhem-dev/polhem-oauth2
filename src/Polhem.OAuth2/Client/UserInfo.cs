namespace Polhem.OAuth2
{
    /// <summary>
    /// The user information returned by a provider.
    /// </summary>
    /// <remarks>
    /// Identify a user by <see cref="UserId"/> together with the provider name, not by <see cref="Email"/>: an email address
    /// can change.
    /// </remarks>
    public sealed class UserInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserInfo"/> class.
        /// </summary>
        /// <param name="userId">The user identifier issued by the provider, or null if the response does not include one.</param>
        /// <param name="userName">The user's display name, or null if the response does not include one.</param>
        /// <param name="email">The user's email address, or null if the response does not include one.</param>
        /// <param name="rawJson">The raw JSON returned by the user information endpoint.</param>
        /// <exception cref="ArgumentNullException"><paramref name="rawJson"/> is null.</exception>
        public UserInfo(string? userId, string? userName, string? email, string rawJson)
        {
            UserId = userId;
            UserName = userName;
            Email = email;
            RawJson = rawJson ?? throw new ArgumentNullException(nameof(rawJson));
        }

        /// <summary>
        /// Gets the user identifier issued by the provider, or null if the response does not include one.
        /// </summary>
        public string? UserId { get; }

        /// <summary>
        /// Gets the user's display name, or null if the response does not include one.
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        /// Gets the user's email address, or null if the response does not include one.
        /// </summary>
        public string? Email { get; }

        /// <summary>
        /// Gets the raw JSON returned by the user information endpoint.
        /// </summary>
        public string RawJson { get; }
    }
}
