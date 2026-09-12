namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Google endpoints and scopes.
    /// </summary>
    public class GoogleOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Options"/> class.
        /// </summary>
        public GoogleOAuth2Options()
        {
            Scopes = new[] { "openid", "email", "profile" };
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/auth";
            TokenEndpoint = "https://oauth2.googleapis.com/token";
            UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";
        }
    }
}
