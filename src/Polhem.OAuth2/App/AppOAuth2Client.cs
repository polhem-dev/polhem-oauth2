namespace Polhem.OAuth2
{
    /// <summary>
    /// Signs a user in from a mobile application, such as a .NET MAUI application on Android, iOS or Mac Catalyst. The
    /// application opens the authorization URL in the system browser session, and the provider redirects back to the
    /// application with a custom scheme or an https URI bound to it, as described in RFC 8252.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client does not depend on a UI framework. The function passed to the constructor opens the authorization URL and
    /// returns the URI the provider redirected to. With .NET MAUI it returns <c>WebAuthenticatorResult.CallbackUri</c>.
    /// </para>
    /// <para>
    /// <see cref="OAuth2Options.RedirectUri"/> must be an absolute https URI or a custom scheme URI, such as
    /// <c>com.example.app:/oauth2redirect</c>, without a fragment, and it must be registered with the provider. Which forms a
    /// provider accepts depends on the provider and the platform (ADR-006).
    /// </para>
    /// <para>
    /// A client secret distributed with an application can be extracted from it, so it cannot be kept confidential. For that
    /// reason the client always uses PKCE, whatever <see cref="OAuth2Options.UsePkce"/> is set to, and sends a client secret
    /// that is set only to a provider that requires one from a public client, as <see cref="LoopbackOAuth2Client"/> does.
    /// </para>
    /// <para>
    /// The user information is not proof of identity for a server. An application that signs in to its own back end lets the
    /// back end sign the user in instead.
    /// </para>
    /// <para>The options are copied when the client is created, so later changes to them have no effect.</para>
    /// </remarks>
    public sealed class AppOAuth2Client
    {
        private readonly PublicSignIn _signIn;
        private readonly Func<Uri, Uri, CancellationToken, Task<Uri>> _authenticate;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppOAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <param name="authenticate">
        /// Opens the authorization URL and returns the URI the provider redirected to. It receives the authorization URL, the
        /// redirect URI and the cancellation token of <see cref="SignInAsync"/>.
        /// </param>
        /// <param name="httpClient">
        /// The HTTP client for requests to the provider, or null to use a shared instance. The client does not dispose it.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="authenticate"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The redirect URI is not an absolute https URI or custom scheme URI without a fragment, the client ID is empty, a
        /// scope is empty, or an endpoint is not an absolute https URI without a fragment.
        /// </exception>
        public AppOAuth2Client(
            OAuth2Options options, Func<Uri, Uri, CancellationToken, Task<Uri>> authenticate, HttpClient? httpClient = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(authenticate);

            _signIn = new PublicSignIn(new OAuth2Client(options, httpClient, publicClient: true, appRedirectUri: true));
            _authenticate = authenticate;
        }

        /// <summary>
        /// Opens the authorization URL through the function passed to the constructor, reads the parameters of the URI the
        /// provider redirected to, and exchanges the authorization code for tokens and user information.
        /// </summary>
        /// <param name="cancellationToken">Cancels the sign-in, including the requests to the provider after the redirect.</param>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// In addition to the failures described on <see cref="OAuth2Client.CompleteAuthorizationAsync"/>, an
        /// <see cref="OperationCanceledException"/> becomes a failed result: when the function throws one, which is how
        /// <c>WebAuthenticator</c> reports that the user closed the sign-in, and when <paramref name="cancellationToken"/> is
        /// canceled. Any other exception from the function propagates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A sign-in is already in progress on this client, or the function returned null.
        /// </exception>
        public async Task<AuthorizationResult> SignInAsync(CancellationToken cancellationToken = default)
        {
            using (_signIn.Enter())
            {
                if (PublicSignIn.CanceledBeforeStart(cancellationToken) is { } canceled)
                    return canceled;

                AuthorizationRequest authorization = _signIn.Client.CreateAuthorizationRequest();
                var redirectUri = new Uri(authorization.Pending.RedirectUri);

                Uri callbackUri;
                try
                {
                    callbackUri = await _authenticate(new Uri(authorization.Url), redirectUri, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The authenticate function returned null instead of the callback URI.");
                }
                catch (OperationCanceledException ex)
                {
                    return AuthorizationResult.Failure(ex);
                }

                return await _signIn.CompleteAsync(CallbackQuery.FromUri(callbackUri), authorization.Pending, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Obtains new tokens with a refresh token. A client secret that is set is sent only to a provider that requires one
        /// from a public client.
        /// </summary>
        /// <param name="refreshToken">The refresh token from an earlier <see cref="TokenResponse"/>.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The new tokens. Keep the refresh token of the new response if it has one.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="refreshToken"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="refreshToken"/> is empty.</exception>
        /// <exception cref="NotSupportedException">The provider does not issue refresh tokens, as with Facebook.</exception>
        /// <exception cref="OAuth2Exception">The token endpoint returned an error, or the response has no access token or names a token type other than Bearer.</exception>
        /// <exception cref="HttpRequestException">
        /// The request failed, or the token endpoint returned an unsuccessful status code without an error code.
        /// </exception>
        /// <exception cref="System.Text.Json.JsonException">A successful response is not a JSON object.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return _signIn.Client.RefreshTokenAsync(refreshToken, cancellationToken);
        }
    }
}
