using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Microsoft Entra ID OAuth2 provider.
    /// </summary>
    internal sealed class AzureOAuth2Provider : OpenIdConnectProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Microsoft Entra ID OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public AzureOAuth2Provider(AzureOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Azure";

        /// <inheritdoc/>
        /// <remarks>
        /// The issuer is not checked. It names the tenant of the user, which the <c>common</c>, <c>organizations</c> and
        /// <c>consumers</c> tenants do not fix in advance; the audience is still checked. The user information endpoint
        /// returns no object ID, so the user is identified by <c>sub</c>, which it shares with the ID token.
        /// </remarks>
        protected override IReadOnlyCollection<string> IdTokenIssuers => Array.Empty<string>();

        /// <inheritdoc/>
        /// <remarks>
        /// The client ID is a GUID, which Entra ID accepts in any case and writes in lower case in its tokens, so it is compared
        /// as a GUID.
        /// </remarks>
        protected override IEqualityComparer<string> ClientIdComparer => EntraClientIdComparer.Instance;

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            // A personal Microsoft account returns no name claim, and its name parts as givenname and familyname, without the
            // underscores of OpenID Connect.
            return JoinNameParts(user, "givenname", "familyname");
        }
    }
}
