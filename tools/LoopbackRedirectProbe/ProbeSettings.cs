using System.Text.Json;
using Polhem.OAuth2;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// Reads the client credentials of one provider from the local settings file.
    /// </summary>
    internal static class ProbeSettings
    {
        public static OAuth2Options Load(string path, string provider)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"The settings file '{path}' was not found. Copy probe.settings.example.json to probe.settings.json and fill it in.",
                    path);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(provider, out var section))
                throw new InvalidDataException($"The settings file has no '{provider}' section.");

            string json = section.GetRawText();
            OAuth2Options? options = provider switch
            {
                "Google" => JsonSerializer.Deserialize<GoogleOAuth2Options>(json),
                "Facebook" => JsonSerializer.Deserialize<FacebookOAuth2Options>(json),
                "Line" => JsonSerializer.Deserialize<LineOAuth2Options>(json),
                "Azure" => JsonSerializer.Deserialize<AzureOAuth2Options>(json),
                "Auth0" => JsonSerializer.Deserialize<Auth0OAuth2Options>(json),
                "Okta" => JsonSerializer.Deserialize<OktaOAuth2Options>(json),
                _ => null
            };

            return options ?? throw new InvalidDataException($"The '{provider}' section could not be read.");
        }
    }
}
