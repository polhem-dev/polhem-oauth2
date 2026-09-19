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
        /// <exception cref="System.ComponentModel.Win32Exception">The operating system cannot start a browser.</exception>
        /// <exception cref="PlatformNotSupportedException">The platform cannot start a process, as on iOS.</exception>
        public static void Open(Uri url)
        {
            // The operating system shell also starts programs and opens files, so anything other than a web URL is refused.
            if (!IsWebUrl(url))
                throw new InvalidOperationException("The authorization URL must be an absolute http or https URL.");

            // AbsoluteUri escapes characters such as quotation marks, so the URL cannot end the argument that the shell receives.
            // Only the handle of the started process is released; the browser keeps running.
            using var process = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }

        /// <summary>
        /// Checks whether a URI is an absolute http or https URL.
        /// </summary>
        /// <param name="url">The URI to check.</param>
        /// <returns><see langword="true"/> for an absolute http or https URL; otherwise, <see langword="false"/>.</returns>
        public static bool IsWebUrl(Uri url)
        {
            return url.IsAbsoluteUri
                && (string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                    || string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));
        }
    }
}
