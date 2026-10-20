using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The provider for any OpenID Connect provider, configured by <see cref="OidcOAuth2Options"/>.
    /// </summary>
    internal sealed class OidcOAuth2Provider : OAuth2Provider
    {
        private const string SubjectClaim = "sub";

        private readonly OidcOAuth2Options _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="OidcOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The OpenID Connect options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public OidcOAuth2Provider(OidcOAuth2Options options, Func<HttpClient>? httpClientFactory) : base(options, httpClientFactory)
        {
            _options = options;
        }

        /// <inheritdoc/>
        public override string ProviderName => _options.ProviderName;

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

            if (string.IsNullOrEmpty(Options.UserInfoEndpoint))
            {
                if (token.IdToken is not { Length: > 0 } idToken)
                {
                    throw new OAuth2Exception(
                        $"The token response of {ProviderName} has no ID token, from which the user is read because no user information endpoint is set.");
                }
                using (var claims = IdToken.ReadClaims(idToken, Options.ClientId, _options.Issuer))
                {
                    ReadSubject(claims.RootElement);
                    return CreateUserInfo(claims.RootElement, claims.RootElement.GetRawText(), token);
                }
            }

            string? idTokenSubject = null;
            if (token.IdToken is { Length: > 0 } signInIdToken)
            {
                using (var claims = IdToken.ReadClaims(signInIdToken, Options.ClientId, _options.Issuer))
                {
                    idTokenSubject = ReadSubject(claims.RootElement);
                }
            }

            var user = await base.GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);
            if (idTokenSubject is not null && !string.Equals(user.UserId, idTokenSubject, StringComparison.Ordinal))
                throw new OAuth2Exception("The user information names another subject than the ID token.");
            return user;
        }

        // OpenID Connect Core 1.0, section 2, makes sub a required string claim of every ID token.
        private static string ReadSubject(JsonElement claims)
        {
            return OAuth2Json.GetProtocolString(claims, SubjectClaim) is { Length: > 0 } subject
                ? subject
                : throw new OAuth2Exception("The ID token has no subject.");
        }

        /// <inheritdoc/>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(new NotSupportedException(
                $"{ProviderName} is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "checking its signature, issuer, expiry and audience, for example with JwtBearer authentication, instead of "
                + "passing an access token to this method."));
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, SubjectClaim),
                GetOidcDisplayName(user) ?? OAuth2Json.GetString(user, "preferred_username") ?? OAuth2Json.GetString(user, "nickname"),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
