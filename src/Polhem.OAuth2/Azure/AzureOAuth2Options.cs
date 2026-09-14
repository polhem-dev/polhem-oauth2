namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Microsoft Entra ID endpoints and scopes.
    /// </summary>
    public sealed class AzureOAuth2Options : OAuth2Options
    {
        private const string DefaultTenant = "common";

        private string _tenant = DefaultTenant;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOAuth2Options"/> class.
        /// </summary>
        public AzureOAuth2Options()
        {
            Scopes = new[] { "openid", "profile", "email" };
            UserInfoEndpoint = "https://graph.microsoft.com/oidc/userinfo";
            SetTenant(DefaultTenant);
        }

        /// <summary>
        /// Gets or sets the tenant in the authority URL. The default is <c>common</c>, which accepts both work or school and
        /// personal Microsoft accounts. An application registered for a single tenant must use that tenant's ID or domain name;
        /// <c>organizations</c> and <c>consumers</c> are also accepted. An empty value selects <c>common</c>.
        /// Setting it updates the authorization and token endpoints.
        /// </summary>
        public string Tenant
        {
            get => _tenant;
            set => SetTenant(value);
        }

        private void SetTenant(string? tenant)
        {
            string value = tenant?.Trim() ?? string.Empty;
            _tenant = value.Length == 0 ? DefaultTenant : value;

            string authority = "https://login.microsoftonline.com/" + Uri.EscapeDataString(_tenant);
            AuthorizationEndpoint = authority + "/oauth2/v2.0/authorize";
            TokenEndpoint = authority + "/oauth2/v2.0/token";
        }
    }
}
