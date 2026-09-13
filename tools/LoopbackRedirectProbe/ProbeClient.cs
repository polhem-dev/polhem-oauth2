using Polhem.OAuth2;

namespace LoopbackRedirectProbe
{
    /// <summary>
    /// An OAuth2 client that keeps the state and the PKCE code verifier in memory for a single sign-in.
    /// </summary>
    internal sealed class ProbeClient : BaseOAuth2Client
    {
        public ProbeClient(OAuth2Options options) : base(options)
        {
        }

        public override IStateStorage StateStorage { get; } = new MemoryStateStorage();
    }
}
