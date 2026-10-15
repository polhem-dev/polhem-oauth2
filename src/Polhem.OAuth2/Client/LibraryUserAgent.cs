using System.Net.Http.Headers;
using System.Reflection;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The <c>User-Agent</c> product that names this library and its version in each request to a provider.
    /// </summary>
    internal static class LibraryUserAgent
    {
        private const string ProductName = "Polhem.OAuth2";

        private static readonly string? s_version = ReadVersion();

        /// <summary>
        /// Creates the product, <c>Polhem.OAuth2/&lt;version&gt;</c>, or the name alone when the assembly version cannot
        /// be written in the header.
        /// </summary>
        /// <returns>A new instance for one request.</returns>
        public static ProductInfoHeaderValue Create()
        {
            return new ProductInfoHeaderValue(ProductName, s_version);
        }

        // The informational version is the package version, followed by `+` and the commit when SourceLink adds it. The commit
        // is left out. A version that is not a valid HTTP token is dropped, because a malformed header would fail every request.
        private static string? ReadVersion()
        {
            var assembly = typeof(LibraryUserAgent).Assembly;
            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3);
            if (version is null)
                return null;

            int metadata = version.IndexOf('+');
            if (metadata >= 0)
                version = version.Substring(0, metadata);
            return ProductHeaderValue.TryParse(ProductName + "/" + version, out _) ? version : null;
        }
    }
}
