using Newtonsoft.Json.Linq;

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
        public override UserInfo ParseUserJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty.");

            var jObject = JObject.Parse(json);

            return new UserInfo
            {
                UserId = jObject["sub"]?.ToString(),
                UserName = jObject["name"]?.ToString(),
                Email = jObject["email"]?.ToString(),
                RawJson = json
            };
        }
    }
}
