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
            LoopbackListener listener;
            try
            {
                options = ProbeSettings.Load(arguments.SettingsPath, arguments.Provider);
                listener = LoopbackListener.Start(arguments.RedirectUri);
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
            catch (SocketException ex)
            {
                return Fail($"Cannot listen for {arguments.RedirectUri}: {ex.Message}", 2);
            }

            using (listener)
            {
                options.RedirectUri = listener.RedirectUri.AbsoluteUri;
                options.UsePkce = arguments.UsePkce;

                var client = new ProbeClient(options);
                string authorizationUrl = client.GetAuthorizationUrl(Pkce.GenerateCodeVerifier());

                Console.WriteLine($"Provider:     {arguments.Provider}");
                Console.WriteLine($"Redirect URI: {options.RedirectUri}");
                Console.WriteLine($"PKCE:         {(arguments.UsePkce ? "on" : "off")}");
                Console.WriteLine("Opening the system browser. If it does not open, visit this URL:");
                Console.WriteLine(authorizationUrl);
                BrowserLauncher.TryOpen(authorizationUrl);

                LoopbackCallback callback;
                try
                {
                    callback = await listener.WaitForCallbackAsync(TimeSpan.FromSeconds(arguments.TimeoutSeconds));
                }
                catch (TimeoutException)
                {
                    return Fail("No callback arrived before the timeout. If the browser shows a redirect URI error, the provider rejected this URI.", 1);
                }

                if (callback.Error is { } providerError)
                    return Fail($"The provider returned an error to the callback: {providerError} {callback.ErrorDescription}".TrimEnd(), 1);

                if (!client.ValidateState(callback.State))
                    return Fail("The state returned to the callback does not match.", 1);

                var result = await client.ValidateAuthorization(callback.Code);
                if (!result.IsSuccess || result.UserInfo is not { } user)
                    return Fail($"The provider accepted the redirect, but the code exchange failed: {result.Exception?.Message}", 1);

                Console.WriteLine("The provider accepted the loopback redirect, and the code exchange succeeded.");
                Console.WriteLine($"  User ID:   {user.UserId}");
                Console.WriteLine($"  User name: {user.UserName}");
                Console.WriteLine($"  Email:     {user.Email}");
                return 0;
            }
        }

        private static int Fail(string message, int exitCode)
        {
            Console.Error.WriteLine(message);
            return exitCode;
        }
    }
}
