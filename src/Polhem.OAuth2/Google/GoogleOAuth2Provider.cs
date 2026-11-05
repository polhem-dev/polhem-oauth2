using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Google OAuth2 provider.
    /// </summary>
    internal sealed class GoogleOAuth2Provider : OpenIdConnectProvider
    {
        private static readonly string[] s_issuers = { "https://accounts.google.com", "accounts.google.com" };

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Google OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public GoogleOAuth2Provider(GoogleOAuth2Options options, Func<HttpClient>? httpClientFactory)
            : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Google";

        /// <inheritdoc/>
        /// <remarks>ADR-004 keeps sending the client secret to Google from a public client as well.</remarks>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        /// <remarks>
        /// https://developers.google.com/identity/openid-connect/openid-connect#validatinganidtoken names both values, with and
        /// without the scheme.
        /// </remarks>
        protected override IReadOnlyCollection<string> IdTokenIssuers => s_issuers;

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return null;
        }
    }
}
