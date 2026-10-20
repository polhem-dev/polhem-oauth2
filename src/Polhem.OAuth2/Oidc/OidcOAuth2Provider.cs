using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The provider for any OpenID Connect provider, configured by <see cref="OidcOAuth2Options"/>.
    /// </summary>
    internal sealed class OidcOAuth2Provider : OpenIdConnectProvider
    {
        private readonly OidcOAuth2Options _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The OpenID Connect options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <exception cref="ArgumentException">An endpoint or the issuer of <paramref name="options"/> is not valid, or the provider name is empty.</exception>
        public OidcOAuth2Provider(OidcOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
            if (options.GetProviderNameError() is { } error)
                throw new ArgumentException(error, nameof(options));
            _options = options;
            IdTokenIssuers = options.Issuer.Length == 0 ? Array.Empty<string>() : new[] { options.Issuer };
        }

        /// <inheritdoc/>
        public override string ProviderName => _options.ProviderName;

        /// <inheritdoc/>
        /// <remarks><see cref="OidcOAuth2Options.Issuer"/>, or none when it is empty.</remarks>
        protected override IReadOnlyCollection<string> IdTokenIssuers { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Without a user information endpoint, the user is the payload of the ID token, which must be issued to the client by
        /// <see cref="OidcOAuth2Options.Issuer"/>, and <see cref="UserInfo.RawJson"/> holds that payload.
        /// </para>
        /// <para>
        /// With one, an ID token in the token response is checked the same way, and the <c>sub</c> of the user information
        /// must equal its <c>sub</c>, as OpenID Connect Core 1.0, section 5.3.2, requires against token substitution.
        /// </para>
        /// </remarks>
        /// <exception cref="OAuth2Exception">
        /// The ID token is missing while there is no user information endpoint, cannot be read, is issued to another client
        /// or by another issuer, or names another subject than the user information.
        /// </exception>
        public override async Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(token);

            if (!string.IsNullOrEmpty(Options.UserInfoEndpoint))
                return await base.GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);

            if (token.IdToken is not { Length: > 0 } idToken)
            {
                throw new OAuth2Exception(
                    $"The token response of {ProviderName} has no ID token, from which the user is read because no user information endpoint is set.");
            }
            using (var claims = ReadIdTokenClaims(idToken))
            {
                ReadSubject(claims.RootElement);
                return CreateUserInfo(claims.RootElement, claims.RootElement.GetRawText(), token);
            }
        }

        /// <inheritdoc/>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(CreateIdTokenFrontEndException());
        }

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return OAuth2Json.GetString(user, "preferred_username") ?? OAuth2Json.GetString(user, "nickname");
        }
    }
}
