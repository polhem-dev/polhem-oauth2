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

        /// <summary>
        /// Sends a request with an HTTP client after adding this library to its <c>User-Agent</c> header, after the products
        /// of the client's own <c>User-Agent</c>.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="HttpRequestException">The request failed.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public static Task<HttpResponseMessage> SendAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The header is set on the request rather than on the client, so that it also reaches a provider through an
            // HttpClient that the application passes in. The GitHub API refuses a request without it. A request header
            // replaces the default header of the client, so the products of the application's own User-Agent come first.
            foreach (var product in httpClient.DefaultRequestHeaders.UserAgent)
                request.Headers.UserAgent.Add(product);
            request.Headers.UserAgent.Add(Create());
            return httpClient.SendAsync(request, cancellationToken);
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
