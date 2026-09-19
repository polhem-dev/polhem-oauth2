namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for OAuth2 options: the client credentials, the redirect URI and the provider endpoints. Each supported
    /// provider has its own derived type.
    /// </summary>
    public abstract class OAuth2Options
    {
        // Schemes an application redirect URI cannot use. Uri reports the scheme in lower case.
        private static readonly HashSet<string> s_nonAppSchemes = new(StringComparer.Ordinal)
        {
            Uri.UriSchemeHttp, "javascript", "data", Uri.UriSchemeFile
        };

        private protected OAuth2Options()
        {
        }

        /// <summary>
        /// Gets or sets the client ID that identifies the application to the provider. It is required.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client secret that authenticates the application to the provider.
        /// </summary>
        /// <remarks>Keep this value out of source control and logs.</remarks>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URI the provider sends the user back to after sign-in. It is required, and must match a redirect URI
        /// registered with the provider.
        /// </summary>
        /// <remarks>
        /// <see cref="OAuth2Client"/> requires an absolute http or https URI, and <see cref="LoopbackOAuth2Client"/> an http URI
        /// on a loopback address. <see cref="AppOAuth2Client"/> requires an absolute https URI or a custom scheme URI, such as
        /// <c>com.example.app:/oauth2redirect</c>, without a fragment.
        /// </remarks>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scopes to request.
        /// </summary>
        public string[] Scopes { get; set; } = new[] { "openid", "email", "profile" };

        /// <summary>
        /// Gets or sets the authorization endpoint. It must be an absolute https URI.
        /// </summary>
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token endpoint, where the authorization code is exchanged for tokens. It must be an absolute https URI.
        /// </summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user information endpoint, which returns details such as the user's name and email address.
        /// It must be an absolute https URI.
        /// </summary>
        public string UserInfoEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the flow uses PKCE. The default is <see langword="true"/>.
        /// </summary>
        /// <remarks><see cref="LoopbackOAuth2Client"/> and <see cref="AppOAuth2Client"/> always use PKCE, whatever this property is set to.</remarks>
        public bool UsePkce { get; set; } = true;

        /// <summary>
        /// Creates a copy that later changes to these options do not affect.
        /// </summary>
        /// <returns>The copy.</returns>
        internal OAuth2Options Clone()
        {
            var copy = (OAuth2Options)MemberwiseClone();
            if (Scopes is not null)
                copy.Scopes = (string[])Scopes.Clone();
            return copy;
        }

        /// <summary>
        /// Checks the settings that every client needs.
        /// </summary>
        /// <param name="appRedirectUri">
        /// Whether the redirect URI is checked with the rules of <see cref="AppOAuth2Client"/> instead of those of a web client.
        /// </param>
        /// <returns>A message that describes the first invalid setting, or null if the settings are valid.</returns>
        internal string? GetValidationError(bool appRedirectUri = false)
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                return $"{nameof(ClientId)} is required.";
            if (appRedirectUri && !IsAppRedirectUri(RedirectUri))
                return $"{nameof(RedirectUri)} must be an absolute https URI or a custom scheme URI, without a fragment.";
            if (!appRedirectUri && !IsWebUri(RedirectUri))
                return $"{nameof(RedirectUri)} must be an absolute http or https URI.";
            if (Scopes is null || Scopes.Any(string.IsNullOrWhiteSpace))
                return $"{nameof(Scopes)} cannot be null or contain an empty scope.";
            return FindInsecureEndpoint() is { } endpoint ? $"{endpoint} must be an absolute https URI." : null;
        }

        /// <summary>
        /// Finds an endpoint that is not an absolute https URI. The client secret, authorization codes and tokens travel to
        /// these endpoints, so none of them may be sent unencrypted.
        /// </summary>
        /// <returns>The name of the first such endpoint property, or null if every endpoint is an absolute https URI.</returns>
        internal string? FindInsecureEndpoint()
        {
            if (!IsHttpsUri(AuthorizationEndpoint))
                return nameof(AuthorizationEndpoint);
            if (!IsHttpsUri(TokenEndpoint))
                return nameof(TokenEndpoint);
            if (!IsHttpsUri(UserInfoEndpoint))
                return nameof(UserInfoEndpoint);
            return null;
        }

        private static bool IsHttpsUri(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks the redirect URI of an application (ADR-006): https or a custom scheme, but not http, a scheme that runs or
        /// embeds content, or a local file, and no fragment, which the provider could not append its parameters after.
        /// </summary>
        /// <param name="value">The redirect URI.</param>
        /// <returns>True if an application can use the redirect URI.</returns>
        internal static bool IsAppRedirectUri(string value)
        {
            if (value.IndexOf('#') >= 0 || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;
            // A custom scheme needs no period: Facebook requires fb<app id>, and Entra ID on Android requires msauth.
            // On Unix a path such as /callback parses as an absolute file URI, which the file scheme rejects.
            return !s_nonAppSchemes.Contains(uri.Scheme);
        }

        // On Unix a path such as /callback parses as an absolute file URI, so the scheme is checked as well.
        private static bool IsWebUri(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));
        }
    }
}
