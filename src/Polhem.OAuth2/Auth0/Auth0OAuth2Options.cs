namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options for Auth0. Setting <see cref="Domain"/> fills in the endpoints.
    /// </summary>
    public class Auth0OAuth2Options : OAuth2Options
    {
        private string _domain = string.Empty;

        /// <summary>
        /// Gets or sets the Auth0 domain, for example <c>your-tenant.auth0.com</c>. Setting a non-empty value
        /// updates the authorization, token and user information endpoints.
        /// </summary>
        public string Domain
        {
            get => _domain;
            set
            {
                _domain = (value ?? string.Empty).TrimEnd('/');
                if (string.IsNullOrEmpty(_domain))
                    return;

                AuthorizationEndpoint = $"https://{_domain}/authorize";
                TokenEndpoint = $"https://{_domain}/oauth/token";
                UserInfoEndpoint = $"https://{_domain}/userinfo";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Auth0OAuth2Options"/> class.
        /// </summary>
        public Auth0OAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
        }
    }
}
