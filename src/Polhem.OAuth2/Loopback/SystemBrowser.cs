using System.Diagnostics;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Opens a URL in the default browser of the operating system.
    /// </summary>
    internal static class SystemBrowser
    {
        /// <summary>
        /// Opens a web URL in the default browser.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <exception cref="InvalidOperationException"><paramref name="url"/> is not an absolute http or https URL.</exception>
        public static void Open(string url)
        {
            // The operating system shell also starts programs and opens files, so anything other than a web URL is refused.
            if (!IsWebUrl(url))
                throw new InvalidOperationException("The authorization URL must be an absolute http or https URL.");

            // Only the handle of the started process is released; the browser keeps running.
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        /// <summary>
        /// Checks whether a string is an absolute http or https URL.
        /// </summary>
        /// <param name="url">The string to check.</param>
        /// <returns><see langword="true"/> for an absolute http or https URL; otherwise, <see langword="false"/>.</returns>
        public static bool IsWebUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));
        }
    }
}
