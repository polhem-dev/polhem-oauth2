using System.Net.Sockets;

namespace Polhem.OAuth2
{
    internal sealed partial class LoopbackRequest
    {
        // Socket reads do not observe a cancellation token on this target framework, so the read passes `CancellationToken.None`
        // and a stalled read is ended by closing the connection when the timeout cancels the token. A browser can open a
        // connection in advance and leave it idle.
        private static async Task<int> ReadHeadAsync(TcpClient client, byte[] buffer, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(client.Dispose))
            {
                try
                {
                    var stream = client.GetStream();
                    return await ReadHeadAsync(buffer, (bytes, offset, count) => stream.ReadAsync(bytes, offset, count, CancellationToken.None)).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return -1;
                }
                catch (InvalidOperationException)
                {
                    // Raised as ObjectDisposedException when the timeout closed the connection.
                    return -1;
                }
            }
        }
    }
}
