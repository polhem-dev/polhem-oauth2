namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Microsoft Entra ID endpoints and scopes.
    /// </summary>
    public class AzureOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOAuth2Options"/> class.
        /// </summary>
        public AzureOAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
            AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
            TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
            UserInfoEndpoint = "https://graph.microsoft.com/oidc/userinfo";
        }
    }
}
