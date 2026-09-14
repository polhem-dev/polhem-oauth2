namespace Polhem.OAuth2
{
    internal static partial class SharedHttpClient
    {
        private static partial HttpMessageHandler CreateHandler()
        {
            // netstandard2.0 has no connection lifetime setting. Applications that need one pass in their own HttpClient.
            return new HttpClientHandler();
        }
    }
}
