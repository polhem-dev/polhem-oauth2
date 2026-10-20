using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for the OpenID Connect providers. It maps the standard claims of the user information and checks that the
    /// user information has the subject of the ID token of the sign-in.
    /// </summary>
    internal abstract class OpenIdConnectProvider : OAuth2Provider
    {
        private const string SubjectClaim = "sub";

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectProvider"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <param name="tokenVerificationEndpoint">
        /// The endpoint that tells which client an access token was issued to, or null when the provider has none.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI without a fragment.</exception>
        protected OpenIdConnectProvider(OAuth2Options options, Func<HttpClient>? httpClientFactory, string? tokenVerificationEndpoint = null)
            : base(options, httpClientFactory, tokenVerificationEndpoint)
        {
        }

        /// <summary>
        /// Gets the values that the <c>iss</c> claim of an ID token must equal one of, or an empty collection when the issuer
        /// cannot be known in advance and is not checked.
        /// </summary>
        protected abstract IReadOnlyCollection<string> IdTokenIssuers { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// An ID token in the token response must be issued to the client, and by one of <see cref="IdTokenIssuers"/> when
        /// there are any, and the <c>sub</c> of the user information must equal its <c>sub</c>, as OpenID Connect Core 1.0,
        /// section 5.3.2, requires against token substitution. A token response without an ID token is not checked.
        /// </remarks>
        /// <exception cref="OAuth2Exception">
        /// The ID token cannot be read, is issued to another client or by another issuer, or names another subject than the
        /// user information, or the user information response is empty.
        /// </exception>
        public override async Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(token);

            // The ID token is read first, so that a token for another client fails before the access token is sent anywhere.
            string? idTokenSubject = null;
            if (token.IdToken is { Length: > 0 } idToken)
            {
                using (var claims = ReadIdTokenClaims(idToken))
                {
                    idTokenSubject = ReadSubject(claims.RootElement);
                }
            }

            var user = await base.GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);
            if (idTokenSubject is not null && !string.Equals(user.UserId, idTokenSubject, StringComparison.Ordinal))
                throw new OAuth2Exception("The user information names another subject than the ID token.");
            return user;
        }

        /// <summary>
        /// Reads the claims of an ID token from the token endpoint, after checking its audience and its issuer.
        /// </summary>
        /// <param name="idToken">The ID token.</param>
        /// <returns>The payload, which the caller disposes.</returns>
        /// <exception cref="OAuth2Exception">The token cannot be read, or is issued to another client or by another issuer.</exception>
        protected JsonDocument ReadIdTokenClaims(string idToken)
        {
            return IdToken.ReadClaims(idToken, Options.ClientId, IdTokenIssuers);
        }

        /// <summary>
        /// Reads the subject of an ID token, which OpenID Connect Core 1.0, section 2, makes a required string claim.
        /// </summary>
        /// <param name="claims">The payload of the ID token.</param>
        /// <returns>The subject, which is not empty.</returns>
        /// <exception cref="OAuth2Exception">The ID token has no subject.</exception>
        protected static string ReadSubject(JsonElement claims)
        {
            return OAuth2Json.GetProtocolString(claims, SubjectClaim) is { Length: > 0 } subject
                ? subject
                : throw new OAuth2Exception("The ID token has no subject.");
        }

        /// <summary>
        /// Gets the user name to use when the claims have neither <c>name</c> nor the given and family names.
        /// </summary>
        /// <param name="user">The root object of the user information, or the payload of the ID token.</param>
        /// <returns>The user name, or null when the provider has no other field for it.</returns>
        protected abstract string? GetFallbackUserName(JsonElement user);

        /// <inheritdoc/>
        protected sealed override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, SubjectClaim),
                GetOidcDisplayName(user) ?? GetFallbackUserName(user),
                OAuth2Json.GetString(user, "email"),
                json);
        }
    }
}
