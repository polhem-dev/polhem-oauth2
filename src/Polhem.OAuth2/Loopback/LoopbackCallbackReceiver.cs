using System.Net.Sockets;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The parts of a loopback sign-in that <see cref="LoopbackOAuth2Client"/> and <see cref="LoopbackRelayClient"/> share:
    /// opening the browser, and waiting on the listener for the redirect that carries the state of the sign-in (ADR-004).
    /// </summary>
    internal static class LoopbackCallbackReceiver
    {
        private const string ReceivedMessage = "The sign-in response was received. You can close this tab and return to the application.";
        private const string RejectedMessage = "The sign-in did not complete. You can close this tab and return to the application.";
        private const string NotFoundMessage = "Not found.";

        // A browser opens a few connections in advance and may leave them idle, so requests are read side by side and the
        // redirect does not wait behind them. Further connections wait until one of these is answered or times out.
        private const int MaxConcurrentRequests = 8;

        // A browser sends its request as soon as it connects, so a connection that stays silent this long is one it opened
        // in advance and is not waited for.
        private static readonly TimeSpan s_requestReadTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Checks the value of a <c>Timeout</c> property of a loopback client.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns>The value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive, or is longer than <see cref="int.MaxValue"/> milliseconds.</exception>
        public static TimeSpan ValidateTimeout(TimeSpan value)
        {
            if (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "The timeout must be positive and at most int.MaxValue milliseconds.");
            return value;
        }

        /// <summary>
        /// Opens a URL with the function of the client, or in the default browser when there is none.
        /// </summary>
        /// <param name="openBrowser">The function that opens the URL, or null.</param>
        /// <param name="url">The URL.</param>
        /// <returns>The task of the function, or a completed task.</returns>
        public static Task OpenAsync(Func<Uri, Task>? openBrowser, Uri url)
        {
            if (openBrowser is not null)
                return openBrowser(url);

            SystemBrowser.Open(url);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Answers requests to the listener until one is the redirect of the sign-in: a request for the redirect path that
        /// names the redirect host and carries the state. Other requests, which any web page open in the browser could send,
        /// are answered and ignored.
        /// </summary>
        /// <param name="listener">The listener.</param>
        /// <param name="state">The state of the sign-in.</param>
        /// <param name="cancellationToken">Ends the wait.</param>
        /// <returns>The parameters of the redirect.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public static async Task<AuthorizationCallback> ReceiveAsync(LoopbackListener listener, string state, CancellationToken cancellationToken)
        {
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var pendingReads = new List<Task<LoopbackRequest>>();
                Task<TcpClient>? pendingAccept = null;
                try
                {
                    while (true)
                    {
                        if (pendingAccept is null && pendingReads.Count < MaxConcurrentRequests)
                            pendingAccept = listener.AcceptClientAsync(reading.Token);

                        var waiting = new List<Task>(pendingReads);
                        if (pendingAccept is not null)
                            waiting.Add(pendingAccept);

                        Task completed = await Task.WhenAny(waiting).ConfigureAwait(false);
                        if (completed == pendingAccept)
                        {
                            pendingAccept = null;
                            TcpClient client = await ((Task<TcpClient>)completed).ConfigureAwait(false);
                            pendingReads.Add(LoopbackRequest.ReadAsync(client, s_requestReadTimeout, reading.Token));
                            continue;
                        }

                        var read = (Task<LoopbackRequest>)completed;
                        pendingReads.Remove(read);
                        using (var request = await read.ConfigureAwait(false))
                        {
                            if (await AnswerAsync(listener, request, state).ConfigureAwait(false) is { } callback)
                                return callback;
                        }
                    }
                }
                finally
                {
                    // Reads still in progress end promptly once the token is canceled, and their connections are closed.
                    await reading.CancelAsync().ConfigureAwait(false);
                    foreach (var read in pendingReads)
                        LoopbackListener.ReleaseWhenAbandoned(read);
                    if (pendingAccept is not null)
                        LoopbackListener.ReleaseWhenAbandoned(pendingAccept);
                }
            }
        }

        // Answers one request, and returns its callback parameters when it is the redirect of this sign-in, or null otherwise.
        private static async Task<AuthorizationCallback?> AnswerAsync(LoopbackListener listener, LoopbackRequest request, string state)
        {
            if (request.Target is null)
                return null;

            if (!listener.TryGetRedirectQuery(request.Target, out string query))
            {
                await request.RespondAsync("404 Not Found", NotFoundMessage).ConfigureAwait(false);
                return null;
            }

            // Any web page open in the browser can send requests to a loopback address, and through DNS rebinding such a page
            // can use a host name of its own. A request that names another host, or lacks the state, is not the redirect.
            var callback = CallbackQuery.FromQuery(query);
            if (!listener.IsRedirectHost(request.Host) || !string.Equals(callback.State, state, StringComparison.Ordinal))
            {
                await request.RespondAsync("400 Bad Request", RejectedMessage).ConfigureAwait(false);
                return null;
            }

            // An empty error names no error, as OAuth2Client.CompleteAuthorizationAsync also reads it, so the page agrees with
            // how the sign-in ends.
            bool received = string.IsNullOrEmpty(callback.Error) && !string.IsNullOrEmpty(callback.Code);
            await request.RespondAsync("200 OK", received ? ReceivedMessage : RejectedMessage).ConfigureAwait(false);
            return callback;
        }
    }
}
