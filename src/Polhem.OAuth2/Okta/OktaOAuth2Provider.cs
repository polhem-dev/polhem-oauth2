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
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public OktaOAuth2Provider(OktaOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Okta";

        /// <inheritdoc/>
        /// <remarks>
        /// Okta could report the client of a token through introspection, with the client credentials of the back end. The
        /// check is not offered, because the ID token answers the same question without a request to the provider.
        /// </remarks>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                "Okta is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "or request an access token for an API of your own and validate it as a JSON Web Token whose audience is "
                + "that API, for example with JwtBearer authentication, instead of passing it to this method."));
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                GetOidcDisplayName(user) ?? OAuth2Json.GetString(user, "preferred_username"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
