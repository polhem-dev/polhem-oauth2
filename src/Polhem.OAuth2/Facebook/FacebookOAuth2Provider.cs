using System.Net.Http.Headers;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Facebook OAuth2 provider.
    /// </summary>
    internal sealed class FacebookOAuth2Provider : OAuth2Provider
    {
        // https://developers.facebook.com/docs/graph-api/reference/debug_token
        private const string DebugTokenEndpoint = $"https://graph.facebook.com/{FacebookOAuth2Options.GraphApiVersion}/debug_token";

        private static readonly char[] s_pathCharacters = { '/', '?', '#' };

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Facebook OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public FacebookOAuth2Provider(FacebookOAuth2Options options, Func<HttpClient>? httpClientFactory)
            : base(options, httpClientFactory, DebugTokenEndpoint)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "Facebook";

        /// <inheritdoc/>
        /// <remarks>Facebook Login does not issue refresh tokens.</remarks>
        protected override bool SupportsRefreshToken => false;

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAuthorizationParameters(string state, string redirectUri, string? codeChallenge)
        {
            var parameters = base.GetAuthorizationParameters(state, redirectUri, codeChallenge);
            // Facebook documents both commas and spaces as separators. Commas are kept because they are what this provider has
            // always sent, and what the sign-ins recorded in ADR-004 and ADR-006 used.
            parameters["scope"] = string.Join(",", Options.Scopes);
            return parameters;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Facebook redirects an application to <c>fb&lt;app id&gt;://authorize/</c>, with a trailing slash, and accepts the
        /// code only when the token request names that URI (ADR-006), so the slash is added to such a URI that has none.
        /// </remarks>
        protected override string GetTokenRedirectUri(string redirectUri)
        {
            return IsAppSchemeWithoutPath(redirectUri) ? redirectUri + "/" : redirectUri;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The Graph API reports an error as an object with a numeric <c>code</c>, a <c>type</c> and a <c>message</c>, not as
        /// the strings of RFC 6749, section 5.2. The code identifies the failure, so it becomes
        /// <see cref="OAuth2Exception.Error"/>, and the type stands in for a response that has no code.
        /// </remarks>
        protected override OAuth2Exception? ReadError(JsonElement response)
        {
            if (!response.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return base.ReadError(response);

            string? code = error.TryGetProperty("code", out var number) && number.ValueKind == JsonValueKind.Number
                ? number.GetRawText()
                : OAuth2Json.GetProtocolString(error, "type");
            return code is { Length: > 0 }
                ? OAuth2Exception.FromProviderError(code, OAuth2Json.GetProtocolString(error, "message"))
                : null;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// The endpoint needs an app access token, which is the app ID and the app secret joined with a vertical bar, so the
        /// options must have a client secret. It travels in the Authorization header rather than in the query.
        /// </para>
        /// <para>
        /// NOTE: The token being checked travels in the query, where the systems it passes through may log it, because the
        /// Graph API documents only GET for this endpoint. Only a valid user access token names its app: an app access
        /// token names one too, but signs in no user.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The options have no client secret.</exception>
        protected override async Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            if (Options.ClientSecret is not { Length: > 0 } appSecret)
            {
                throw new InvalidOperationException(
                    $"Facebook checks an access token with the app secret, so {nameof(OAuth2Options.ClientSecret)} must be set.");
            }

            string url = AppendQuery(DebugTokenEndpoint, "input_token=" + Uri.EscapeDataString(accessToken));
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ClientId + "|" + appSecret);
                using (var document = await SendTokenVerificationAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                        return Array.Empty<string>();
                    // An invalid or expired token is reported with status 200 and the error inside data.
                    if (!data.TryGetProperty("is_valid", out var isValid) || isValid.ValueKind != JsonValueKind.True)
                        throw ReadError(data) ?? new OAuth2Exception("Facebook reports that the access token is not valid.");
                    if (!string.Equals(OAuth2Json.GetProtocolString(data, "type"), "USER", StringComparison.Ordinal))
                        throw new OAuth2Exception("The access token is not a Facebook user access token.");

                    return OAuth2Json.GetProtocolString(data, "app_id") is { } appId ? new[] { appId } : Array.Empty<string>();
                }
            }
        }

        /// <inheritdoc/>
        protected override string GetUserInfoUrl()
        {
            // The Graph API returns only the fields that are asked for.
            return AppendQuery(Options.UserInfoEndpoint, "fields=" + Uri.EscapeDataString("id,name,first_name,last_name,email"));
        }

        // True for fb<digits>://<host> with no path, query or fragment, the form Facebook requires of an application.
        private static bool IsAppSchemeWithoutPath(string redirectUri)
        {
            const string Separator = "://";
            int separator = redirectUri.IndexOf(Separator, StringComparison.Ordinal);
            if (separator <= 2 || !redirectUri.StartsWith("fb", StringComparison.Ordinal))
                return false;
            for (int i = 2; i < separator; i++)
            {
                if (redirectUri[i] < '0' || redirectUri[i] > '9')
                    return false;
            }
            string rest = redirectUri.Substring(separator + Separator.Length);
            return rest.Length > 0 && rest.IndexOfAny(s_pathCharacters) < 0;
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, "id"),
                OAuth2Json.GetString(user, "name") ?? JoinNameParts(user, "first_name", "last_name"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
