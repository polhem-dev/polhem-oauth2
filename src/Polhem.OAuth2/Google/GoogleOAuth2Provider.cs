using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Google OAuth2 provider.
    /// </summary>
    internal sealed class GoogleOAuth2Provider : OAuth2Provider
    {
        // https://docs.cloud.google.com/docs/authentication/token-types describes the response for an access token.
        private const string TokenInfoEndpoint = "https://oauth2.googleapis.com/tokeninfo";

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Google OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public GoogleOAuth2Provider(GoogleOAuth2Options options, Func<HttpClient>? httpClientFactory)
            : base(options, httpClientFactory, TokenInfoEndpoint)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Google";

        /// <inheritdoc/>
        /// <remarks>ADR-004 keeps sending the client secret to Google from a public client as well.</remarks>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// The token is sent in a form body rather than in the query, which the endpoint also accepts, so that it does not
        /// appear in the logs of the systems it passes through.
        /// </para>
        /// <para>
        /// <c>aud</c> names the client the token is for, and <c>azp</c> the client that requested it. Clients of one Google
        /// Cloud project can obtain tokens for each other, so the two can differ, and both are returned to be checked.
        /// </para>
        /// </remarks>
        protected override async Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, TokenInfoEndpoint))
            {
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["access_token"] = accessToken });
                using (var document = await SendTokenVerificationAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var root = document.RootElement;
                    // A response without an audience names no client, whatever else it holds.
                    if (OAuth2Json.GetProtocolString(root, "aud") is not { } audience)
                        return Array.Empty<string>();
                    if (!root.TryGetProperty("azp", out _))
                        return new[] { audience };
                    // An authorized party that is not a string cannot be checked, so the response names no client.
                    return OAuth2Json.GetProtocolString(root, "azp") is { } authorizedParty
                        ? new[] { audience, authorizedParty }
                        : Array.Empty<string>();
                }
            }
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "sub"),
                GetOidcDisplayName(user),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
