using System.ComponentModel;
using System.Diagnostics;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// Opens a URL in the system's default browser.
    /// </summary>
    internal static class BrowserLauncher
    {
        public static void TryOpen(string url)
        {
            try
            {
                // UseShellExecute hands the URL to the operating system, which opens it with the registered browser.
                using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                Console.Error.WriteLine($"Could not open a browser: {ex.Message}");
            }
        }
    }
}
