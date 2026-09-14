using System.Net;
using System.Net.Sockets;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Listens on a loopback address for the request that a provider's redirect makes the browser send.
    /// </summary>
    /// <remarks>
    /// It accepts TCP connections itself instead of using <c>HttpListener</c>, which on Windows needs a URL reservation for
    /// loopback address prefixes and cannot pick a free port. Only loopback addresses are bound, as RFC 8252 requires.
    /// </remarks>
    internal sealed class LoopbackListener : IDisposable
    {
        private const int BindAttempts = 5;

        private readonly List<TcpListener> _listeners;
        private readonly Task<TcpClient>?[] _pendingAccepts;
        private readonly TimeSpan _requestReadTimeout;

        private LoopbackListener(List<TcpListener> listeners, Uri redirectUri, TimeSpan requestReadTimeout)
        {
            _listeners = listeners;
            _pendingAccepts = new Task<TcpClient>?[listeners.Count];
            _requestReadTimeout = requestReadTimeout;
            RedirectUri = redirectUri;
        }

        /// <summary>
        /// Gets the redirect URI, with the port that was actually bound.
        /// </summary>
        public Uri RedirectUri { get; }

        /// <summary>
        /// Checks whether a redirect URI can be served: it uses the http scheme, and its host is <c>localhost</c> or a loopback address.
        /// </summary>
        /// <param name="redirectUri">The absolute redirect URI.</param>
        /// <returns><see langword="true"/> if the listener can serve the URI; otherwise, <see langword="false"/>.</returns>
        public static bool IsLoopbackRedirectUri(Uri redirectUri)
        {
            return string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && (IsLocalhost(redirectUri) || (IPAddress.TryParse(redirectUri.DnsSafeHost, out var address) && IPAddress.IsLoopback(address)));
        }

        /// <summary>
        /// Starts listening for a redirect URI. Port 0 picks a free port.
        /// </summary>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <param name="requestReadTimeout">How long to wait for the request line of each accepted connection.</param>
        /// <returns>The started listener.</returns>
        /// <exception cref="ArgumentException"><paramref name="redirectUri"/> is not an http URI on localhost or a loopback address.</exception>
        /// <exception cref="SocketException">The port cannot be listened on, for example because it is in use.</exception>
        public static LoopbackListener Start(Uri redirectUri, TimeSpan requestReadTimeout)
        {
            if (!IsLoopbackRedirectUri(redirectUri))
                throw new ArgumentException("The redirect URI must be an http URI on localhost or a loopback address.", nameof(redirectUri));

            IPAddress[] addresses = IsLocalhost(redirectUri)
                ? new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }
                : new[] { IPAddress.Parse(redirectUri.DnsSafeHost) };

            for (int attempt = 1; ; attempt++)
            {
                List<TcpListener>? listeners = TryBind(addresses, redirectUri.Port, retryOnConflict: redirectUri.Port == 0 && attempt < BindAttempts);
                if (listeners is null)
                    continue;

                int port = ((IPEndPoint)listeners[0].LocalEndpoint).Port;
                return new LoopbackListener(listeners, new UriBuilder(redirectUri) { Port = port }.Uri, requestReadTimeout);
            }
        }

        /// <summary>
        /// Checks whether a request target is the redirect path, and gets its query string.
        /// </summary>
        /// <param name="target">The request target, such as <c>/callback?code=abc</c>.</param>
        /// <param name="query">The query string without the question mark, or an empty string.</param>
        /// <returns><see langword="true"/> if the path is the path of <see cref="RedirectUri"/>; otherwise, <see langword="false"/>.</returns>
        public bool TryGetRedirectQuery(string target, out string query)
        {
            int queryStart = target.IndexOf('?');
            string path = queryStart >= 0 ? target.Substring(0, queryStart) : target;
            query = queryStart >= 0 ? target.Substring(queryStart + 1) : string.Empty;
            return string.Equals(path, RedirectUri.AbsolutePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Waits for the next connection on any bound address and reads its request line.
        /// </summary>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns>The request, which the caller answers and disposes.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="SocketException">A connection could not be accepted.</exception>
        public async Task<LoopbackRequest> AcceptAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < _listeners.Count; i++)
                _pendingAccepts[i] ??= _listeners[i].AcceptTcpClientAsync();

            // AcceptTcpClientAsync takes no cancellation token on netstandard2.0, so the wait races a task that the token completes.
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => canceled.TrySetResult(true)))
            {
                var waiting = new List<Task>(_pendingAccepts.Length + 1) { canceled.Task };
                foreach (var pending in _pendingAccepts)
                {
                    if (pending is not null)
                        waiting.Add(pending);
                }

                Task completed = await Task.WhenAny(waiting).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                int index = Array.IndexOf(_pendingAccepts, completed);
                var accept = _pendingAccepts[index]!;
                _pendingAccepts[index] = null;

                TcpClient client = await accept.ConfigureAwait(false);
                return await LoopbackRequest.ReadAsync(client, _requestReadTimeout, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        public void Dispose()
        {
            foreach (var listener in _listeners)
                listener.Stop();

            for (int i = 0; i < _pendingAccepts.Length; i++)
            {
                _ = _pendingAccepts[i]?.ContinueWith(
                    ReleaseAbandonedAccept, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                _pendingAccepts[i] = null;
            }
        }

        private static bool IsLocalhost(Uri redirectUri)
        {
            return string.Equals(redirectUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        // Returns null when the port must be tried again: with port 0 the IPv4 listener picks a free port, and the IPv6 loopback
        // may already have that port in use.
        private static List<TcpListener>? TryBind(IPAddress[] addresses, int port, bool retryOnConflict)
        {
            var listeners = new List<TcpListener>();
            try
            {
                foreach (var address in addresses)
                {
                    var listener = StartOrSkip(address, port, canSkip: addresses.Length > 1);
                    if (listener is null)
                        continue;

                    port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    listeners.Add(listener);
                }
                return listeners;
            }
            catch (SocketException ex) when (retryOnConflict && ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                listeners.ForEach(started => started.Stop());
                return null;
            }
            catch (SocketException)
            {
                listeners.ForEach(started => started.Stop());
                throw;
            }
        }

        // An address is skipped only when the machine has no such address family. If another program already listens on the
        // IPv6 loopback with the same port, starting fails instead: the browser could send the authorization code to that program.
        private static TcpListener? StartOrSkip(IPAddress address, int port, bool canSkip)
        {
            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(address, port);
                listener.Start();
                return listener;
            }
            catch (SocketException ex) when (canSkip
                && address.AddressFamily == AddressFamily.InterNetworkV6
                && (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported || ex.SocketErrorCode == SocketError.AddressNotAvailable))
            {
                listener?.Stop();
                return null;
            }
        }

        // An accept still pending when the listener stops either fails, which is observed here so that it is not reported as an
        // unobserved task exception, or in a rare race returns a connection that nobody will answer.
        private static void ReleaseAbandonedAccept(Task<TcpClient> accept)
        {
            if (accept.Status == TaskStatus.RanToCompletion)
                accept.Result.Dispose();
            else
                _ = accept.Exception;
        }
    }
}
