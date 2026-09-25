namespace Polhem.OAuth2
{
    /// <summary>
    /// The HTTP client that providers use when the application does not pass one in.
    /// </summary>
    /// <remarks>
    /// HttpClient is designed to be shared: creating one for each request can exhaust the available sockets under load.
    /// <see cref="CreateHandler"/> has one implementation for each target framework, in <c>SharedHttpClient.Net.cs</c> and
    /// <c>SharedHttpClient.NetStandard.cs</c>, and the project file compiles the one that matches.
    /// <para>
    /// Both handlers turn off automatic redirects. A token request carries the client secret and the authorization code in
    /// its body, which a 307 or 308 redirect would send again to the new location. A redirect response fails the request
    /// instead of being followed.
    /// </para>
    /// </remarks>
    internal static partial class SharedHttpClient
    {
        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static HttpClient Instance { get; } = new HttpClient(CreateHandler());

        internal static partial HttpMessageHandler CreateHandler();
    }
}
