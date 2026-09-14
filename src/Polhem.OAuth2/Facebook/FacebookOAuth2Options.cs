namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Facebook endpoints and scopes.
    /// </summary>
    public sealed class FacebookOAuth2Options : OAuth2Options
    {
        // Meta retires each Graph API version on a published date, listed at
        // https://developers.facebook.com/docs/graph-api/changelog/versions. All three endpoints use the same version.
        private const string GraphApiVersion = "v26.0";

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Options"/> class.
        /// </summary>
        public FacebookOAuth2Options()
        {
            Scopes = new[] { "public_profile", "email" };
            AuthorizationEndpoint = $"https://www.facebook.com/{GraphApiVersion}/dialog/oauth";
            TokenEndpoint = $"https://graph.facebook.com/{GraphApiVersion}/oauth/access_token";
            UserInfoEndpoint = $"https://graph.facebook.com/{GraphApiVersion}/me";
        }
    }
}
