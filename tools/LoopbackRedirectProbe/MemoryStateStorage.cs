using Polhem.OAuth2;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// Keeps the state and the PKCE code verifier in memory.
    /// </summary>
    internal sealed class MemoryStateStorage : IStateStorage
    {
        private string? _state;
        private string? _codeVerifier;

        public void SaveState(string value) => _state = value;

        public string? GetState() => _state;

        public void RemoveState() => _state = null;

        public void SaveCodeVerifier(string codeVerifier) => _codeVerifier = codeVerifier;

        public string? GetCodeVerifier() => _codeVerifier;

        public void RemoveCodeVerifier() => _codeVerifier = null;
    }
}
