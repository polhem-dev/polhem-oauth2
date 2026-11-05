namespace Polhem.OAuth2
{
    /// <summary>
    /// Signs a user in from a desktop or console application through its own back end, which runs the sign-in with the
    /// provider and returns a single-use code (ADR-006). The back end's sign-in URL opens in the default browser,
    /// and the back end redirects to a loopback address on which this client listens, as described in RFC 8252.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use it when the application has a back end of its own, such as an ASP.NET Core application with
    /// <c>AddOAuth2AppRelay</c> of Polhem.OAuth2.AspNetCore. The provider's tokens and client secret stay on the back end, and
    /// the application learns who signed in from the back end, which issues its own session. An application without a back
    /// end signs in with <see cref="LoopbackOAuth2Client"/> instead.
    /// </para>
    /// <para>
    /// The client does not define how the application talks to its back end. It creates the PKCE code verifier, the
    /// application redirect URI and the state, asks the application for the sign-in URL of the back end, and returns the code
    /// with the verifier. The application then sends both to its back end to redeem them.
    /// </para>
    /// <para>
    /// The application redirect URI is the configured URI with the port that was bound and a <c>state</c> query, such as
    /// <c>http://127.0.0.1:53682/callback?state=...</c>. Register the configured URI with the back end without a port, such as
    /// <c>http://127.0.0.1/callback</c>, so any port is accepted.
    /// </para>
    /// </remarks>
    public sealed class LoopbackRelayClient
    {
        private readonly Uri _redirectUri;
        private TimeSpan _timeout = TimeSpan.FromMinutes(5);
        private int _inProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoopbackRelayClient"/> class.
        /// </summary>
        /// <param name="redirectUri">
        /// The loopback URI to listen on: <c>http</c>, the address <c>127.0.0.1</c> or <c>[::1]</c>, a port, and a path, such as
        /// <c>http://127.0.0.1:0/callback</c>. Port 0 picks a free port for each sign-in.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="redirectUri"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="redirectUri"/> is not an http URI of 127.0.0.1 or [::1] with a path, or it has user information, a
        /// query or a fragment. <c>localhost</c> is not accepted, because the back end accepts only these addresses.
        /// </exception>
        public LoopbackRelayClient(string redirectUri)
        {
            ArgumentNullException.ThrowIfNull(redirectUri);
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttp
                || uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6)
                || uri.Host is not ("127.0.0.1" or "[::1]")
                || uri.UserInfo.Length > 0
                || uri.Query.Length > 0
                || uri.Fragment.Length > 0
                || uri.AbsolutePath.Length <= 1)
            {
                throw new ArgumentException(
                    "The redirect URI must be an http URI of 127.0.0.1 or [::1] with a path, and without user information, a query or a fragment.",
                    nameof(redirectUri));
            }

            _redirectUri = uri;
        }

        /// <summary>
        /// Gets or sets how long <see cref="SignInAsync"/> waits for the back end to redirect back. The default is 5 minutes.
        /// </summary>
        /// <remarks>
        /// The time is counted from just before the sign-in URL is opened, so the time that <see cref="OpenBrowser"/> takes
        /// counts as well. <see cref="SignInAsync"/> reads this property when it starts, so a change made during a sign-in
        /// applies to the next one.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive, or is longer than <see cref="int.MaxValue"/> milliseconds.</exception>
        public TimeSpan Timeout
        {
            get => _timeout;
            set => _timeout = LoopbackCallbackReceiver.ValidateTimeout(value);
        }

        /// <summary>
        /// Gets or sets the function that opens the sign-in URL of the back end. When it is null, the URL opens in the default
        /// browser of the operating system. Set it to open the URL another way, for example through the launcher of a UI framework.
        /// </summary>
        /// <remarks>
        /// <see cref="SignInAsync"/> calls it after the listener has started, and waits for the returned task before it waits
        /// for the redirect. An exception from the function or its task propagates from <see cref="SignInAsync"/>.
        /// <see cref="SignInAsync"/> reads this property when it starts, so a change made during a sign-in applies to the next one.
        /// </remarks>
        public Func<Uri, Task>? OpenBrowser { get; set; }

        /// <summary>
        /// Opens the sign-in URL of the back end in the browser, and waits for the back end to redirect back with a code.
        /// </summary>
        /// <param name="createSignInUrl">
        /// Returns the sign-in URL of the back end for the application redirect URI and the S256 code challenge that it
        /// receives, for example <c>https://api.example.com/auth/app/GitHub?redirect_uri=...&amp;code_challenge=...</c>. Escape
        /// the redirect URI as a query value.
        /// </param>
        /// <param name="cancellationToken">Cancels the sign-in.</param>
        /// <returns>
        /// A successful result with the code and the code verifier to redeem it with, or a failed result that carries the exception.
        /// </returns>
        /// <remarks>
        /// <para>
        /// These become a failed result: an <see cref="OAuth2Exception"/> when the back end redirects with an error, whose
        /// <see cref="OAuth2Exception.Error"/> is the error, or without a code; a <see cref="TimeoutException"/> when no
        /// redirect arrives within <see cref="Timeout"/>; and an <see cref="OperationCanceledException"/> when
        /// <paramref name="cancellationToken"/> is canceled. Any other exception propagates.
        /// </para>
        /// <para>
        /// Only a request for the redirect path that names the redirect host and carries the state of this sign-in ends the
        /// wait. Other requests to the loopback address, which any web page open in the browser could send, are answered and ignored.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="createSignInUrl"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// A sign-in is already in progress on this client, or <paramref name="createSignInUrl"/> returned null or a URL that is
        /// not an absolute http or https URL.
        /// </exception>
        /// <exception cref="System.Net.Sockets.SocketException">The port of the redirect URI cannot be listened on, for example because it is in use.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception"><see cref="OpenBrowser"/> is null and no browser can be started.</exception>
        /// <exception cref="PlatformNotSupportedException"><see cref="OpenBrowser"/> is null and the platform cannot start a process, as on iOS.</exception>
        public async Task<RelaySignInResult> SignInAsync(Func<Uri, string, Uri> createSignInUrl, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createSignInUrl);
            if (Interlocked.CompareExchange(ref _inProgress, 1, 0) != 0)
                throw new InvalidOperationException("A sign-in is already in progress on this client.");

            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return RelaySignInResult.Failure(new OperationCanceledException(cancellationToken));

                TimeSpan timeoutValue = _timeout;
                Func<Uri, Task>? openBrowser = OpenBrowser;
                string codeVerifier = Pkce.GenerateCodeVerifier();
                // A state has the same requirements as a code verifier: enough random bytes, encoded to be safe in a URL.
                string state = Pkce.GenerateCodeVerifier();

                using (var listener = LoopbackListener.Start(_redirectUri))
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // The back end appends the code after this query, so the redirect carries the state that the receiver
                    // compares, and a request that a web page sends to the loopback address cannot end the sign-in.
                    var appRedirectUri = new Uri(listener.RedirectUri.AbsoluteUri + "?state=" + state);
                    Uri signInUrl = createSignInUrl(appRedirectUri, Pkce.GenerateCodeChallenge(codeVerifier));
                    if (signInUrl is null || !OAuth2Options.IsWebUri(signInUrl))
                        throw new InvalidOperationException("The sign-in URL of the back end must be an absolute http or https URL.");

                    timeout.CancelAfter(timeoutValue);
                    await LoopbackCallbackReceiver.OpenAsync(openBrowser, signInUrl).ConfigureAwait(false);

                    AuthorizationCallback callback;
                    try
                    {
                        callback = await LoopbackCallbackReceiver.ReceiveAsync(listener, state, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        return cancellationToken.IsCancellationRequested
                            ? RelaySignInResult.Failure(ex)
                            : RelaySignInResult.Failure(new TimeoutException("No redirect arrived before the timeout."));
                    }

                    if (!string.IsNullOrEmpty(callback.Error))
                        return RelaySignInResult.Failure(new OAuth2Exception("The back end returned an error.", callback.Error!, callback.ErrorDescription));
                    if (string.IsNullOrEmpty(callback.Code))
                        return RelaySignInResult.Failure(new OAuth2Exception("The back end returned no code."));

                    return RelaySignInResult.Success(callback.Code!, codeVerifier);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _inProgress, 0);
            }
        }
    }
}
