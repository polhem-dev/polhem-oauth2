using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Facebook OAuth2 provider.
    /// </summary>
    internal sealed class FacebookOAuth2Provider : OAuth2Provider
    {
        private static readonly char[] s_pathCharacters = { '/', '?', '#' };

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Facebook OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public FacebookOAuth2Provider(FacebookOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
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
            // Facebook separates scopes with commas rather than spaces.
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
