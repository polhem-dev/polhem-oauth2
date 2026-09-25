namespace Polhem.OAuth2
{
    /// <summary>
    /// A pending sign-in read from its cookie by <see cref="PendingAuthorizationCookie.Deserialize"/>.
    /// </summary>
    internal sealed class PendingSignIn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PendingSignIn"/> class.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="pending">The values kept until the callback.</param>
        /// <param name="appRedirectUri">The application redirect URI of a relayed sign-in, or null for a web sign-in.</param>
        /// <param name="appCodeChallenge">The code challenge of a relayed sign-in, or null for a web sign-in.</param>
        public PendingSignIn(string clientName, PendingAuthorization pending, string? appRedirectUri, string? appCodeChallenge)
        {
            ClientName = clientName;
            Pending = pending;
            AppRedirectUri = appRedirectUri;
            AppCodeChallenge = appCodeChallenge;
        }

        /// <summary>
        /// Gets the name the client is registered under.
        /// </summary>
        public string ClientName { get; }

        /// <summary>
        /// Gets the values kept until the callback.
        /// </summary>
        public PendingAuthorization Pending { get; }

        /// <summary>
        /// Gets the application redirect URI of a sign-in relayed to an application (ADR-006), or null for a web sign-in.
        /// </summary>
        public string? AppRedirectUri { get; }

        /// <summary>
        /// Gets the S256 code challenge of the application of a relayed sign-in, or null for a web sign-in.
        /// </summary>
        public string? AppCodeChallenge { get; }
    }
}
