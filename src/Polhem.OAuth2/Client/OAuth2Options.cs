namespace Polhem.OAuth2
{
    /// <summary>
    /// The base class for OAuth2 options: the client credentials, the redirect URI and the provider endpoints. Each supported
    /// provider has its own derived type.
    /// </summary>
    /// <remarks>
    /// Only this package derives from the class, so the options of an application always select one of its providers.
    /// </remarks>
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
        /// Gets or sets the authorization endpoint. It must be an absolute https URI without a fragment.
        /// </summary>
        /// <remarks>
        /// A query is kept, and the parameters of the authorization request are added after it (RFC 6749, section 3.1). Do not
        /// put a parameter there that the client sends itself, such as <c>scope</c> or <c>state</c>, because a parameter must
        /// not appear twice.
        /// </remarks>
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token endpoint, where the authorization code is exchanged for tokens. It must be an absolute https URI
        /// without a fragment. A query is kept.
        /// </summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user information endpoint, which returns details such as the user's name and email address.
        /// It must be an absolute https URI without a fragment. A query is kept.
        /// </summary>
        public string UserInfoEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the flow uses PKCE. The default is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="LoopbackOAuth2Client"/> and <see cref="AppOAuth2Client"/> always use PKCE, whatever this property is set to.
        /// RFC 9700, section 2.1.1, asks every client to use PKCE, confidential ones as well, and this library sends no
        /// <c>nonce</c> in its place. Turn it off only for a provider that refuses the parameters.
        /// </remarks>
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
            return GetEndpointError();
        }

        /// <summary>
        /// Checks that every endpoint is an absolute https URI without a fragment. The client secret, authorization codes and
        /// tokens travel to these endpoints, so none of them may be sent unencrypted, and RFC 6749, section 3.1, allows an
        /// endpoint a query but no fragment, which would swallow the parameters added after it.
        /// </summary>
        /// <returns>A message that names the first endpoint property that is not valid, or null if every endpoint is valid.</returns>
        internal string? GetEndpointError()
        {
            string? endpoint = !IsEndpointUri(AuthorizationEndpoint) ? nameof(AuthorizationEndpoint)
                : !IsEndpointUri(TokenEndpoint) ? nameof(TokenEndpoint)
                : !IsEndpointUri(UserInfoEndpoint) ? nameof(UserInfoEndpoint)
                : null;
            return endpoint is null ? null : $"{endpoint} must be an absolute https URI without a fragment.";
        }

        private static bool IsEndpointUri(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && endpoint.IndexOf('#') < 0;
        }

        /// <summary>
        /// Checks whether a redirect URI can return a sign-in to an application, such as a .NET MAUI application: an absolute
        /// URI with the https scheme or a custom scheme, such as <c>com.example.app:/oauth2redirect</c>, without a fragment.
        /// </summary>
        /// <param name="redirectUri">The redirect URI, or null.</param>
        /// <returns>
        /// True if an application can use the redirect URI; false if it is null or relative, has a fragment, which a provider
        /// could not append its parameters after, or uses the http scheme, a scheme that runs or embeds content, or the file scheme.
        /// </returns>
        /// <remarks>
        /// <see cref="AppOAuth2Client"/> applies this rule to <see cref="RedirectUri"/>, and the back-end relay of
        /// Polhem.OAuth2.AspNetCore applies it to the application redirect URIs it may return to (ADR-006). Whether a provider
        /// accepts a redirect URI that passes depends on the provider and the platform.
        /// </remarks>
        public static bool IsAppRedirectUri(string? redirectUri)
        {
            if (redirectUri is null || redirectUri.IndexOf('#') >= 0 || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
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
