using Newtonsoft.Json;
using OAuthAspNetCore.Models;
using Polhem.OAuth2;

namespace OAuthAspNetCore.Extensions
{
    public static class OAuth2RegistrationHelper
    {
        public static void AddOAuth2Clients(IServiceCollection services, string configPath)
        {
            var config = LoadOAuthConfig(configPath);
            AddIfConfigured(services, "Google", config.GoogleOAuth);
            AddIfConfigured(services, "Facebook", config.FacebookOAuth);
            AddIfConfigured(services, "Line", config.LineOAuth);
            AddIfConfigured(services, "Azure", config.AzureOAuth);
            AddIfConfigured(services, "Auth0", config.Auth0OAuth);
            AddIfConfigured(services, "Okta", config.OktaOAuth);
        }

        private static OAuthConfig LoadOAuthConfig(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Configuration file not found.", filePath);

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<OAuthConfig>(json) ?? new OAuthConfig();
        }

        private static void AddIfConfigured(IServiceCollection services, string name, OAuth2Options? options)
        {
            if (options != null)
            {
                services.AddOAuth2Client(name, options);
            }
        }
    }
}
