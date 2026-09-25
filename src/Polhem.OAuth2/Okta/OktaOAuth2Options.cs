namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options for Okta. Setting <see cref="Domain"/> and <see cref="AuthorizationServerId"/> fills in the endpoints.
    /// </summary>
    public sealed class OktaOAuth2Options : OAuth2Options
    {
        private string _domain = string.Empty;
        private string _authorizationServerId = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="OktaOAuth2Options"/> class.
        /// </summary>
        public OktaOAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
        }

        /// <summary>
        /// Gets or sets the Okta domain, for example <c>dev-123456.okta.com</c>. An <c>https://</c> prefix and a trailing slash
        /// are accepted and removed. Setting it updates the authorization, token and user information endpoints, and an empty
        /// value clears them.
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
                UpdateEndpoints();
            }
        }

        /// <summary>
        /// Gets or sets the ID of the custom authorization server. The default is <c>default</c>, the server that Okta creates
        /// for each organization. An empty value selects the org authorization server instead. Setting it updates the endpoints.
        /// </summary>
        public string AuthorizationServerId
        {
            get => _authorizationServerId;
            set
            {
                _authorizationServerId = (value ?? string.Empty).Trim().Trim('/');
                UpdateEndpoints();
            }
        }

        private void UpdateEndpoints()
        {
            if (_domain.Length == 0)
            {
                AuthorizationEndpoint = string.Empty;
                TokenEndpoint = string.Empty;
                UserInfoEndpoint = string.Empty;
                return;
            }

            string baseUrl = _authorizationServerId.Length == 0
                ? $"https://{_domain}/oauth2/v1"
                : $"https://{_domain}/oauth2/{Uri.EscapeDataString(_authorizationServerId)}/v1";

            AuthorizationEndpoint = baseUrl + "/authorize";
            TokenEndpoint = baseUrl + "/token";
            UserInfoEndpoint = baseUrl + "/userinfo";
        }

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new OktaOAuth2Provider(this, httpClientFactory);
        }
    }
}
