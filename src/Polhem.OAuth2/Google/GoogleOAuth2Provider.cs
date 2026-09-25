using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Google OAuth2 provider.
    /// </summary>
    internal sealed class GoogleOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Google OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public GoogleOAuth2Provider(GoogleOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Google";

        /// <inheritdoc/>
        /// <remarks>ADR-004 keeps sending the client secret to Google from a public client as well.</remarks>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                GetOidcDisplayName(user),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
