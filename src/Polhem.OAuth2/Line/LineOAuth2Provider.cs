using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The LINE Login OAuth2 provider.
    /// </summary>
    internal sealed class LineOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The LINE OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public LineOAuth2Provider(LineOAuth2Options options, Func<HttpClient>? httpClientFactory)
            : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "LINE";

        /// <inheritdoc/>
        /// <remarks>
        /// A LINE Login channel that serves web apps only refuses a refresh without the client secret (ADR-004), and a desktop
        /// application registers its loopback redirect URI as a web app. A secret that is set is therefore sent from a public
        /// client as well; a mobile application sets none.
        /// </remarks>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            // The profile response has no email field. LINE puts the address in the ID token, and only when the channel
            // has permission to read it, the email scope was requested, and the user agreed to share it.
            string? email = token?.IdToken is { } idToken ? ReadEmail(idToken) : null;

            return new UserInfo(OAuth2Json.GetString(user, "userId"), OAuth2Json.GetString(user, "displayName"), email, json);
        }

        // The user comes from the profile response, and the ID token only adds the email address, so a token that cannot
        // be read leaves the address out instead of failing the sign-in. The issuer is not checked, for the same reason.
        private string? ReadEmail(string idToken)
        {
            try
            {
                using (var claims = IdToken.ReadClaims(idToken, Options.ClientId, issuer: null))
                {
                    return OAuth2Json.GetProtocolString(claims.RootElement, "email");
                }
            }
            catch (OAuth2Exception)
            {
                return null;
            }
        }
    }
}
