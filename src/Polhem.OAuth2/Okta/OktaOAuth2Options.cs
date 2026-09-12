namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options for Okta. Setting <see cref="Domain"/> and <see cref="AuthorizationServerId"/> fills in the endpoints.
    /// </summary>
    public class OktaOAuth2Options : OAuth2Options
    {
        private string _domain = string.Empty;
        private string _authorizationServerId = "default";

        /// <summary>
        /// Gets or sets the Okta domain, for example <c>dev-123456.okta.com</c> or <c>https://dev-123456.okta.com</c>.
        /// Setting it updates the authorization, token and user information endpoints.
        /// </summary>
        public string Domain
        {
            get => _domain;
            set
            {
                _domain = (value ?? string.Empty).Trim().TrimEnd('/');
                UpdateEndpoints();
            }
        }

        /// <summary>
        /// Gets or sets the ID of the Okta authorization server. The default is <c>default</c>.
        /// Setting it updates the authorization, token and user information endpoints.
        /// </summary>
        public string AuthorizationServerId
        {
            get => _authorizationServerId;
            set
            {
                _authorizationServerId = string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().Trim('/');
                UpdateEndpoints();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OktaOAuth2Options"/> class.
        /// </summary>
        public OktaOAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
        }

        private void UpdateEndpoints()
        {
            if (string.IsNullOrEmpty(_domain))
                return;

            var baseUrl = _domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? _domain
                : $"https://{_domain}";

            var serverId = string.IsNullOrEmpty(_authorizationServerId) ? "default" : _authorizationServerId;

            AuthorizationEndpoint = $"{baseUrl}/oauth2/{serverId}/v1/authorize";
            TokenEndpoint = $"{baseUrl}/oauth2/{serverId}/v1/token";
            UserInfoEndpoint = $"{baseUrl}/oauth2/{serverId}/v1/userinfo";
        }
    }
}
