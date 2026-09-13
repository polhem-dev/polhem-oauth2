using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// Listens on a loopback address for the HTTP request that a provider's redirect makes the browser send.
    /// </summary>
    /// <remarks>
    /// It reads raw TCP instead of using <c>HttpListener</c>, so no URL reservation or elevated rights are needed on Windows.
    /// </remarks>
    internal sealed class LoopbackListener : IDisposable
    {
        private const int MaxRequestLineBytes = 16 * 1024;

        private readonly List<TcpListener> _listeners;

        private LoopbackListener(List<TcpListener> listeners, Uri redirectUri)
        {
            _listeners = listeners;
            RedirectUri = redirectUri;
        }

        /// <summary>
        /// Gets the redirect URI, with the port that was actually bound.
        /// </summary>
        public Uri RedirectUri { get; }

        /// <summary>
        /// Starts listening for a redirect URI whose host is <c>localhost</c> or a loopback address. Port 0 picks a free port.
        /// </summary>
        public static LoopbackListener Start(Uri redirectUri)
        {
            if (redirectUri.Scheme != Uri.UriSchemeHttp)
                throw new ArgumentException("The redirect URI must use the http scheme.", nameof(redirectUri));

            IPAddress[] addresses;
            if (string.Equals(redirectUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                addresses = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback };
            else if (IPAddress.TryParse(redirectUri.DnsSafeHost, out var address) && IPAddress.IsLoopback(address))
                addresses = new[] { address };
            else
                throw new ArgumentException("The redirect URI must point to localhost or a loopback address.", nameof(redirectUri));

            int port = redirectUri.Port;
            var listeners = new List<TcpListener>();
            try
            {
                foreach (var loopback in addresses)
                {
                    var listener = new TcpListener(loopback, port);
                    try
                    {
                        listener.Start();
                    }
                    catch (SocketException) when (addresses.Length > 1 && loopback.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        // localhost also reaches the IPv4 loopback, so a machine without IPv6 can still be probed.
                        listener.Dispose();
                        continue;
                    }

                    port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    listeners.Add(listener);
                }
            }
            catch (SocketException)
            {
                listeners.ForEach(started => started.Dispose());
                throw;
            }

            return new LoopbackListener(listeners, new UriBuilder(redirectUri) { Port = port }.Uri);
        }

        /// <summary>
        /// Waits for a request to the redirect path and answers it. Requests to other paths, such as a favicon, are ignored.
        /// </summary>
        /// <exception cref="TimeoutException">No request to the redirect path arrived in time.</exception>
        public async Task<LoopbackCallback> WaitForCallbackAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            try
            {
                return await WaitCoreAsync(cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw new TimeoutException("No callback arrived before the timeout.");
            }
        }

        public void Dispose()
        {
            _listeners.ForEach(listener => listener.Dispose());
        }

        private async Task<LoopbackCallback> WaitCoreAsync(CancellationToken cancellationToken)
        {
            var pending = new Task<TcpClient>?[_listeners.Count];
            while (true)
            {
                for (int i = 0; i < _listeners.Count; i++)
                    pending[i] ??= _listeners[i].AcceptTcpClientAsync(cancellationToken).AsTask();

                var completed = await Task.WhenAny(pending.Select(task => task!));
                pending[Array.IndexOf(pending, completed)] = null;

                using var client = await completed;
                var stream = client.GetStream();
                string? target = await ReadRequestTargetAsync(stream, cancellationToken);

                if (target is null || !IsRedirectPath(target))
                {
                    await WriteResponseAsync(stream, "404 Not Found", "Not found.", cancellationToken);
                    continue;
                }

                await WriteResponseAsync(stream, "200 OK", "The sign-in finished. You can close this tab.", cancellationToken);
                return LoopbackCallback.FromRequestTarget(target);
            }
        }

        private bool IsRedirectPath(string target)
        {
            int queryStart = target.IndexOf('?', StringComparison.Ordinal);
            string path = queryStart >= 0 ? target[..queryStart] : target;
            return string.Equals(path, RedirectUri.AbsolutePath, StringComparison.Ordinal);
        }

        private static async Task<string?> ReadRequestTargetAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[MaxRequestLineBytes];
            int length = 0;
            int lineEnd = -1;
            while (length < buffer.Length && lineEnd < 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
                if (read == 0)
                    break;
                length += read;
                lineEnd = Array.IndexOf(buffer, (byte)'\n', 0, length);
            }

            if (lineEnd < 0)
                return null;

            // The request line is "METHOD target HTTP/version".
            string[] parts = Encoding.ASCII.GetString(buffer, 0, lineEnd).TrimEnd('\r').Split(' ');
            return parts.Length >= 2 ? parts[1] : null;
        }

        private static async Task WriteResponseAsync(NetworkStream stream, string status, string message, CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes($"<!doctype html><html><body><p>{WebUtility.HtmlEncode(message)}</p></body></html>");
            string head = $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
        }
    }
}
