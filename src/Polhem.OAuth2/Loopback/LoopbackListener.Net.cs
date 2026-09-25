using System.Net.Sockets;

namespace Polhem.OAuth2
{
    internal sealed partial class LoopbackListener
    {
        private static partial Task<TcpClient> AcceptAsync(TcpListener listener, CancellationToken stopping)
        {
            return listener.AcceptTcpClientAsync(stopping).AsTask();
        }
    }
}
