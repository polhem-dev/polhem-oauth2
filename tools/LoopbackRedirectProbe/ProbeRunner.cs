using System.Net.Sockets;
using System.Text.Json;
using OAuthSamples;
using Polhem.OAuth2;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// Runs one sign-in through the system browser and a loopback redirect, and reports whether the provider accepted it.
    /// </summary>
    internal static class ProbeRunner
    {
        public static async Task<int> RunAsync(string[] args)
        {
            if (!ProbeArguments.TryParse(args, out var arguments, out var error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine(ProbeArguments.Usage);
                return 2;
            }

            OAuth2Options options;
            LoopbackOAuth2Client client;
            try
            {
                options = OAuthConfig.Load(arguments.SettingsPath).GetClient(arguments.Provider, OAuthClientType.Desktop);
                if (arguments.RedirectUri is not null)
                    options.RedirectUri = arguments.RedirectUri.OriginalString;
                if (string.IsNullOrWhiteSpace(options.RedirectUri))
                    return Fail($"Set RedirectUri in 'Providers.{arguments.Provider}.{OAuthClientType.Desktop}' of the settings file, or pass --redirect.", 2);
                if (arguments.Scopes is not null)
                    options.Scopes = arguments.Scopes;
                if (arguments.OmitSecret)
                    options.ClientSecret = string.Empty;
                client = new LoopbackOAuth2Client(options) { Timeout = TimeSpan.FromSeconds(arguments.TimeoutSeconds) };
            }
            catch (FileNotFoundException ex)
            {
                return Fail(ex.Message, 2);
            }
            catch (InvalidDataException ex)
            {
                return Fail(ex.Message, 2);
            }
            catch (JsonException ex)
            {
                return Fail($"The settings file is not valid JSON: {ex.Message}", 2);
            }
            catch (ArgumentException ex)
            {
                return Fail(ex.Message, 2);
            }

            // The client calls this after it starts listening. The authorization URL carries the redirect URI with the port that was bound.
            client.OpenBrowser = url =>
            {
                Console.WriteLine($"Provider:     {arguments.Provider}");
                Console.WriteLine($"Redirect URI: {GetQueryValue(url, "redirect_uri")}");
                Console.WriteLine($"Scopes:       {GetQueryValue(url, "scope")}");
                Console.WriteLine($"Secret:       {(string.IsNullOrEmpty(options.ClientSecret) ? "none set" : "set, sent only to a provider that requires it from a public client")}");
                Console.WriteLine("Opening the system browser. If it does not open, visit this URL:");
                Console.WriteLine(url.AbsoluteUri);
                BrowserLauncher.TryOpen(url.AbsoluteUri);
                return Task.CompletedTask;
            };

            AuthorizationResult result;
            try
            {
                result = await client.SignInAsync();
            }
            catch (SocketException ex)
            {
                return Fail($"Cannot listen for {options.RedirectUri}: {ex.Message}", 2);
            }

            if (result.IsSuccess && result.UserInfo is { } user)
            {
                Console.WriteLine("The provider accepted the loopback redirect, and the code exchange succeeded.");
                Console.WriteLine($"  User ID:   {user.UserId}");
                Console.WriteLine($"  User name: {user.UserName}");
                Console.WriteLine($"  Email:     {user.Email}");
                WriteTokenFacts(result.Token);
                return arguments.Refresh ? await RefreshAsync(client, result.Token) : 0;
            }

            return result.Exception switch
            {
                TimeoutException => Fail(
                    "No redirect with the state of this sign-in arrived before the timeout. If the browser shows a redirect URI error, the provider rejected this URI.", 1),
                HttpRequestException ex => Fail($"The provider accepted the redirect, but the code exchange failed: {ex.Message}", 1),
                { } ex => Fail($"The sign-in failed: {ex.Message}", 1),
                null => Fail("The sign-in failed without an exception.", 1)
            };
        }

        // Describes the token response without any token: the type, the lifetime, the granted scopes as returned, and which
        // tokens came back.
        private static void WriteTokenFacts(TokenResponse token)
        {
            Console.WriteLine($"  Token type:    {token.TokenType ?? "(not returned)"}");
            Console.WriteLine($"  Expires in:    {(token.ExpiresIn is { } expiresIn ? $"{expiresIn.TotalSeconds:0} seconds" : "(not returned)")}");
            Console.WriteLine($"  Scope:         {token.Scope ?? "(not returned)"}");
            Console.WriteLine($"  Refresh token: {(token.RefreshToken is null ? "not issued" : "issued")}");
            Console.WriteLine($"  ID token:      {(token.IdToken is null ? "not issued" : "issued")}");
        }

        private static async Task<int> RefreshAsync(LoopbackOAuth2Client client, TokenResponse token)
        {
            if (token.RefreshToken is null)
            {
                Console.WriteLine("No refresh token was issued, so there is nothing to refresh.");
                return 0;
            }

            try
            {
                var refreshed = await client.RefreshTokenAsync(token.RefreshToken);
                Console.WriteLine("The refresh succeeded.");
                WriteTokenFacts(refreshed);
                return 0;
            }
            catch (OAuth2Exception ex)
            {
                return Fail($"The refresh failed with the provider error '{ex.Error}': {ex.Message}", 1);
            }
            catch (HttpRequestException ex)
            {
                return Fail($"The refresh failed: {ex.Message}", 1);
            }
            catch (NotSupportedException ex)
            {
                return Fail($"The refresh is not supported: {ex.Message}", 1);
            }
        }

        private static string? GetQueryValue(Uri url, string name)
        {
            foreach (string pair in url.Query.TrimStart('?').Split('&'))
            {
                int equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0 && string.Equals(pair[..equals], name, StringComparison.Ordinal))
                    return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
            return null;
        }

        private static int Fail(string message, int exitCode)
        {
            Console.Error.WriteLine(message);
            return exitCode;
        }
    }
}
