using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// A connection accepted by <see cref="LoopbackListener"/>, together with the target of its HTTP request line.
    /// </summary>
    internal sealed class LoopbackRequest : IDisposable
    {
        private const int MaxRequestLineBytes = 16 * 1024;

        private readonly TcpClient _client;

        private LoopbackRequest(TcpClient client, string? target)
        {
            _client = client;
            Target = target;
        }

        /// <summary>
        /// Gets the target of a <c>GET</c> request, such as <c>/callback?code=abc</c>. It is null when no complete
        /// <c>GET</c> request line arrived in time.
        /// </summary>
        public string? Target { get; }

        /// <summary>
        /// Reads the request line of an accepted connection. The returned request owns the connection.
        /// </summary>
        /// <param name="client">The accepted connection.</param>
        /// <param name="readTimeout">How long to wait for the request line.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The request.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public static async Task<LoopbackRequest> ReadAsync(TcpClient client, TimeSpan readTimeout, CancellationToken cancellationToken)
        {
            string? target;
            try
            {
                target = await ReadTargetAsync(client, readTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                throw;
            }
            return new LoopbackRequest(client, target);
        }

        /// <summary>
        /// Answers the request with a short HTML page and closes the connection afterwards.
        /// </summary>
        /// <param name="status">The status line text, such as <c>200 OK</c>.</param>
        /// <param name="message">The message shown on the page.</param>
        /// <returns>A task that completes when the response is written.</returns>
        public async Task RespondAsync(string status, string message)
        {
            byte[] body = Encoding.UTF8.GetBytes(
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>Sign-in</title></head><body><p>"
                + WebUtility.HtmlEncode(message)
                + "</p></body></html>");
            byte[] head = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + status + "\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n");

            try
            {
                var stream = _client.GetStream();
                await stream.WriteAsync(head, 0, head.Length).ConfigureAwait(false);
                await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The browser may have closed the connection already. The page is a courtesy, so the sign-in continues without it.
            }
            catch (InvalidOperationException)
            {
                // Raised as ObjectDisposedException when the read timeout closed the connection. The sign-in continues without the page.
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Dispose()
        {
            _client.Dispose();
        }

        private static async Task<string?> ReadTargetAsync(TcpClient client, TimeSpan readTimeout, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(readTimeout);

                // Socket reads do not observe a cancellation token on every target framework, so a stalled read is ended by
                // closing the connection. A browser can open a connection in advance and leave it idle.
                using (timeout.Token.Register(client.Dispose))
                {
                    var buffer = new byte[MaxRequestLineBytes];
                    int length = 0;
                    int lineEnd = -1;
                    try
                    {
                        var stream = client.GetStream();
                        while (length < buffer.Length && lineEnd < 0)
                        {
                            int read = await stream.ReadAsync(buffer, length, buffer.Length - length).ConfigureAwait(false);
                            if (read == 0)
                                break;
                            length += read;
                            lineEnd = Array.IndexOf(buffer, (byte)'\n', 0, length);
                        }
                    }
                    catch (IOException)
                    {
                        lineEnd = -1;
                    }
                    catch (InvalidOperationException)
                    {
                        lineEnd = -1;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (lineEnd < 0)
                        return null;

                    // The request line is "METHOD target HTTP/version". Providers redirect with GET.
                    string[] parts = Encoding.ASCII.GetString(buffer, 0, lineEnd).TrimEnd('\r').Split(' ');
                    return parts.Length == 3 && string.Equals(parts[0], "GET", StringComparison.Ordinal) ? parts[1] : null;
                }
            }
        }
    }
}
