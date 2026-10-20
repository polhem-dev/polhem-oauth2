using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Auth0 OAuth2 provider.
    /// </summary>
    internal sealed class Auth0OAuth2Provider : OpenIdConnectProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Auth0OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Auth0 OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public Auth0OAuth2Provider(Auth0OAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
            IdTokenIssuers = new[] { options.AuthorizationEndpoint, options.TokenEndpoint }
                .Select(endpoint => new Uri(endpoint).GetLeftPart(UriPartial.Authority) + "/")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <inheritdoc/>
        public override string ProviderName => "Auth0";

        /// <inheritdoc/>
        /// <remarks>
        /// Auth0 names the domain that the request went to, with a trailing slash, as its discovery document does. The issuer
        /// is therefore the scheme and host of the authorization or the token endpoint followed by a slash, which also covers
        /// a custom domain and endpoints that are set without <see cref="Auth0OAuth2Options.Domain"/>.
        /// </remarks>
        protected override IReadOnlyCollection<string> IdTokenIssuers { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Without an audience, which these options do not send, Auth0 issues an opaque access token that only its user
        /// information endpoint accepts. Auth0 offers no introspection endpoint, so the client of such a token cannot be
        /// learned from the provider; a token with an audience is a JSON Web Token for that API, which this library does not
        /// validate.
        /// </remarks>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                "Auth0 is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "checking its signature, issuer, expiry and audience, or request an access token for an API of your own and "
                + "validate it as a JSON Web Token whose audience is that API, for example with JwtBearer authentication, "
                + "instead of passing it to this method."));
        }

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "nickname");
        }
    }
}
