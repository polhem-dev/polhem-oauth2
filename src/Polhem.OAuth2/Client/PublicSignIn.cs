namespace Polhem.OAuth2
{
    /// <summary>
    /// The parts of a sign-in that <see cref="LoopbackOAuth2Client"/> and <see cref="AppOAuth2Client"/> share: one sign-in
    /// at a time, and the mapping of cancellation to a failed result (ADR-004, ADR-006).
    /// </summary>
    internal sealed class PublicSignIn
    {
        private int _inProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublicSignIn"/> class.
        /// </summary>
        /// <param name="client">The public client that runs the flow.</param>
        public PublicSignIn(OAuth2Client client)
        {
            Client = client;
        }

        /// <summary>
        /// Gets the public client that runs the flow.
        /// </summary>
        public OAuth2Client Client { get; }

        /// <summary>
        /// Marks a sign-in as in progress until the returned object is disposed.
        /// </summary>
        /// <returns>An object that ends the sign-in when disposed.</returns>
        /// <exception cref="InvalidOperationException">A sign-in is already in progress on this client.</exception>
        public IDisposable Enter()
        {
            if (Interlocked.CompareExchange(ref _inProgress, 1, 0) != 0)
                throw new InvalidOperationException("A sign-in is already in progress on this client.");
            return new Exit(this);
        }

        /// <summary>
        /// Returns a failed result when the sign-in is canceled before it starts.
        /// </summary>
        /// <param name="cancellationToken">The token of the caller.</param>
        /// <returns>A failed result with <see cref="OperationCanceledException"/>, or null when the token is not canceled.</returns>
        public static AuthorizationResult? CanceledBeforeStart(CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested
                ? AuthorizationResult.Failure(new OperationCanceledException(cancellationToken))
                : null;
        }

        /// <summary>
        /// Completes the sign-in with the parameters of the redirect, turning cancellation by the caller into a failed result.
        /// </summary>
        /// <param name="callback">The parameters of the redirect.</param>
        /// <param name="pending">The values of the authorization request.</param>
        /// <param name="cancellationToken">The token of the caller.</param>
        /// <returns>The result of <see cref="OAuth2Client.CompleteAuthorizationAsync"/>, or a failed result when the caller canceled.</returns>
        public async Task<AuthorizationResult> CompleteAsync(
            AuthorizationCallback callback, PendingAuthorization pending, CancellationToken cancellationToken)
        {
            try
            {
                return await Client.CompleteAuthorizationAsync(callback, pending, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                return AuthorizationResult.Failure(ex);
            }
        }

        private sealed class Exit : IDisposable
        {
            private PublicSignIn? _signIn;

            public Exit(PublicSignIn signIn)
            {
                _signIn = signIn;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _signIn, null) is { } signIn)
                    Interlocked.Exchange(ref signIn._inProgress, 0);
            }
        }
    }
}
