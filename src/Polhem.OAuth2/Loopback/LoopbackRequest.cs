using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.OAuth2
{
    /// <summary>
    /// A connection accepted by <see cref="LoopbackListener"/>, together with the parts of its HTTP request that a sign-in uses.
    /// </summary>
    /// <remarks>
    /// The read of the request head has one implementation for each target framework, in <c>LoopbackRequest.Net.cs</c> and
    /// <c>LoopbackRequest.NetStandard.cs</c>, because only the newer one can cancel a socket read.
    /// </remarks>
    internal sealed partial class LoopbackRequest : IDisposable
    {
        private const int MaxRequestHeadBytes = 16 * 1024;

        private static readonly char[] s_lineSeparator = { '\n' };

        private readonly TcpClient _client;

        private LoopbackRequest(TcpClient client, string? target, string? host)
        {
            _client = client;
            Target = target;
            Host = host;
        }

        /// <summary>
        /// Gets the target of a <c>GET</c> request, such as <c>/callback?code=abc</c>. It is null when no complete
        /// <c>GET</c> request head arrived in time.
        /// </summary>
        public string? Target { get; }

        /// <summary>
        /// Gets the value of the <c>Host</c> header. It is null when the request has no <c>Host</c> header, has more than one,
        /// or did not arrive in time.
        /// </summary>
        public string? Host { get; }

        /// <summary>
        /// Reads the request line and headers of an accepted connection. The returned request owns the connection.
        /// </summary>
        /// <param name="client">The accepted connection.</param>
        /// <param name="readTimeout">How long to wait for the request line and headers.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The request.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public static async Task<LoopbackRequest> ReadAsync(TcpClient client, TimeSpan readTimeout, CancellationToken cancellationToken)
        {
            string? head;
            try
            {
                head = await ReadHeadAsync(client, readTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                throw;
            }

            if (head is null)
                return new LoopbackRequest(client, null, null);

            ParseHead(head, out string? target, out string? host);
            return new LoopbackRequest(client, target, host);
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

        // Returns the request line and header lines, or null when the connection closed, the read timed out, or the head is
        // longer than the limit. How a stalled read is ended differs by target framework: see the other part of this class.
        private static async Task<string?> ReadHeadAsync(TcpClient client, TimeSpan readTimeout, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(readTimeout);
                var buffer = new byte[MaxRequestHeadBytes];
                int headEnd = await ReadHeadAsync(client, buffer, timeout.Token).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return headEnd < 0 ? null : Encoding.ASCII.GetString(buffer, 0, headEnd);
            }
        }

        // Reads into the buffer until the empty line that ends the head arrives, and returns the index of its final line
        // feed, or -1 when the connection closed first or the buffer filled up. The read is supplied by the target framework.
        private static async Task<int> ReadHeadAsync(byte[] buffer, Func<byte[], int, int, Task<int>> read)
        {
            int length = 0;
            int headEnd = -1;
            while (length < buffer.Length && headEnd < 0)
            {
                int count = await read(buffer, length, buffer.Length - length).ConfigureAwait(false);
                if (count == 0)
                    break;

                int searchStart = Math.Max(0, length - 2);
                length += count;
                headEnd = FindHeadEnd(buffer, searchStart, length);
            }
            return headEnd;
        }

        // The head ends with an empty line. Lines end with CRLF, and a bare LF is accepted as well (RFC 9112, section 2.2).
        // Returns the index of the final line feed, or -1 when the empty line has not arrived.
        private static int FindHeadEnd(byte[] buffer, int start, int length)
        {
            for (int i = start; i < length; i++)
            {
                if (buffer[i] != (byte)'\n')
                    continue;
                if (i + 1 < length && buffer[i + 1] == (byte)'\n')
                    return i + 1;
                if (i + 2 < length && buffer[i + 1] == (byte)'\r' && buffer[i + 2] == (byte)'\n')
                    return i + 2;
            }
            return -1;
        }

        private static void ParseHead(string head, out string? target, out string? host)
        {
            string[] lines = head.Split(s_lineSeparator);

            // The request line is "METHOD target HTTP/version". Providers redirect with GET.
            string[] parts = lines[0].TrimEnd('\r').Split(' ');
            target = parts.Length == 3 && string.Equals(parts[0], "GET", StringComparison.Ordinal) ? parts[1] : null;

            host = null;
            int hostCount = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                int colon = line.IndexOf(':');
                if (colon > 0 && string.Equals(line.Substring(0, colon), "Host", StringComparison.OrdinalIgnoreCase))
                {
                    hostCount++;
                    host = line.Substring(colon + 1).Trim();
                }
            }

            // RFC 9112, section 3.2: a request with more than one Host header is invalid.
            if (hostCount != 1)
                host = null;
        }
    }
}
