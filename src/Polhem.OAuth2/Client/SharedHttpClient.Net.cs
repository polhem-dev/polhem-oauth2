namespace Polhem.OAuth2
{
    internal static partial class SharedHttpClient
    {
        internal static partial HttpMessageHandler CreateHandler()
        {
            // A pooled connection is replaced after five minutes, so that a long-running process follows DNS changes of the
            // provider host names. The value is written here instead of in a static field, whose initializer might run after
            // the one in the other part of this class.
            return new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5), AllowAutoRedirect = false };
        }
    }
}
