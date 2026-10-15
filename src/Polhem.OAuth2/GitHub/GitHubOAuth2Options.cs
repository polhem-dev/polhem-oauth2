namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the GitHub endpoints and scopes, for an OAuth App or a GitHub App.
    /// </summary>
    /// <remarks>
    /// An OAuth App issues access tokens that do not expire and no refresh token. A GitHub App with expiring user tokens
    /// issues a refresh token as well.
    /// </remarks>
    public sealed class GitHubOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubOAuth2Options"/> class.
        /// </summary>
        public GitHubOAuth2Options()
        {
            Scopes = new[] { "read:user", "user:email" };
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            TokenEndpoint = "https://github.com/login/oauth/access_token";
            UserInfoEndpoint = "https://api.github.com/user";
        }

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new GitHubOAuth2Provider(this, httpClientFactory);
        }
    }
}
