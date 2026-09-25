namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options for Auth0. Setting <see cref="Domain"/> fills in the endpoints.
    /// </summary>
    public sealed class Auth0OAuth2Options : OAuth2Options
    {
        private string _domain = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="Auth0OAuth2Options"/> class.
        /// </summary>
        public Auth0OAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
        }

        /// <summary>
        /// Gets or sets the Auth0 domain, for example <c>your-tenant.auth0.com</c>. An <c>https://</c> prefix and a trailing
        /// slash are accepted and removed. Setting it updates the authorization, token and user information endpoints, and an
        /// empty value clears them.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The value has a scheme other than https, or includes user information, a path, a query or a fragment.
        /// </exception>
        public string Domain
        {
            get => _domain;
            set
            {
                _domain = ProviderDomain.Normalize(value);
                bool hasDomain = _domain.Length != 0;
                AuthorizationEndpoint = hasDomain ? $"https://{_domain}/authorize" : string.Empty;
                TokenEndpoint = hasDomain ? $"https://{_domain}/oauth/token" : string.Empty;
                UserInfoEndpoint = hasDomain ? $"https://{_domain}/userinfo" : string.Empty;
            }
        }

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new Auth0OAuth2Provider(this, httpClientFactory);
        }
    }
}
