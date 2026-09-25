using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Auth0 OAuth2 provider.
    /// </summary>
    internal sealed class Auth0OAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Auth0OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Auth0 OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public Auth0OAuth2Provider(Auth0OAuth2Options options, Func<HttpClient>? httpClientFactory = null) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Auth0";

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                OAuth2Json.GetString(user, "name") ?? OAuth2Json.GetString(user, "nickname"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
