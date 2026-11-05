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
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "nickname");
        }
    }
}
