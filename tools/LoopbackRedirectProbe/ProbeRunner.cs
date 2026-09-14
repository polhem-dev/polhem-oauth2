using System.Net.Sockets;
using System.Text.Json;
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
                options = ProbeSettings.Load(arguments.SettingsPath, arguments.Provider);
                if (arguments.RedirectUri is not null)
                    options.RedirectUri = arguments.RedirectUri.OriginalString;
                if (string.IsNullOrWhiteSpace(options.RedirectUri))
                    return Fail($"Set RedirectUri in the '{arguments.Provider}' section of the settings file, or pass --redirect.", 2);
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

            // The client calls this after it starts listening, when the redirect URI carries the port that was bound.
            client.OpenBrowser = url =>
            {
                Console.WriteLine($"Provider:     {arguments.Provider}");
                Console.WriteLine($"Redirect URI: {options.RedirectUri}");
                Console.WriteLine("Opening the system browser. If it does not open, visit this URL:");
                Console.WriteLine(url);
                BrowserLauncher.TryOpen(url);
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
                return 0;
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

        private static int Fail(string message, int exitCode)
        {
            Console.Error.WriteLine(message);
            return exitCode;
        }
    }
}
