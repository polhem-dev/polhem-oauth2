using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Okta OAuth2 provider.
    /// </summary>
    internal sealed class OktaOAuth2Provider : OpenIdConnectProvider
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
        /// The issuer is not checked. The issuer mode of an authorization server decides whether a token names the Okta domain
        /// or a custom domain, so it can differ from <see cref="OktaOAuth2Options.Domain"/>; the audience is still checked.
        /// </remarks>
        protected override IReadOnlyCollection<string> IdTokenIssuers => Array.Empty<string>();

        /// <inheritdoc/>
        /// <remarks>
        /// Okta could report the client of a token through introspection, with the client credentials of the back end. The
        /// check is not offered, because the ID token answers the same question without a request for each token.
        /// </remarks>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                "Okta is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "checking its signature, issuer, expiry and audience, or request an access token from a custom authorization "
                + "server for an API of your own and validate it as a JSON Web Token whose audience is that API, for example "
                + "with JwtBearer authentication, instead of passing it to this method."));
        }

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "preferred_username");
        }
    }
}
