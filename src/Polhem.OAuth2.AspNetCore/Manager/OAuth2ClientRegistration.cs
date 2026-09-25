namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// A client added to the service collection under a name.
    /// </summary>
    internal sealed class OAuth2ClientRegistration
    {
        private IServiceProvider? _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2ClientRegistration"/> class for a client that is complete.
        /// </summary>
        /// <param name="name">The name that identifies the client.</param>
        /// <param name="client">The client.</param>
        public OAuth2ClientRegistration(string name, OAuth2Client client)
        {
            Name = name;
            Client = client;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2ClientRegistration"/> class for a client whose HTTP client comes
        /// from the service provider for each request, once <see cref="Attach"/> has been called.
        /// </summary>
        /// <param name="name">The name that identifies the client.</param>
        /// <param name="options">The OAuth2 options, which are copied and checked here.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider.</param>
        public OAuth2ClientRegistration(string name, OAuth2Options options, Func<IServiceProvider, HttpClient> httpClientFactory)
        {
            Name = name;
            Client = OAuth2Client.Create(options, () => httpClientFactory(
                _services ?? throw new InvalidOperationException("The OAuth2 client was used before the service provider was built.")));
        }

        /// <summary>
        /// Gets the name that identifies the client.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the client.
        /// </summary>
        public OAuth2Client Client { get; }

        /// <summary>
        /// Gives the client the service provider that its HTTP clients come from. The first call wins.
        /// </summary>
        /// <param name="services">The service provider.</param>
        public void Attach(IServiceProvider services)
        {
            _services ??= services;
        }
    }
}
