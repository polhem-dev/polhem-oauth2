using System.Net.Sockets;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Signs a user in from a desktop or console application. The authorization URL opens in the default browser, and the
    /// provider redirects back to a loopback address on which this client listens, as described in RFC 8252.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OAuth2Options.RedirectUri"/> must be an <c>http</c> URI whose host is <c>localhost</c> or a loopback address,
    /// such as <c>http://127.0.0.1:53682/callback</c>, and it must be registered with the provider. Port 0 picks a free port
    /// for each sign-in, which only works with providers that accept any loopback port.
    /// </para>
    /// <para>
    /// A client secret distributed with a desktop application can be extracted from it, so it cannot be kept confidential.
    /// For that reason the client always uses PKCE, whatever <see cref="OAuth2Options.UsePkce"/> is set to, and does not send
    /// the client secret, except to Google.
    /// </para>
    /// <para>The options are copied when the client is created, so later changes to them have no effect.</para>
    /// </remarks>
    public sealed class LoopbackOAuth2Client
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

        private readonly PublicSignIn _signIn;
        private readonly string _configuredRedirectUri;
        private readonly Uri _redirectUri;
        private TimeSpan _timeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="LoopbackOAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <param name="httpClient">
        /// The HTTP client for requests to the provider, or null to use a shared instance. The client does not dispose it.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The redirect URI is not an absolute http URI on localhost or a loopback address, the client ID is empty, a scope is
        /// empty, or an endpoint is not an absolute https URI without a fragment.
        /// </exception>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        public LoopbackOAuth2Client(OAuth2Options options, HttpClient? httpClient = null)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));
            if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var redirectUri) || !LoopbackListener.IsLoopbackRedirectUri(redirectUri))
                throw new ArgumentException("The redirect URI must be an absolute http URI on localhost or a loopback address.", nameof(options));

            _signIn = new PublicSignIn(new OAuth2Client(options, httpClient, publicClient: true));
            _configuredRedirectUri = options.RedirectUri;
            _redirectUri = redirectUri;
        }

        /// <summary>
        /// Gets or sets how long <see cref="SignInAsync"/> waits for the provider to redirect back. The default is 5 minutes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive, or is longer than <see cref="int.MaxValue"/> milliseconds.</exception>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(value), "The timeout must be positive and at most int.MaxValue milliseconds.");
                _timeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the function that opens the authorization URL. When it is null, the URL opens in the default browser of
        /// the operating system. Set it to open the URL another way, for example through the launcher of a UI framework.
        /// </summary>
        /// <remarks>
        /// <see cref="SignInAsync"/> calls it after the listener has started, and waits for the returned task before it waits
        /// for the redirect. An exception from the function or its task propagates from <see cref="SignInAsync"/>.
        /// </remarks>
        public Func<Uri, Task>? OpenBrowser { get; set; }

        /// <summary>
        /// Opens the authorization URL in the browser, waits for the provider to redirect back, and exchanges the
        /// authorization code for tokens and user information.
        /// </summary>
        /// <param name="cancellationToken">Cancels the sign-in, including the requests to the provider after the redirect.</param>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// <para>
        /// In addition to the failures described on <see cref="OAuth2Client.CompleteAuthorizationAsync"/>, these become a
        /// failed result: a <see cref="TimeoutException"/> when no redirect arrives within <see cref="Timeout"/>, and an
        /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is canceled. Any other exception propagates.
        /// </para>
        /// <para>
        /// Only a request for the redirect path that names the redirect host and carries the state of this sign-in ends the
        /// wait. Other requests to the loopback address, which any web page open in the browser could send, are answered and ignored.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">A sign-in is already in progress on this client.</exception>
        /// <exception cref="SocketException">The port of the redirect URI cannot be listened on, for example because it is in use.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception"><see cref="OpenBrowser"/> is null and no browser can be started.</exception>
        /// <exception cref="PlatformNotSupportedException"><see cref="OpenBrowser"/> is null and the platform cannot start a process, as on iOS.</exception>
        public async Task<AuthorizationResult> SignInAsync(CancellationToken cancellationToken = default)
        {
            using (_signIn.Enter())
            {
                if (PublicSignIn.CanceledBeforeStart(cancellationToken) is { } canceled)
                    return canceled;

                using (var listener = LoopbackListener.Start(_redirectUri))
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // With port 0 the bound port differs on every sign-in, and both the authorization request and the token
                    // request must send the redirect URI with that port.
                    string redirectUri = _redirectUri.Port == 0 ? listener.RedirectUri.AbsoluteUri : _configuredRedirectUri;
                    AuthorizationRequest authorization = _signIn.Client.CreateAuthorizationRequest(redirectUri);

                    timeout.CancelAfter(_timeout);
                    await OpenAuthorizationUrlAsync(new Uri(authorization.Url)).ConfigureAwait(false);

                    AuthorizationCallback callback;
                    try
                    {
                        callback = await ReceiveCallbackAsync(listener, authorization.Pending.State, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        return cancellationToken.IsCancellationRequested
                            ? AuthorizationResult.Failure(ex)
                            : AuthorizationResult.Failure(new TimeoutException("No redirect arrived before the timeout."));
                    }

                    return await _signIn.CompleteAsync(callback, authorization.Pending, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Obtains new tokens with a refresh token. The client secret is not sent, except to Google.
        /// </summary>
        /// <param name="refreshToken">The refresh token from an earlier <see cref="TokenResponse"/>.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The new tokens. Keep the refresh token of the new response if it has one.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="refreshToken"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="refreshToken"/> is empty.</exception>
        /// <exception cref="NotSupportedException">The provider does not issue refresh tokens, as with Facebook.</exception>
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token.</exception>
        /// <exception cref="HttpRequestException">
        /// The request failed, or the token endpoint returned an unsuccessful status code without an error code.
        /// </exception>
        /// <exception cref="System.Text.Json.JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return _signIn.Client.RefreshTokenAsync(refreshToken, cancellationToken);
        }

        private Task OpenAuthorizationUrlAsync(Uri url)
        {
            if (OpenBrowser is { } openBrowser)
                return openBrowser(url);

            SystemBrowser.Open(url);
            return Task.CompletedTask;
        }

        private static async Task<AuthorizationCallback> ReceiveCallbackAsync(LoopbackListener listener, string state, CancellationToken cancellationToken)
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
                    reading.Cancel();
                    foreach (var read in pendingReads)
                    {
                        _ = read.ContinueWith(
                            abandoned => ReleaseAbandoned(abandoned), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                    _ = pendingAccept?.ContinueWith(
                        abandoned => ReleaseAbandoned(abandoned), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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

            bool received = callback.Error is null && !string.IsNullOrEmpty(callback.Code);
            await request.RespondAsync("200 OK", received ? ReceivedMessage : RejectedMessage).ConfigureAwait(false);
            return callback;
        }

        // A task abandoned when the wait ends is observed here, so that a failure is not reported as an unobserved task
        // exception, and a connection it produced is closed.
        private static void ReleaseAbandoned<T>(Task<T> task) where T : IDisposable
        {
            if (task.Status == TaskStatus.RanToCompletion)
                task.Result.Dispose();
            else
                _ = task.Exception;
        }
    }
}
