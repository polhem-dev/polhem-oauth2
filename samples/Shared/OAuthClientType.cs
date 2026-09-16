namespace OAuthSamples
{
    /// <summary>
    /// The kind of application a set of client credentials belongs to. Providers register a desktop client and a web
    /// client separately, so every provider section of <c>OAuthConfig.json</c> holds one client per kind.
    /// </summary>
    public enum OAuthClientType
    {
        /// <summary>A desktop or console application that signs in through the system browser and a loopback redirect.</summary>
        Desktop,

        /// <summary>A web application that signs in through a redirect back to one of its own URLs.</summary>
        Web
    }
}
