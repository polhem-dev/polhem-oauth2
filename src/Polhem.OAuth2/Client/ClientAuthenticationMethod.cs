namespace Polhem.OAuth2
{
    /// <summary>
    /// How a client sends its client secret to the token endpoint (RFC 6749, section 2.3.1).
    /// </summary>
    public enum ClientAuthenticationMethod
    {
        /// <summary>
        /// The client ID and the client secret are sent in the request body, as <c>client_id</c> and <c>client_secret</c>.
        /// OpenID Connect calls this method <c>client_secret_post</c>.
        /// </summary>
        ClientSecretPost = 0,

        /// <summary>
        /// The client ID and the client secret are sent in an HTTP Basic <c>Authorization</c> header, each form-encoded first.
        /// OpenID Connect calls this method <c>client_secret_basic</c>.
        /// </summary>
        ClientSecretBasic = 1
    }
}
