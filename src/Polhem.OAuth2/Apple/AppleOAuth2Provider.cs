using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Sign in with Apple provider.
    /// </summary>
    internal sealed class AppleOAuth2Provider : OpenIdConnectProvider
    {
        /// <summary>
        /// The issuer of Apple's ID tokens, from https://appleid.apple.com/.well-known/openid-configuration.
        /// </summary>
        public const string Issuer = "https://appleid.apple.com";

        // Far longer than a name and an email address in JSON. A longer value is ignored rather than parsed, because the
        // browser posts it and anyone can make it as long as the server accepts.
        private const int MaxUserLength = 4096;

        private static readonly string[] s_issuers = { Issuer };
        private static readonly string[] s_formPostScopes = { "name", "email" };

        private readonly AppleClientSecret? _clientSecret;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppleOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Apple OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        /// <param name="clock">Returns the current time for the client secret, or null to use the system clock.</param>
        public AppleOAuth2Provider(AppleOAuth2Options options, Func<HttpClient>? httpClientFactory, Func<DateTimeOffset>? clock = null)
            : base(options, httpClientFactory)
        {
            UsesFormPost = options.Scopes
                .SelectMany(scope => scope.Split(' '))
                .Any(scope => s_formPostScopes.Contains(scope, StringComparer.Ordinal));
            if (!string.IsNullOrEmpty(options.PrivateKey))
                _clientSecret = new AppleClientSecret(options.TeamId, options.KeyId, options.ClientId, options.PrivateKey, clock ?? UtcNow);
        }

        /// <inheritdoc/>
        public override string ProviderName => "Apple";

        /// <inheritdoc/>
        /// <remarks>
        /// Apple accepts only an https redirect URI on a domain name, not a loopback or custom scheme URI, and requires the
        /// client secret for every token request, so neither public client can sign in.
        /// </remarks>
        public override bool SupportsPublicClient => false;

        /// <inheritdoc/>
        /// <remarks>
        /// Apple refuses any other response mode when the <c>name</c> or <c>email</c> scope is requested, and accepts a
        /// query redirect for the <c>openid</c> scope alone or for none.
        /// </remarks>
        public override bool UsesFormPost { get; }

        /// <inheritdoc/>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        protected override IReadOnlyCollection<string> IdTokenIssuers => s_issuers;

        /// <inheritdoc/>
        /// <remarks>
        /// Apple has no user information endpoint, so the user is the payload of the ID token, which must be issued to the
        /// client by Apple and name a subject. <see cref="UserInfo.RawJson"/> holds that payload.
        /// </remarks>
        /// <exception cref="OAuth2Exception">The ID token is missing, cannot be read, is issued to another client or by another issuer, or has no subject.</exception>
        public override Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(token);

            if (token.IdToken is not { Length: > 0 } idToken)
                return Task.FromException<UserInfo>(new OAuth2Exception("The token response of Apple has no ID token, from which the user is read."));
            try
            {
                using (var claims = ReadIdTokenClaims(idToken))
                {
                    ReadSubject(claims.RootElement);
                    return Task.FromResult(CreateUserInfo(claims.RootElement, claims.RootElement.GetRawText(), token));
                }
            }
            catch (OAuth2Exception ex)
            {
                return Task.FromException<UserInfo>(ex);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Apple sends the name in the <c>user</c> parameter, and only the first time a user authorizes the application. It is
        /// joined from the strings <c>name.firstName</c> and <c>name.lastName</c>. A value that is not such a JSON object, or is longer
        /// than 4096 characters, is ignored. The email address of the parameter is ignored too, because the ID token carries
        /// it and the browser could have changed the parameter.
        /// </remarks>
        public override UserInfo AddCallbackDetails(UserInfo user, AuthorizationCallback callback)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(callback);

            return user.UserName is null && ReadName(callback.User) is { } name
                ? new UserInfo(user.UserId, name, user.Email, user.RawJson)
                : user;
        }

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAuthorizationParameters(string state, string redirectUri, string? codeChallenge)
        {
            var parameters = base.GetAuthorizationParameters(state, redirectUri, codeChallenge);
            if (UsesFormPost)
                parameters["response_mode"] = "form_post";
            return parameters;
        }

        /// <inheritdoc/>
        /// <remarks>A client secret signed by the library takes precedence over <see cref="OAuth2Options.ClientSecret"/>.</remarks>
        protected override string GetClientSecret()
        {
            return _clientSecret?.GetValue() ?? base.GetClientSecret();
        }

        /// <inheritdoc/>
        protected override Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyCollection<string>>(CreateIdTokenFrontEndException());
        }

        /// <inheritdoc/>
        protected override string? GetFallbackUserName(JsonElement user)
        {
            return null;
        }

        private static string? ReadName(string? userJson)
        {
            if (userJson is not { Length: > 0 and <= MaxUserLength })
                return null;

            try
            {
                using (var document = OAuth2Json.ParseObject(userJson))
                {
                    if (!document.RootElement.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.Object)
                        return null;

                    // Only strings are taken, because the browser posts the value and Apple writes the parts as strings.
                    string?[] parts = { OAuth2Json.GetProtocolString(name, "firstName"), OAuth2Json.GetProtocolString(name, "lastName") };
                    string joined = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
                    return joined.Length == 0 ? null : joined;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static DateTimeOffset UtcNow()
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
