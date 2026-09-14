using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Okta OAuth2 provider.
    /// </summary>
    internal sealed class OktaOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OktaOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Okta OAuth2 options.</param>
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        public OktaOAuth2Provider(OktaOAuth2Options options, HttpClient? httpClient = null) : base(options, httpClient)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Okta";

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo
            {
                UserId = OAuth2Json.GetString(user, "sub"),
                UserName = OAuth2Json.GetString(user, "name") ?? OAuth2Json.GetString(user, "preferred_username"),
                Email = OAuth2Json.GetString(user, "email"),
                RawJson = json
            };
        }
    }
}
