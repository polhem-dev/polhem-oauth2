namespace Polhem.OAuth2
{
    /// <summary>
    /// A new sign-in created by <see cref="OAuth2Client.CreateAuthorizationRequest()"/>: the URL to send the user to, and
    /// the values to keep until the provider redirects back.
    /// </summary>
    public sealed class AuthorizationRequest
    {
        internal AuthorizationRequest(string url, PendingAuthorization pending)
        {
            Url = url;
            Pending = pending;
        }

        /// <summary>
        /// Gets the authorization URL to send the user to.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets the values to keep until the callback and pass to <see cref="OAuth2Client.CompleteAuthorizationAsync"/>.
        /// </summary>
        public PendingAuthorization Pending { get; }
    }
}
