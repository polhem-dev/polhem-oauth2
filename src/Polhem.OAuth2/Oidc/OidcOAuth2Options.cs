namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options for any OpenID Connect provider, such as Keycloak, GitLab, AWS Cognito or Authentik. Set the endpoints
    /// and <see cref="Issuer"/> yourself, or read them from the discovery document of the provider with
    /// <see cref="FromDiscoveryAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The user information comes from <see cref="OAuth2Options.UserInfoEndpoint"/>. When it is empty, the user comes from the
    /// ID token that the token endpoint returns instead, which needs <see cref="Issuer"/> and the <c>openid</c> scope.
    /// </para>
    /// <para>
    /// The sign-in is the authorization code flow with PKCE. Whether a provider accepts the redirect URI of a loopback or
    /// .NET MAUI client depends on the rules of that provider.
    /// </para>
    /// </remarks>
    public sealed class OidcOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// The provider name that <see cref="ProviderName"/> starts with.
        /// </summary>
        internal const string DefaultProviderName = "OpenID Connect";

        private const string OpenIdScope = "openid";

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcOAuth2Options"/> class.
        /// </summary>
        public OidcOAuth2Options()
        {
            Scopes = new[] { OpenIdScope, "profile", "email" };
        }

        /// <summary>
        /// Gets or sets the provider name, which <see cref="OAuth2Client.ProviderName"/> and
        /// <see cref="AuthorizationResult.ProviderName"/> report and error messages use. The default is <c>OpenID Connect</c>.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: An application that signs in with more than one OpenID Connect provider gives each one a name of its own,
        /// such as <c>Keycloak</c> or <c>GitLab</c>. A user is identified by <see cref="UserInfo.UserId"/> together with the
        /// provider name, and the user IDs of two providers can be the same value for different people.
        /// </remarks>
        public string ProviderName { get; set; } = DefaultProviderName;

        /// <summary>
        /// Gets or sets the issuer identifier of the provider, such as <c>https://idp.example.com/realms/demo</c>: an absolute
        /// https URI without a query or a fragment, written exactly as the <c>iss</c> claim of its ID tokens. It is optional
        /// while <see cref="OAuth2Options.UserInfoEndpoint"/> is set, and required when it is empty.
        /// </summary>
        /// <remarks>
        /// When it is set, the <c>iss</c> claim of the ID token must equal it, compared ordinally, or the sign-in fails.
        /// <see cref="FromDiscoveryAsync"/> sets it to the <c>issuer</c> of the discovery document.
        /// </remarks>
        public string Issuer { get; set; } = string.Empty;

        /// <inheritdoc/>
        internal override bool RequiresUserInfoEndpoint => false;

        /// <summary>
        /// Creates options from the discovery document of a provider, <c>/.well-known/openid-configuration</c> under the
        /// authority: the authorization, token and user information endpoints, <see cref="Issuer"/>, and
        /// <see cref="OAuth2Options.ClientAuthentication"/>. Set <see cref="OAuth2Options.ClientId"/>,
        /// <see cref="OAuth2Options.RedirectUri"/> and the other settings of the application on the result.
        /// </summary>
        /// <param name="authority">
        /// The issuer identifier of the provider, such as <c>https://idp.example.com/realms/demo</c>: an absolute https URI
        /// without a query or a fragment.
        /// </param>
        /// <param name="httpClient">
        /// The HTTP client for the request, or null to use a shared instance. It does not follow redirects, so a provider that
        /// redirects the request fails with its status code. The method does not dispose it.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The options that the document describes.</returns>
        /// <remarks>
        /// <para>
        /// The <c>issuer</c> of the document must equal <paramref name="authority"/>, as OpenID Connect Discovery 1.0, section
        /// 4.3, requires, compared ordinally except that a single trailing slash on either side is ignored:
        /// <c>https://tenant.auth0.com</c> matches the issuer <c>https://tenant.auth0.com/</c>. <see cref="Issuer"/> is set to
        /// the value of the document. A provider whose issuer is a template, such as the <c>common</c> tenant of Microsoft
        /// Entra ID, cannot be read this way.
        /// </para>
        /// <para>
        /// The document must name an authorization endpoint and a token endpoint, and every endpoint must be an absolute https
        /// URI without a fragment. When it lists <c>code_challenge_methods_supported</c>, the list must contain <c>S256</c>.
        /// <see cref="OAuth2Options.ClientAuthentication"/> is <see cref="ClientAuthenticationMethod.ClientSecretPost"/> when
        /// <c>token_endpoint_auth_methods_supported</c> lists <c>client_secret_post</c>, and
        /// <see cref="ClientAuthenticationMethod.ClientSecretBasic"/> otherwise, the default of the specification.
        /// </para>
        /// <para>
        /// The method sends a request each time it is called and caches nothing. Call it once when the application starts,
        /// for example before registering the client with dependency injection, not for each sign-in.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="authority"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="authority"/> is not an absolute https URI without a query or a fragment.</exception>
        /// <exception cref="HttpRequestException">The request failed, or the provider returned an unsuccessful status code.</exception>
        /// <exception cref="System.Text.Json.JsonException">The response is not a JSON object.</exception>
        /// <exception cref="OAuth2Exception">
        /// The issuer of the document does not match <paramref name="authority"/>, a required endpoint is missing or is not an
        /// absolute https URI without a fragment, or the provider supports neither the authorization code flow nor the
        /// S256 code challenge method.
        /// </exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public static Task<OidcOAuth2Options> FromDiscoveryAsync(
            string authority, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
        {
            return OidcDiscovery.LoadAsync(authority, httpClient, cancellationToken);
        }

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new OidcOAuth2Provider(this, httpClientFactory);
        }

        /// <inheritdoc/>
        private protected override string? GetProviderSettingsError()
        {
            if (string.IsNullOrWhiteSpace(ProviderName))
                return $"{nameof(ProviderName)} is required.";
            if (!string.IsNullOrEmpty(Issuer) && !IsIssuerUri(Issuer))
                return $"{nameof(Issuer)} must be an absolute https URI without a query or a fragment.";
            if (string.IsNullOrEmpty(UserInfoEndpoint))
            {
                if (string.IsNullOrEmpty(Issuer))
                    return $"{nameof(Issuer)} is required when {nameof(UserInfoEndpoint)} is empty, because the user is read from the ID token.";
                if (Scopes is null || !Scopes.Contains(OpenIdScope, StringComparer.Ordinal))
                    return $"{nameof(Scopes)} must contain '{OpenIdScope}' when {nameof(UserInfoEndpoint)} is empty, because the user is read from the ID token.";
            }
            return null;
        }

        // OpenID Connect Discovery 1.0, section 2: an issuer is a case-sensitive https URL without a query or a fragment.
        internal static bool IsIssuerUri(string? issuer)
        {
            return IsEndpointUri(issuer) && issuer!.IndexOf('?') < 0;
        }
    }
}
