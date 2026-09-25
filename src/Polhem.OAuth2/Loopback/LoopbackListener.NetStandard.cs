using System.Net.Sockets;

namespace Polhem.OAuth2
{
    internal sealed partial class LoopbackListener
    {
        // AcceptTcpClientAsync takes no cancellation token on this target framework. Dispose stops the listener, which ends
        // the accept with an exception.
        private static partial Task<TcpClient> AcceptAsync(TcpListener listener, CancellationToken stopping)
        {
            return listener.AcceptTcpClientAsync();
        }
    }
}
