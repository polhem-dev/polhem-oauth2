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
        /// Gets the user's display name, or null if the response does not include one: the name the provider returns, or
        /// the given and family names joined when it returns those but no name. Auth0 falls back to the nickname and Okta
        /// to the preferred user name after that, and <see cref="OidcOAuth2Options"/> to the preferred user name and then
        /// the nickname. GitHub falls back to the login, and Sign in with Apple takes the name from
        /// <see cref="AuthorizationCallback.UserJson"/>, which Apple sends only the first time the user authorizes the
        /// application.
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        /// Gets the user's email address, or null if the response does not include one.
        /// </summary>
        /// <remarks>
        /// An OpenID Connect provider leaves the address out when its <c>email_verified</c> claim is false, and GitHub
        /// returns only a public or verified primary address. Other providers do not say whether the address is verified, so
        /// identify a user by <see cref="UserId"/> rather than by this address.
        /// </remarks>
        public string? Email { get; }

        /// <summary>
        /// Gets the raw JSON returned by the user information endpoint, or the payload of the ID token when the user is
        /// read from it, as by Sign in with Apple and by <see cref="OidcOAuth2Options"/> without a user information endpoint.
        /// </summary>
        /// <remarks>
        /// For GitHub it is the response of <c>/user</c>, which does not contain an address read from <c>/user/emails</c>.
        /// </remarks>
        public string RawJson { get; }
    }
}
