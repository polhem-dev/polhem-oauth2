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
    internal sealed partial class LoopbackListener : IDisposable
    {
        private const int BindAttempts = 5;

        private readonly List<TcpListener> _listeners;
        private readonly Task<TcpClient>?[] _pendingAccepts;

        // Canceled by Dispose. It ends a pending accept natively where the framework can, and Stop ends it everywhere else.
        private readonly CancellationTokenSource _stopping = new();

        private LoopbackListener(List<TcpListener> listeners, Uri redirectUri)
        {
            _listeners = listeners;
            _pendingAccepts = new Task<TcpClient>?[listeners.Count];
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
        /// <returns>The started listener.</returns>
        /// <exception cref="ArgumentException"><paramref name="redirectUri"/> is not an http URI on localhost or a loopback address.</exception>
        /// <exception cref="SocketException">The port cannot be listened on, for example because it is in use.</exception>
        public static LoopbackListener Start(Uri redirectUri)
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
                return new LoopbackListener(listeners, new UriBuilder(redirectUri) { Port = port }.Uri);
            }
        }

        /// <summary>
        /// Checks whether a request target is the redirect path, and gets its query string. Percent-encoded characters in the
        /// path are decoded before the comparison.
        /// </summary>
        /// <param name="target">The request target, such as <c>/callback?code=abc</c>.</param>
        /// <param name="query">The query string without the question mark, or an empty string.</param>
        /// <returns><see langword="true"/> if the path is the path of <see cref="RedirectUri"/>; otherwise, <see langword="false"/>.</returns>
        public bool TryGetRedirectQuery(string target, out string query)
        {
            int queryStart = target.IndexOf('?');
            string path = queryStart >= 0 ? target.Substring(0, queryStart) : target;
            query = queryStart >= 0 ? target.Substring(queryStart + 1) : string.Empty;
            return string.Equals(Uri.UnescapeDataString(path), Uri.UnescapeDataString(RedirectUri.AbsolutePath), StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks whether the <c>Host</c> header of a request names the host and port of <see cref="RedirectUri"/>, as it does
        /// when a browser follows the redirect.
        /// </summary>
        /// <param name="host">The value of the <c>Host</c> header, or null if the request has none.</param>
        /// <returns><see langword="true"/> if the header names the redirect URI's authority; otherwise, <see langword="false"/>.</returns>
        public bool IsRedirectHost(string? host)
        {
            return host is not null && string.Equals(host, RedirectUri.Authority, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Waits for the next connection on any bound address.
        /// </summary>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns>The accepted connection, which the caller disposes.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="ObjectDisposedException">The listener was disposed while a connection was being accepted.</exception>
        /// <exception cref="SocketException">A connection could not be accepted.</exception>
        public async Task<TcpClient> AcceptClientAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < _listeners.Count; i++)
                _pendingAccepts[i] ??= AcceptAsync(_listeners[i], _stopping.Token);

            // An accept outlives this wait: the one that loses the race on a listener with two addresses is kept for the next
            // call, because a connection it completes with may be the redirect. The wait therefore races a task that the token
            // completes, instead of passing the token to the accepts.
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

                // Dispose may have run since the wait ended: it clears the pending accepts and closes the connection that
                // the completed one produced, so this listener has nothing to hand over.
                int index = Array.IndexOf(_pendingAccepts, completed);
                if (index < 0)
                    throw new ObjectDisposedException(nameof(LoopbackListener));

                var accept = _pendingAccepts[index]!;
                _pendingAccepts[index] = null;

                return await accept.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        public void Dispose()
        {
            if (_stopping.IsCancellationRequested)
                return;

            // The slots are cleared before the accepts end, so a wait in AcceptClientAsync that they wake finds its accept gone
            // and reports ObjectDisposedException on every target framework.
            var abandoned = _pendingAccepts.OfType<Task<TcpClient>>().ToList();
            Array.Clear(_pendingAccepts, 0, _pendingAccepts.Length);

            _stopping.Cancel();
            _stopping.Dispose();
            foreach (var listener in _listeners)
                listener.Stop();

            foreach (var accept in abandoned)
                ReleaseWhenAbandoned(accept);
        }

        // Accepts a connection. The token is observed where the framework supports it; otherwise Dispose ends the accept by
        // stopping the listener.
        private static partial Task<TcpClient> AcceptAsync(TcpListener listener, CancellationToken stopping);

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

        /// <summary>
        /// Releases what a task that nobody waits for any more produces: a failure is observed, so that it is not reported as
        /// an unobserved task exception, and a connection or request it returns is closed.
        /// </summary>
        /// <remarks>
        /// An accept still pending when the listener stops usually fails, but in a rare race returns a connection that nobody
        /// will answer. In that race the wait in <see cref="AcceptClientAsync"/> may already have ended, and it finds the slot cleared.
        /// </remarks>
        /// <typeparam name="T">The type of the result, which is disposed.</typeparam>
        /// <param name="task">The abandoned task, which may still be running.</param>
        public static void ReleaseWhenAbandoned<T>(Task<T> task) where T : IDisposable
        {
            _ = task.ContinueWith(
                abandoned =>
                {
                    if (abandoned.Status == TaskStatus.RanToCompletion)
                        abandoned.Result.Dispose();
                    else
                        _ = abandoned.Exception;
                },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
