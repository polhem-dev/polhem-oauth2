namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Facebook endpoints and scopes.
    /// </summary>
    public class FacebookOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Options"/> class.
        /// </summary>
        public FacebookOAuth2Options()
        {
            Scopes = new[] { "public_profile", "email" };
            AuthorizationEndpoint = "https://www.facebook.com/v18.0/dialog/oauth";
            TokenEndpoint = "https://graph.facebook.com/v18.0/oauth/access_token";
            UserInfoEndpoint = "https://graph.facebook.com/me";
        }
    }
}
