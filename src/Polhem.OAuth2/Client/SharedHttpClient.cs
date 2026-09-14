namespace Polhem.OAuth2
{
    /// <summary>
    /// The HTTP client that providers use when the application does not pass one in.
    /// </summary>
    /// <remarks>
    /// HttpClient is designed to be shared: creating one for each request can exhaust the available sockets under load.
    /// <see cref="CreateHandler"/> has one implementation for each target framework, in <c>SharedHttpClient.Net.cs</c> and
    /// <c>SharedHttpClient.NetStandard.cs</c>, and the project file compiles the one that matches.
    /// </remarks>
    internal static partial class SharedHttpClient
    {
        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static HttpClient Instance { get; } = new HttpClient(CreateHandler());

        private static partial HttpMessageHandler CreateHandler();
    }
}
