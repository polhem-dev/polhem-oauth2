using System.Net;
using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The status code and the body of a response from a provider, read up to a limit.
    /// </summary>
    /// <remarks>
    /// The response is read after its headers, so that its size can be limited before the body is buffered.
    /// <see cref="HttpClient.Timeout"/> covers only the headers then, so a token that the timeout cancels covers the body as
    /// well.
    /// </remarks>
    internal sealed class ProviderResponse
    {
        /// <summary>
        /// The largest body that is read. Responses of real providers are a few kilobytes; the limit keeps a provider that
        /// answers with a huge body from exhausting the memory of the application.
        /// </summary>
        public const int MaxBodyBytes = 1024 * 1024;

        private ProviderResponse(HttpStatusCode statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>
        /// Gets the status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets a value indicating whether the status code is in the range 200 to 299.
        /// </summary>
        public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;

        /// <summary>
        /// Gets the body, decoded as UTF-8, the encoding of JSON (RFC 8259, section 8.1).
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// Sends a request with the library's <c>User-Agent</c> and reads the status code and the body of the response.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="HttpRequestException">
        /// The request failed, the connection failed while the body was read, or the body is larger than
        /// <see cref="MaxBodyBytes"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// The request was canceled, or timed out, as a <see cref="TaskCanceledException"/> with a <see cref="TimeoutException"/>
        /// inside, the way <see cref="HttpClient"/> reports its own timeout.
        /// </exception>
        public static async Task<ProviderResponse> SendAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (httpClient.Timeout != Timeout.InfiniteTimeSpan)
                    timeout.CancelAfter(httpClient.Timeout);
                try
                {
                    using (var response = await LibraryUserAgent.SendAsync(httpClient, request, timeout.Token, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        return new ProviderResponse(response.StatusCode, await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false));
                    }
                }
                // The caller's token is the one that a cancellation by the caller carries.
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException
                    && timeout.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TaskCanceledException("The request to the provider timed out.", new TimeoutException(exception.Message, exception));
                }
                catch (IOException exception)
                {
                    // The connection failed while the body was read, which the request reports as HttpRequestException elsewhere.
                    throw new HttpRequestException("The response of the provider could not be read.", exception);
                }
            }
        }

        // Reads the body up to the limit, whether or not the response states its length.
        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            const string TooLargeMessage = "The response of the provider is larger than 1 MiB.";
            if (response.Content.Headers.ContentLength > MaxBodyBytes)
                throw new HttpRequestException(TooLargeMessage);

            cancellationToken.ThrowIfCancellationRequested();
            // The registration also disposes the response, for a stream whose reads do not observe the token.
            using (cancellationToken.Register(response.Dispose))
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var body = new MemoryStream())
            {
                byte[] buffer = new byte[16 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (body.Length + read > MaxBodyBytes)
                        throw new HttpRequestException(TooLargeMessage);
                    await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                return new UTF8Encoding(false).GetString(body.GetBuffer(), 0, (int)body.Length).TrimStart('\uFEFF');
            }
        }
    }
}
