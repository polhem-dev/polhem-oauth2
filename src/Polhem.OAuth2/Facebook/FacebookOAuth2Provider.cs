using Newtonsoft.Json.Linq;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Facebook OAuth2 provider.
    /// </summary>
    public class FacebookOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Facebook OAuth2 options.</param>
        public FacebookOAuth2Provider(FacebookOAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "Facebook";

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAuthorizationUrlParams(string state, string codeChallenge = "")
        {
            var queryParams = base.GetAuthorizationUrlParams(state, codeChallenge);
            // Facebook separates scopes with commas rather than spaces.
            queryParams["scope"] = string.Join(",", Options.Scopes);
            return queryParams;
        }

        /// <inheritdoc/>
        protected override string GetUserInfoUrl()
        {
            // The Graph API returns only the fields that are asked for.
            var fields = "id,name,email,picture";
            return $"{Options.UserInfoEndpoint}?fields={Uri.EscapeDataString(fields)}";
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
                UserId = jObject["id"]?.ToString(),
                UserName = jObject["name"]?.ToString(),
                Email = jObject["email"]?.ToString(),
                RawJson = json
            };
        }

        /// <summary>
        /// Not supported, because Facebook Login does not issue refresh tokens.
        /// </summary>
        /// <param name="refreshToken">Not used.</param>
        /// <returns>This method does not return.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            throw new NotSupportedException();
        }
    }
}
