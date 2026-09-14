using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Microsoft Entra ID OAuth2 provider.
    /// </summary>
    internal sealed class AzureOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Microsoft Entra ID OAuth2 options.</param>
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        public AzureOAuth2Provider(AzureOAuth2Options options, HttpClient? httpClient = null) : base(options, httpClient)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Azure";

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            // The OpenID Connect user information endpoint returns the standard claims only, so the user is identified by sub.
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                OAuth2Json.GetString(user, "name"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
