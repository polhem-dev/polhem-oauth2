using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2OptionsTests
    {
        [Theory]
        [DisplayName("Auth0 accepts a domain with or without the https scheme and a trailing slash")]
        [InlineData("tenant.auth0.com")]
        [InlineData("https://tenant.auth0.com")]
        [InlineData("https://tenant.auth0.com/")]
        [InlineData("  HTTPS://Tenant.Auth0.com/  ")]
        public void Auth0_Domain_NormalizesAndFillsEndpoints(string domain)
        {
            var options = new Auth0OAuth2Options { Domain = domain };

            Assert.Equal("tenant.auth0.com", options.Domain);
            Assert.Equal("https://tenant.auth0.com/authorize", options.AuthorizationEndpoint);
            Assert.Equal("https://tenant.auth0.com/oauth/token", options.TokenEndpoint);
            Assert.Equal("https://tenant.auth0.com/userinfo", options.UserInfoEndpoint);
        }

        [Fact]
        [DisplayName("Okta treats a host name that begins with http as a host name")]
        public void Okta_DomainStartingWithHttp_AddsHttpsScheme()
        {
            var options = new OktaOAuth2Options { Domain = "httpauth.example.com" };

            Assert.Equal("https://httpauth.example.com/oauth2/default/v1/authorize", options.AuthorizationEndpoint);
        }

        [Theory]
        [DisplayName("Auth0 and Okta reject a domain that is not an https host name")]
        [InlineData("http://tenant.example.com")]
        [InlineData("ftp://tenant.example.com")]
        [InlineData("tenant.example.com/path")]
        [InlineData("tenant.example.com?query=1")]
        [InlineData("user@tenant.example.com")]
        public void Domain_NotHttpsHostName_ThrowsArgumentException(string domain)
        {
            Assert.Throws<ArgumentException>(() => new Auth0OAuth2Options { Domain = domain });
            Assert.Throws<ArgumentException>(() => new OktaOAuth2Options { Domain = domain });
        }

        [Theory]
        [DisplayName("An empty domain clears the endpoints of Auth0 and Okta")]
        [InlineData("")]
        [InlineData("   ")]
        public void Domain_Empty_ClearsEndpoints(string domain)
        {
            var auth0 = new Auth0OAuth2Options { Domain = "tenant.auth0.com" };
            var okta = new OktaOAuth2Options { Domain = "dev-123456.okta.com" };

            auth0.Domain = domain;
            okta.Domain = domain;

            Assert.All(
                new[] { auth0.AuthorizationEndpoint, auth0.TokenEndpoint, auth0.UserInfoEndpoint, okta.AuthorizationEndpoint, okta.TokenEndpoint, okta.UserInfoEndpoint },
                Assert.Empty);
        }

        [Fact]
        [DisplayName("Okta uses the default custom authorization server unless another is set")]
        public void Okta_DefaultAuthorizationServer_UsesDefaultServerPaths()
        {
            var options = new OktaOAuth2Options { Domain = "dev-123456.okta.com" };

            Assert.Equal("https://dev-123456.okta.com/oauth2/default/v1/authorize", options.AuthorizationEndpoint);
            Assert.Equal("https://dev-123456.okta.com/oauth2/default/v1/token", options.TokenEndpoint);
            Assert.Equal("https://dev-123456.okta.com/oauth2/default/v1/userinfo", options.UserInfoEndpoint);
        }

        [Fact]
        [DisplayName("Okta builds the endpoints of a custom authorization server")]
        public void Okta_CustomAuthorizationServer_UsesServerId()
        {
            var options = new OktaOAuth2Options { Domain = "dev-123456.okta.com", AuthorizationServerId = "aus123" };

            Assert.Equal("https://dev-123456.okta.com/oauth2/aus123/v1/token", options.TokenEndpoint);
        }

        [Theory]
        [DisplayName("Okta uses the org authorization server when the server ID is empty, in either order of setting")]
        [InlineData("")]
        [InlineData(" / ")]
        public void Okta_EmptyAuthorizationServerId_UsesOrgAuthorizationServer(string serverId)
        {
            var serverFirst = new OktaOAuth2Options { AuthorizationServerId = serverId, Domain = "dev-123456.okta.com" };
            var domainFirst = new OktaOAuth2Options { Domain = "dev-123456.okta.com", AuthorizationServerId = serverId };

            foreach (var options in new[] { serverFirst, domainFirst })
            {
                Assert.Equal("https://dev-123456.okta.com/oauth2/v1/authorize", options.AuthorizationEndpoint);
                Assert.Equal("https://dev-123456.okta.com/oauth2/v1/token", options.TokenEndpoint);
                Assert.Equal("https://dev-123456.okta.com/oauth2/v1/userinfo", options.UserInfoEndpoint);
            }
        }

        [Fact]
        [DisplayName("Microsoft Entra ID uses the common tenant by default")]
        public void Azure_DefaultTenant_UsesCommon()
        {
            var options = new AzureOAuth2Options();

            Assert.Equal("common", options.Tenant);
            Assert.Equal("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", options.AuthorizationEndpoint);
            Assert.Equal("https://login.microsoftonline.com/common/oauth2/v2.0/token", options.TokenEndpoint);
        }

        [Theory]
        [DisplayName("Microsoft Entra ID builds the authorization and token endpoints from the tenant")]
        [InlineData("contoso.onmicrosoft.com")]
        [InlineData("organizations")]
        [InlineData("00000000-0000-0000-0000-000000000001")]
        public void Azure_Tenant_BuildsEndpoints(string tenant)
        {
            var options = new AzureOAuth2Options { Tenant = tenant };

            Assert.Equal($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize", options.AuthorizationEndpoint);
            Assert.Equal($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", options.TokenEndpoint);
        }

        [Fact]
        [DisplayName("Microsoft Entra ID falls back to the common tenant for an empty value")]
        public void Azure_EmptyTenant_UsesCommon()
        {
            var options = new AzureOAuth2Options { Tenant = "contoso.onmicrosoft.com" };

            options.Tenant = " ";

            Assert.Equal("common", options.Tenant);
            Assert.Equal("https://login.microsoftonline.com/common/oauth2/v2.0/token", options.TokenEndpoint);
        }

        [Theory]
        [DisplayName("IsAppRedirectUri accepts https and custom scheme URIs, including schemes without a period")]
        [InlineData("https://app.example.com/oauth2redirect")]
        [InlineData("com.example.app:/oauth2redirect")]
        [InlineData("com.example.app:/oauth2redirect?source=app")]
        [InlineData("com.googleusercontent.apps.123-abc:/oauthredirect")]
        [InlineData("msauth://com.example.app/2jmj7l5rSw0yVb%2FvlWAYkK%2FYBwk%3D")]
        [InlineData("msauth.com.example.app://auth")]
        [InlineData("line3rdp.com.example.app://auth")]
        [InlineData("fb1234567890://authorize")]
        public void IsAppRedirectUri_AppRedirectUri_ReturnsTrue(string redirectUri)
        {
            Assert.True(OAuth2Options.IsAppRedirectUri(redirectUri));
        }

        [Theory]
        [DisplayName("IsAppRedirectUri rejects null, http, script, data and file URIs, relative URIs and URIs with a fragment")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("http://app.example.com/oauth2redirect")]
        [InlineData("http://127.0.0.1:53682/callback")]
        [InlineData("HTTP://app.example.com/oauth2redirect")]
        [InlineData("javascript:alert(1)")]
        [InlineData("JavaScript:alert(1)")]
        [InlineData("data:text/html,x")]
        [InlineData("file:///tmp/callback")]
        [InlineData("/oauth2redirect")]
        [InlineData("oauth2redirect")]
        [InlineData("com.example.app:/oauth2redirect#fragment")]
        public void IsAppRedirectUri_OtherValue_ReturnsFalse(string? redirectUri)
        {
            Assert.False(OAuth2Options.IsAppRedirectUri(redirectUri));
        }

        [Fact]
        [DisplayName("Facebook uses the same Graph API version for every endpoint")]
        public void Facebook_Endpoints_ShareGraphApiVersion()
        {
            var options = new FacebookOAuth2Options();

            var versions = new[] { options.AuthorizationEndpoint, options.TokenEndpoint, options.UserInfoEndpoint }
                .Select(endpoint => new Uri(endpoint).Segments[1])
                .Distinct()
                .ToList();

            string version = Assert.Single(versions);
            Assert.StartsWith("v", version, StringComparison.Ordinal);
        }
    }
}
