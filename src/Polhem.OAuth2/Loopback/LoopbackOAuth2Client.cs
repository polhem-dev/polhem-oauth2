namespace Polhem.OAuth2
{
    /// <summary>
    /// Signs a user in from a desktop or console application. The authorization URL opens in the default browser, and the
    /// provider redirects back to a loopback address on which this client listens, as described in RFC 8252.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OAuth2Options.RedirectUri"/> must be an <c>http</c> URI whose host is <c>localhost</c> or a loopback address,
    /// such as <c>http://127.0.0.1:53682/callback</c>, and it must be registered with the provider. The value is read when the
    /// client is created. Port 0 picks a free port for each sign-in, which only works with providers that accept any loopback port.
    /// </para>
    /// <para>
    /// A client secret distributed with a desktop application can be extracted from it, so it cannot be kept confidential.
    /// For that reason the client always uses PKCE, whatever <see cref="OAuth2Options.UsePkce"/> is set to, and the client
    /// secret is not sent with the token request unless the provider requires it, as Google does.
    /// </para>
    /// </remarks>
    public class LoopbackOAuth2Client : BaseOAuth2Client
    {
        private const string ReceivedMessage = "The sign-in response was received. You can close this tab and return to the application.";
        private const string RejectedMessage = "The sign-in did not complete. You can close this tab and return to the application.";
        private const string NotFoundMessage = "Not found.";

        // A browser sends the request line as soon as it connects, so a connection that stays silent this long is one it opened
        // in advance and is not waited for.
        private static readonly TimeSpan s_requestReadTimeout = TimeSpan.FromSeconds(5);

        private readonly OAuth2Options _options;
        private readonly string _configuredRedirectUri;
        private readonly Uri _redirectUri;
        private TimeSpan _timeout = TimeSpan.FromMinutes(5);
        private int _signInInProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoopbackOAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        /// <exception cref="ArgumentException">
        /// An endpoint of <paramref name="options"/> is not an absolute https URI, or its redirect URI is not an absolute http
        /// URI on localhost or a loopback address.
        /// </exception>
        public LoopbackOAuth2Client(OAuth2Options options) : this(options, null)
        {
        }

        internal LoopbackOAuth2Client(OAuth2Options options, HttpClient? httpClient) : base(options, httpClient)
        {
            if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var redirectUri) || !LoopbackListener.IsLoopbackRedirectUri(redirectUri))
                throw new ArgumentException("The redirect URI must be an absolute http URI on localhost or a loopback address.", nameof(options));

            _options = options;
            _configuredRedirectUri = options.RedirectUri;
            _redirectUri = redirectUri;

            // RFC 8252 requires PKCE for native applications, because their client secret cannot be kept confidential.
            UsePkce = true;
            IsPublicClient = true;
        }

        /// <inheritdoc/>
        public override IStateStorage StateStorage { get; } = new MemoryStateStorage();

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
        /// Gets or sets the action that opens the authorization URL. When it is null, the URL opens in the default browser of
        /// the operating system. Set it to open the URL another way, for example through the launcher of a UI framework.
        /// </summary>
        public Action<string>? OpenBrowser { get; set; }

        /// <summary>
        /// Opens the authorization URL in the browser, waits for the provider to redirect back, and exchanges the
        /// authorization code for an access token and user information.
        /// </summary>
        /// <param name="cancellationToken">Cancels the wait for the redirect. The code exchange that follows is not canceled.</param>
        /// <returns>A successful result with the access token and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// <para>
        /// In addition to the failures described on <see cref="BaseOAuth2Client.ValidateAuthorization"/>, these become a failed result:
        /// a <see cref="TimeoutException"/> when no redirect arrives within <see cref="Timeout"/>, an <see cref="OperationCanceledException"/>
        /// when <paramref name="cancellationToken"/> is canceled, and an <see cref="OAuth2Exception"/> when the provider redirects
        /// back with an error. Any other exception propagates.
        /// </para>
        /// <para>
        /// Only a request that carries the state of this sign-in ends the wait. Other requests to the redirect URI, which any web
        /// page open in the browser could send, are answered and ignored.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A sign-in is already in progress on this client, or <see cref="OpenBrowser"/> is null and the authorization URL is
        /// not an http or https URL.
        /// </exception>
        /// <exception cref="System.Net.Sockets.SocketException">The port of the redirect URI cannot be listened on, for example because it is in use.</exception>
        public async Task<AuthorizationResult> SignInAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _signInInProgress, 1, 0) != 0)
                throw new InvalidOperationException("A sign-in is already in progress on this client.");

            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return Failure(new OperationCanceledException(cancellationToken));

                using (var listener = LoopbackListener.Start(_redirectUri, s_requestReadTimeout))
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // With port 0 the bound port differs on every sign-in, and both the authorization request and the token
                    // request must send the redirect URI with that port.
                    if (_redirectUri.Port == 0)
                        _options.RedirectUri = listener.RedirectUri.AbsoluteUri;

                    timeout.CancelAfter(_timeout);
                    string authorizationUrl = GetAuthorizationUrl(CreateState());
                    (OpenBrowser ?? SystemBrowser.Open)(authorizationUrl);

                    LoopbackCallback callback;
                    try
                    {
                        callback = await ReceiveCallbackAsync(listener, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        return cancellationToken.IsCancellationRequested
                            ? Failure(ex)
                            : Failure(new TimeoutException("No redirect arrived before the timeout."));
                    }

                    if (callback.Error is { } error)
                        return Failure(new OAuth2Exception($"The provider returned the error '{error}'."));

                    return await ValidateAuthorization(callback.Code).ConfigureAwait(false);
                }
            }
            finally
            {
                if (_redirectUri.Port == 0)
                    _options.RedirectUri = _configuredRedirectUri;
                Interlocked.Exchange(ref _signInInProgress, 0);
            }
        }

        private async Task<LoopbackCallback> ReceiveCallbackAsync(LoopbackListener listener, CancellationToken cancellationToken)
        {
            while (true)
            {
                using (var request = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (request.Target is null)
                        continue;

                    if (!listener.TryGetRedirectQuery(request.Target, out string query))
                    {
                        await request.RespondAsync("404 Not Found", NotFoundMessage).ConfigureAwait(false);
                        continue;
                    }

                    var callback = LoopbackCallback.FromQuery(query);
                    if (!IsCurrentState(callback.State))
                    {
                        await request.RespondAsync("400 Bad Request", RejectedMessage).ConfigureAwait(false);
                        continue;
                    }

                    StateStorage.RemoveState();
                    bool received = callback.Error is null && !string.IsNullOrEmpty(callback.Code);
                    await request.RespondAsync("200 OK", received ? ReceivedMessage : RejectedMessage).ConfigureAwait(false);
                    return callback;
                }
            }
        }

        private bool IsCurrentState(string? returnedState)
        {
            return !string.IsNullOrEmpty(returnedState)
                && string.Equals(returnedState, StateStorage.GetState(), StringComparison.Ordinal);
        }

        // A state has the same requirements as a PKCE code verifier: enough cryptographically random bytes, encoded to be safe in a URL.
        private static string CreateState()
        {
            return Pkce.GenerateCodeVerifier();
        }

        private static AuthorizationResult Failure(Exception exception)
        {
            return new AuthorizationResult()
            {
                IsSuccess = false,
                Exception = exception
            };
        }
    }
}
