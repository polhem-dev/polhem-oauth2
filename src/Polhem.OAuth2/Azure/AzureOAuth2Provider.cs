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
            // The user information endpoint returns no object ID, so the user is identified by sub.
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                OAuth2Json.GetString(user, "name") ?? JoinNameParts(user),
                OAuth2Json.GetString(user, "email"),
                json);
        }

        // A personal Microsoft account returns no name claim. It returns the name parts as givenname and familyname, without
        // the underscores of the OpenID Connect given_name and family_name, so both spellings are read.
        private static string? JoinNameParts(JsonElement user)
        {
            string?[] parts =
            {
                OAuth2Json.GetString(user, "given_name") ?? OAuth2Json.GetString(user, "givenname"),
                OAuth2Json.GetString(user, "family_name") ?? OAuth2Json.GetString(user, "familyname")
            };
            string name = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
            return name.Length == 0 ? null : name;
        }
    }
}
