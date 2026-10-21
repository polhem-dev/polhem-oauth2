namespace Polhem.OAuth2
{
    /// <summary>
    /// OAuth2 options preset with the Sign in with Apple endpoints and scopes, for the web sign-in of
    /// <see cref="OAuth2Client"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OAuth2Options.ClientId"/> is the Services ID. The client secret is a JSON Web Token signed with the private
    /// key of a Sign in with Apple key. Set <see cref="PrivateKey"/>, <see cref="KeyId"/> and <see cref="TeamId"/> to have
    /// the library sign it and replace it before it expires, or set <see cref="OAuth2Options.ClientSecret"/> to a token that
    /// you signed yourself and replace before it expires. <see cref="PrivateKey"/> takes precedence when both are set.
    /// </para>
    /// <para>
    /// Apple accepts only an https redirect URI on a domain name, not on localhost or an IP address, and requires the client
    /// secret for every token request. <see cref="LoopbackOAuth2Client"/> and <see cref="AppOAuth2Client"/> therefore refuse
    /// these options: a desktop or mobile application signs in through its own back end, as the back-end relay of
    /// Polhem.OAuth2.AspNetCore does.
    /// </para>
    /// <para>
    /// When the name or email scope is requested, Apple returns to the redirect URI with a form POST from its own site
    /// rather than a GET redirect (<see cref="OAuth2Client.UsesFormPostResponse"/>). The callback endpoint of the
    /// application must accept POST.
    /// </para>
    /// <para>
    /// Apple has no user information endpoint. The user ID and the email address come from the ID token, and the name only
    /// from <see cref="AuthorizationCallback.UserJson"/>, which Apple sends the first time a user authorizes the application.
    /// </para>
    /// </remarks>
    public sealed class AppleOAuth2Options : OAuth2Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppleOAuth2Options"/> class.
        /// </summary>
        public AppleOAuth2Options()
        {
            // The endpoints published in https://appleid.apple.com/.well-known/openid-configuration.
            Scopes = new[] { "name", "email" };
            AuthorizationEndpoint = "https://appleid.apple.com/auth/authorize";
            TokenEndpoint = "https://appleid.apple.com/auth/token";
        }

        /// <summary>
        /// Gets or sets the Team ID of the Apple developer account, the issuer of the client secret that the library signs.
        /// It is required when <see cref="PrivateKey"/> is set.
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Key ID of the Sign in with Apple key whose private key is <see cref="PrivateKey"/>. It is required
        /// when <see cref="PrivateKey"/> is set.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the private key of the Sign in with Apple key: the text of the <c>.p8</c> file that Apple issued, from
        /// its <c>BEGIN PRIVATE KEY</c> line to its <c>END PRIVATE KEY</c> line. When it is set, the library signs the client
        /// secret with it and ignores <see cref="OAuth2Options.ClientSecret"/>.
        /// </summary>
        /// <remarks>
        /// Keep the key out of source control and logs, as a client secret. Anyone who has it can sign client secrets for
        /// the services of the App ID it belongs to. The key is checked when the client is created, and error messages never
        /// include it.
        /// </remarks>
        public string PrivateKey { get; set; } = string.Empty;

        /// <inheritdoc/>
        internal override bool RequiresUserInfoEndpoint => false;

        internal override OAuth2Provider CreateProvider(Func<HttpClient>? httpClientFactory)
        {
            return new AppleOAuth2Provider(this, httpClientFactory);
        }

        /// <inheritdoc/>
        private protected override string? GetProviderSettingsError()
        {
            if (!string.IsNullOrEmpty(UserInfoEndpoint))
                return $"{nameof(UserInfoEndpoint)} must be empty, because Apple has no user information endpoint and the user is read from the ID token.";
            // https://appleid.apple.com/.well-known/openid-configuration lists client_secret_post as the only method.
            if (ClientAuthentication != ClientAuthenticationMethod.ClientSecretPost)
                return $"{nameof(ClientAuthentication)} must be {nameof(ClientAuthenticationMethod.ClientSecretPost)}, the only method that Apple accepts.";
            if (string.IsNullOrEmpty(PrivateKey))
            {
                return string.IsNullOrEmpty(ClientSecret)
                    ? $"Set {nameof(PrivateKey)}, {nameof(KeyId)} and {nameof(TeamId)} to have the client secret signed, or set {nameof(ClientSecret)} to a client secret signed in advance."
                    : null;
            }
            if (GetIdentifierError(KeyId, nameof(KeyId)) is { } keyIdError)
                return keyIdError;
            if (GetIdentifierError(TeamId, nameof(TeamId)) is { } teamIdError)
                return teamIdError;
            return AppleSigningKey.GetError(PrivateKey) is { } keyError ? $"{nameof(PrivateKey)} is not valid. {keyError}" : null;
        }

        // A value read from a secret store often ends with a line break, which would reach the client secret and make Apple
        // refuse every token request instead of failing when the client is created.
        private static string? GetIdentifierError(string? value, string name)
        {
            // The null check comes first because netstandard2.0 does not annotate IsNullOrWhiteSpace.
            if (value is null || string.IsNullOrWhiteSpace(value))
                return $"{name} is required when {nameof(PrivateKey)} is set.";
            return value.Trim().Length != value.Length ? $"{name} cannot start or end with white space." : null;
        }
    }
}
