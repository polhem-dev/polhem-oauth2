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
        /// Each URI must pass <see cref="OAuth2Options.IsAppRedirectUri"/>, the rule that <see cref="AppOAuth2Client"/> applies
        /// to its redirect URI.
        /// </remarks>
        public IList<string> AppRedirectUris { get; } = [];

        /// <summary>
        /// Gets or sets how long the code that a relayed sign-in returns to the application can be redeemed. The default is
        /// one minute, and the longest is 10 minutes, which RFC 6749, section 4.1.2, recommends for an authorization code.
        /// </summary>
        /// <remarks>An application redeems the code as soon as it receives it, so a short lifetime costs nothing.</remarks>
        public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(1);
    }
}
