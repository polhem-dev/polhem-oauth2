namespace Polhem.OAuth2
{
    /// <summary>
    /// The values of a sign-in that are kept between the redirect to the provider and the callback.
    /// </summary>
    /// <remarks>
    /// Keep them where only the browser that started the sign-in can present them, for example in an encrypted, HTTP-only
    /// cookie, and remove them once the callback has been handled. <see cref="CodeVerifier"/> must stay confidential.
    /// </remarks>
    public sealed class PendingAuthorization
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PendingAuthorization"/> class from values kept earlier.
        /// </summary>
        /// <param name="state">The <c>state</c> sent with the authorization request.</param>
        /// <param name="codeVerifier">The PKCE <c>code_verifier</c> whose challenge was sent, or null when PKCE is not used.</param>
        /// <param name="redirectUri">The redirect URI sent with the authorization request.</param>
        /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="redirectUri"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="state"/> or <paramref name="redirectUri"/> is empty.</exception>
        public PendingAuthorization(string state, string? codeVerifier, string redirectUri)
        {
            ArgumentNullException.ThrowIfNull(state);
            if (state.Length == 0)
                throw new ArgumentException("The state cannot be empty.", nameof(state));
            ArgumentNullException.ThrowIfNull(redirectUri);
            if (redirectUri.Length == 0)
                throw new ArgumentException("The redirect URI cannot be empty.", nameof(redirectUri));

            State = state;
            CodeVerifier = codeVerifier is { Length: > 0 } ? codeVerifier : null;
            RedirectUri = redirectUri;
        }

        /// <summary>
        /// Gets the <c>state</c> sent with the authorization request.
        /// </summary>
        public string State { get; }

        /// <summary>
        /// Gets the PKCE <c>code_verifier</c>, or null when PKCE is not used.
        /// </summary>
        public string? CodeVerifier { get; }

        /// <summary>
        /// Gets the redirect URI sent with the authorization request. The token request must send the same value.
        /// </summary>
        public string RedirectUri { get; }
    }
}
