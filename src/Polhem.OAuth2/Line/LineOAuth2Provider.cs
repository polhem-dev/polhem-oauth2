using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The LINE Login OAuth2 provider.
    /// </summary>
    internal sealed class LineOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The LINE OAuth2 options.</param>
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        public LineOAuth2Provider(LineOAuth2Options options, HttpClient? httpClient = null) : base(options, httpClient)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "LINE";

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo
            {
                UserId = OAuth2Json.GetString(user, "userId"),
                UserName = OAuth2Json.GetString(user, "displayName"),
                // The profile response has no email field. LINE puts the address in the ID token, and only when the channel
                // has permission to read it, the email scope was requested, and the user agreed to share it.
                Email = token?.IdToken is { } idToken ? LineIdToken.ReadEmail(idToken, Options.ClientId) : null,
                RawJson = json
            };
        }
    }
}
