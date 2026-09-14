using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Sends raw HTTP requests to a loopback listener, the way a browser follows a redirect.
    /// </summary>
    internal static class LoopbackTestHttp
    {
        public static Task<string> GetAsync(Uri baseUri, string pathAndQuery, string? host = null)
        {
            var address = string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                ? IPAddress.Loopback
                : IPAddress.Parse(baseUri.DnsSafeHost);
            return SendAsync(address, baseUri.Port, $"GET {pathAndQuery} HTTP/1.1\r\nHost: {host ?? baseUri.Authority}\r\n\r\n");
        }

        public static async Task<string> SendAsync(IPAddress address, int port, string request)
        {
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, port);
            var stream = client.GetStream();
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        public static string? GetQueryValue(string url, string name)
        {
            return GetParameter(new Uri(url).Query.TrimStart('?'), name);
        }

        public static string? GetParameter(string query, string name)
        {
            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals > 0 && string.Equals(pair.Substring(0, equals), name, StringComparison.Ordinal))
                    return Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '));
            }
            return null;
        }
    }
}
