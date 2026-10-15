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
        public Auth0OAuth2Provider(Auth0OAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Auth0";

        /// <inheritdoc/>
        /// <remarks>
        /// Without an audience, which these options do not send, Auth0 issues an opaque access token that only its user
        /// information endpoint accepts, and it offers no introspection endpoint, so the client of a token cannot be checked.
        /// </remarks>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                "Auth0 is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "or request an access token for an API of your own and validate it as a JSON Web Token whose audience is "
                + "that API, for example with JwtBearer authentication, instead of passing it to this method."));
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                GetOidcDisplayName(user) ?? OAuth2Json.GetString(user, "nickname"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
