using Polhem.OAuth2;

namespace OAuthConsole
{
    public class OAuthConfig
    {
        public GoogleOAuth2Options? GoogleOAuth { get; set; }
        public FacebookOAuth2Options? FacebookOAuth { get; set; }
        public LineOAuth2Options? LineOAuth { get; set; }
        public AzureOAuth2Options? AzureOAuth { get; set; }
        public Auth0OAuth2Options? Auth0OAuth { get; set; }
        public OktaOAuth2Options? OktaOAuth { get; set; }

        public OAuth2Options? Find(string providerName)
        {
            return providerName.ToUpperInvariant() switch
            {
                "GOOGLE" => GoogleOAuth,
                "FACEBOOK" => FacebookOAuth,
                "LINE" => LineOAuth,
                "AZURE" => AzureOAuth,
                "AUTH0" => Auth0OAuth,
                "OKTA" => OktaOAuth,
                _ => null
            };
        }
    }
}
