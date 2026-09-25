namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the LINE Login endpoints and scopes.
    /// </summary>
    public sealed class LineOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineOAuth2Options"/> class.
        /// </summary>
        public LineOAuth2Options()
        {
            Scopes = new[] { "profile", "openid", "email" };
            AuthorizationEndpoint = "https://access.line.me/oauth2/v2.1/authorize";
            TokenEndpoint = "https://api.line.me/oauth2/v2.1/token";
            UserInfoEndpoint = "https://api.line.me/v2/profile";
        }

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new LineOAuth2Provider(this, httpClientFactory);
        }
    }
}
