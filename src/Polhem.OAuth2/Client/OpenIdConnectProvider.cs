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

        /// <summary>
        /// Gets the comparer of the <c>aud</c> and <c>azp</c> claims of an ID token with the client ID, which compares them
        /// ordinally unless the provider writes its client IDs in another form than they may be configured in.
        /// </summary>
        protected virtual IEqualityComparer<string> ClientIdComparer => StringComparer.Ordinal;

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
            return IdToken.ReadClaims(idToken, Options.ClientId, IdTokenIssuers, ClientIdComparer);
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
        /// Creates the exception for <see cref="OAuth2Provider.GetTokenClientIdsAsync"/> of a provider whose front end proves
        /// who signed in with the ID token of the sign-in rather than with an access token.
        /// </summary>
        /// <returns>The exception.</returns>
        protected NotSupportedException CreateIdTokenFrontEndException()
        {
            return new NotSupportedException(
                $"{ProviderName} is an OpenID Connect provider, so a front end proves who signed in with the ID token of the sign-in, "
                + "whose audience is its client ID, rather than with an access token. Validate the ID token in the back end, "
                + "checking its signature, issuer, expiry and audience, for example with JwtBearer authentication, instead of "
                + "passing an access token to this method.");
        }

        /// <summary>
        /// Gets the user name to use when the claims have neither <c>name</c> nor the given and family names.
        /// </summary>
        /// <param name="user">The root object of the user information, or the payload of the ID token.</param>
        /// <returns>The user name, or null when the provider has no other field for it.</returns>
        protected abstract string? GetFallbackUserName(JsonElement user);

        /// <inheritdoc/>
        /// <remarks>
        /// The email address is not returned in <see cref="UserInfo.Email"/> when <c>email_verified</c> is false, so that an
        /// address the provider has not verified is not mistaken for one it has. The claim is read from the same JSON as the
        /// address, the user information or the ID token, and an address without it is kept. <see cref="UserInfo.RawJson"/>
        /// still holds the address.
        /// </remarks>
        protected sealed override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            return new UserInfo(
                OAuth2Json.GetString(user, SubjectClaim),
                GetOidcDisplayName(user) ?? GetFallbackUserName(user),
                IsEmailUnverified(user) ? null : OAuth2Json.GetString(user, "email"),
                json);
        }

        // OpenID Connect Core 1.0, section 5.1, makes email_verified a boolean, and Sign in with Apple sends it as the string
        // "true" or "false" as well.
        private static bool IsEmailUnverified(JsonElement user)
        {
            if (user.ValueKind != JsonValueKind.Object || !user.TryGetProperty("email_verified", out var verified))
                return false;
            return verified.ValueKind == JsonValueKind.False
                || (verified.ValueKind == JsonValueKind.String && string.Equals(verified.GetString(), "false", StringComparison.OrdinalIgnoreCase));
        }
    }
}
