using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// A token endpoint on the loopback interface that records the body of one token request and rejects it with status 400.
    /// </summary>
    internal sealed class FakeTokenEndpoint : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public FakeTokenEndpoint()
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/token";
            RequestBody = ReceiveAsync();
        }

        public string Url { get; }

        public Task<string> RequestBody { get; }

        public void Dispose()
        {
            _listener.Stop();
        }

        private async Task<string> ReceiveAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            int contentLength = 0;
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(line["Content-Length:".Length..].Trim(), CultureInfo.InvariantCulture);
            }

            var body = new char[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int count = await reader.ReadAsync(body, read, contentLength - read);
                if (count == 0)
                    break;
                read += count;
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
            return new string(body, 0, read);
        }
    }
}
