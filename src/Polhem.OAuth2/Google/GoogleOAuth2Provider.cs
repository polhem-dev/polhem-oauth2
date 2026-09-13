namespace Polhem.OAuth2
{
    /// <summary>
    /// The Google OAuth2 provider.
    /// </summary>
    public class GoogleOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Google OAuth2 options.</param>
        public GoogleOAuth2Provider(GoogleOAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "Google";

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAccessTokenParams(string authorizationCode, string codeVerifier = "")
        {
            var requestParams = base.GetAccessTokenParams(authorizationCode, codeVerifier);
            // The base class sends the client secret only when PKCE is not used, but Google requires it either way.
            requestParams["client_secret"] = Options.ClientSecret;
            return requestParams;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null or empty.</exception>
        /// <exception cref="System.Text.Json.JsonException"><paramref name="json"/> is not a JSON object.</exception>
        public override UserInfo ParseUserJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty.");

            using (var document = OAuth2Json.ParseObject(json))
            {
                var root = document.RootElement;
                return new UserInfo
                {
                    UserId = OAuth2Json.GetString(root, "sub"),
                    UserName = OAuth2Json.GetString(root, "name"),
                    Email = OAuth2Json.GetString(root, "email"),
                    RawJson = json
                };
            }
        }
    }
}
