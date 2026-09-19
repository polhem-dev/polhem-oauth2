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
        /// <param name="httpClient">The HTTP client for requests to the provider, or null to use a shared instance.</param>
        public FacebookOAuth2Provider(FacebookOAuth2Options options, HttpClient? httpClient = null) : base(options, httpClient)
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
        protected override string GetUserInfoUrl()
        {
            // The Graph API returns only the fields that are asked for.
            return Options.UserInfoEndpoint + "?fields=" + Uri.EscapeDataString("id,name,email");
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
                OAuth2Json.GetString(user, "name"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
