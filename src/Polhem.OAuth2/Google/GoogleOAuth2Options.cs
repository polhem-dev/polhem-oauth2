namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Google endpoints and scopes.
    /// </summary>
    public sealed class GoogleOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Options"/> class.
        /// </summary>
        public GoogleOAuth2Options()
        {
            // The endpoints published in https://accounts.google.com/.well-known/openid-configuration.
            Scopes = new[] { "openid", "email", "profile" };
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            TokenEndpoint = "https://oauth2.googleapis.com/token";
            UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        }
    }
}
