namespace OAuthSamples
{
    /// <summary>
    /// The <c>AppRelay</c> section of <c>OAuthConfig.json</c>: how a mobile application signs in through the back-end relay
    /// of the ASP.NET Core sample (ADR-006). The back end and the application read the same section.
    /// </summary>
    public sealed class OAuthAppRelay
    {
        internal OAuthAppRelay(Uri backendUrl, string redirectUri)
        {
            BackendUrl = backendUrl;
            RedirectUri = redirectUri;
        }

        /// <summary>Gets the base URL of the ASP.NET Core sample, which the application opens and redeems codes at.</summary>
        public Uri BackendUrl { get; }

        /// <summary>
        /// Gets the application redirect URI the back end returns a relayed sign-in to. The back end registers it with
        /// <c>AddOAuth2AppRelay</c>.
        /// </summary>
        public string RedirectUri { get; }
    }
}
