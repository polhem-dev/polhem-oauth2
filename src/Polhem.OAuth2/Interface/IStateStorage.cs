namespace Polhem.OAuth2
{
    /// <summary>
    /// Keeps the <c>state</c> and the PKCE <c>code_verifier</c> between the redirect to the provider and the callback,
    /// for example in a cookie, in session state or in a database.
    /// </summary>
    public interface IStateStorage
    {
        /// <summary>
        /// Stores the state.
        /// </summary>
        /// <param name="value">The state, typically a random string.</param>
        void SaveState(string value);

        /// <summary>
        /// Gets the stored state, to compare it with the state returned to the callback.
        /// </summary>
        /// <returns>The stored state, or null if none is stored.</returns>
        string? GetState();

        /// <summary>
        /// Removes the stored state, typically once the callback has been handled.
        /// </summary>
        void RemoveState();

        /// <summary>
        /// Stores the PKCE code verifier.
        /// </summary>
        /// <param name="codeVerifier">The code verifier generated for this sign-in.</param>
        void SaveCodeVerifier(string codeVerifier);

        /// <summary>
        /// Gets the stored PKCE code verifier, to send it with the token request.
        /// </summary>
        /// <returns>The stored code verifier, or null if none is stored.</returns>
        string? GetCodeVerifier();

        /// <summary>
        /// Removes the stored PKCE code verifier, typically once the token request has been sent.
        /// </summary>
        void RemoveCodeVerifier();
    }
}
