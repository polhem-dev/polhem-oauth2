using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Facebook OAuth2 provider.
    /// </summary>
    internal sealed class FacebookOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Facebook OAuth2 options.</param>
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        public FacebookOAuth2Provider(FacebookOAuth2Options options, HttpClient? httpClient = null) : base(options, httpClient)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Facebook";

        /// <inheritdoc/>
        /// <remarks>Facebook Login does not issue refresh tokens.</remarks>
        protected override bool SupportsRefreshToken => false;

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAuthorizationParameters(string state, string redirectUri, string? codeChallenge)
        {
            var parameters = base.GetAuthorizationParameters(state, redirectUri, codeChallenge);
            // Facebook separates scopes with commas rather than spaces.
            parameters["scope"] = string.Join(",", Options.Scopes);
            return parameters;
        }

        /// <inheritdoc/>
        protected override string GetUserInfoUrl()
        {
            // The Graph API returns only the fields that are asked for.
            return Options.UserInfoEndpoint + "?fields=" + Uri.EscapeDataString("id,name,email");
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo
            {
                UserId = OAuth2Json.GetString(user, "id"),
                UserName = OAuth2Json.GetString(user, "name"),
                Email = OAuth2Json.GetString(user, "email"),
                RawJson = json
            };
        }
    }
}
