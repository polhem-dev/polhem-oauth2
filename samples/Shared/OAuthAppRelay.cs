#nullable enable

namespace OAuthSamples
{
    /// <summary>
    /// The <c>AppRelay</c> section of <c>OAuthConfig.json</c>: how a mobile or desktop application signs in through the
    /// back-end relay of the ASP.NET Core sample (ADR-006). The back end and the applications read the same section.
    /// </summary>
    public sealed class OAuthAppRelay
    {
        internal OAuthAppRelay(Uri backendUrl, string redirectUri, string? desktopRedirectUri)
        {
            BackendUrl = backendUrl;
            RedirectUri = redirectUri;
            DesktopRedirectUri = desktopRedirectUri;
        }

        /// <summary>Gets the base URL of the ASP.NET Core sample, which the application opens and redeems codes at.</summary>
        public Uri BackendUrl { get; }

        /// <summary>
        /// Gets the application redirect URI the back end returns a relayed sign-in to. The back end registers it with
        /// <c>AddOAuth2AppRelay</c>.
        /// </summary>
        public string RedirectUri { get; }

        /// <summary>
        /// Gets the loopback redirect URI the back end returns a relayed sign-in of a desktop application to, such as
        /// <c>http://127.0.0.1/callback</c>, or null when desktop applications sign in directly. It has no port, so the back
        /// end accepts any port, and the desktop application listens on a free one.
        /// </summary>
        public string? DesktopRedirectUri { get; }
    }
}
