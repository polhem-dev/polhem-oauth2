namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// Settings of the back-end relay, through which a mobile application signs in with this ASP.NET Core application
    /// (ADR-006). Register them with
    /// <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2AppRelay"/>.
    /// </summary>
    public sealed class OAuth2AppRelayOptions
    {
        /// <summary>
        /// Gets the application redirect URIs that a relayed sign-in may return to, such as
        /// <c>com.example.app:/signin</c>. A requested URI must equal one of them exactly.
        /// </summary>
        /// <remarks>
        /// Each URI must be an absolute https URI or a custom scheme URI without a fragment, the same rule that
        /// <see cref="AppOAuth2Client"/> applies to its redirect URI.
        /// </remarks>
        public IList<string> AppRedirectUris { get; } = [];

        /// <summary>
        /// Gets or sets how long the code that a relayed sign-in returns to the application can be redeemed. The default is
        /// one minute.
        /// </summary>
        public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(1);
    }
}
