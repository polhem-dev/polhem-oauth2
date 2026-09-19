namespace OAuthSamples
{
    /// <summary>
    /// The kind of application a set of client credentials belongs to. Providers register desktop, web and mobile clients
    /// separately, so every provider section of <c>OAuthConfig.json</c> holds one client per kind.
    /// </summary>
    public enum OAuthClientType
    {
        /// <summary>A desktop or console application that signs in through the system browser and a loopback redirect.</summary>
        Desktop,

        /// <summary>A web application that signs in through a redirect back to one of its own URLs.</summary>
        Web,

        /// <summary>
        /// An iOS or Mac Catalyst application that signs in directly with a custom scheme or https redirect (ADR-006). Its
        /// section is named <c>iOS</c>. It is a public client, so the section cannot hold a client secret.
        /// </summary>
        Ios,

        /// <summary>
        /// An Android application that signs in directly with a custom scheme or https redirect (ADR-006). It is a public
        /// client, so its section cannot hold a client secret.
        /// </summary>
        Android
    }
}
