using System.Net.Sockets;

namespace Polhem.OAuth2
{
    internal sealed partial class LoopbackRequest
    {
        // A socket read observes the cancellation token on this target framework, so a read that stalls, as one on a
        // connection the browser opened in advance does, ends when the timeout cancels the token. The connection stays open
        // for the caller to dispose.
        private static async Task<int> ReadHeadAsync(TcpClient client, byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                var stream = client.GetStream();
                return await ReadHeadAsync(buffer, (bytes, offset, count) => stream.ReadAsync(bytes.AsMemory(offset, count), cancellationToken).AsTask())
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The caller finds out whether its own token was canceled; a timeout just means no head arrived.
                return -1;
            }
            catch (IOException)
            {
                return -1;
            }
        }
    }
}
