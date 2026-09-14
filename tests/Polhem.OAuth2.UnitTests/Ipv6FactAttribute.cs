using System.Net.Sockets;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// A fact that needs IPv6. On a machine without IPv6 it is reported as skipped, instead of passing without running.
    /// </summary>
    public sealed class Ipv6FactAttribute : FactAttribute
    {
        public Ipv6FactAttribute()
        {
            if (!Socket.OSSupportsIPv6)
                Skip = "This machine does not support IPv6.";
        }
    }
}
