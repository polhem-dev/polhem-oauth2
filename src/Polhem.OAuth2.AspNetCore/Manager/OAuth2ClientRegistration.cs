namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// A client added to the service collection under a name.
    /// </summary>
    internal sealed class OAuth2ClientRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2ClientRegistration"/> class.
        /// </summary>
        /// <param name="name">The name that identifies the client.</param>
        /// <param name="client">The client.</param>
        public OAuth2ClientRegistration(string name, OAuth2Client client)
        {
            Name = name;
            Client = client;
        }

        /// <summary>
        /// Gets the name that identifies the client.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the client.
        /// </summary>
        public OAuth2Client Client { get; }
    }
}
