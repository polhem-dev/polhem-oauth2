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
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public AzureOAuth2Provider(AzureOAuth2Options options, Func<HttpClient>? httpClientFactory = null) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Azure";

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            // The user information endpoint returns no object ID, so the user is identified by sub. A personal Microsoft
            // account returns no name claim, and its name parts as givenname and familyname, without the underscores of
            // OpenID Connect, so that spelling is read as well.
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                GetOidcDisplayName(user) ?? JoinNameParts(user, "givenname", "familyname"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
