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
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "preferred_username");
        }
    }
}
