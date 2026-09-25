using System.Net;

namespace Polhem.OAuth2
{
    internal static partial class SharedHttpClient
    {
        internal static partial HttpMessageHandler CreateHandler()
        {
            // netstandard2.0 has no connection lifetime setting on the handler. UseEndpoint sets one where the platform has it.
            return new HttpClientHandler { AllowAutoRedirect = false };
        }

        internal static partial void UseEndpoint(string endpoint)
        {
            // On .NET Framework the ServicePoint of a host decides how long a pooled connection lives, so a connection to the
            // provider is replaced after five minutes and a DNS change is followed, as the handler on .NET does. The setting
            // applies to every client of the process that talks to that host. Other runtimes ignore it; .NET 8 and later load
            // the .NET build of this package instead, whose handler has a connection lifetime of its own.
            ServicePointManager.FindServicePoint(new Uri(endpoint)).ConnectionLeaseTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
        }
    }
}
