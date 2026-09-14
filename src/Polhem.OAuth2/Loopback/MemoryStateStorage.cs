namespace Polhem.OAuth2
{
    /// <summary>
    /// Keeps the state and the PKCE code verifier of one sign-in in memory.
    /// </summary>
    internal sealed class MemoryStateStorage : IStateStorage
    {
        private string? _state;
        private string? _codeVerifier;

        /// <inheritdoc/>
        public void SaveState(string value) => _state = value;

        /// <inheritdoc/>
        public string? GetState() => _state;

        /// <inheritdoc/>
        public void RemoveState() => _state = null;

        /// <inheritdoc/>
        public void SaveCodeVerifier(string codeVerifier) => _codeVerifier = codeVerifier;

        /// <inheritdoc/>
        public string? GetCodeVerifier() => _codeVerifier;

        /// <inheritdoc/>
        public void RemoveCodeVerifier() => _codeVerifier = null;
    }
}
