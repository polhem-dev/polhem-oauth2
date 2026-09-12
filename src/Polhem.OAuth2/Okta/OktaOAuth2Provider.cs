using Newtonsoft.Json.Linq;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The Okta OAuth2 provider.
    /// </summary>
    public class OktaOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OktaOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Okta OAuth2 options.</param>
        public OktaOAuth2Provider(OktaOAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "Okta";

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
                UserName = jObject["name"]?.ToString() ?? jObject["preferred_username"]?.ToString(),
                Email = jObject["email"]?.ToString(),
                RawJson = json
            };
        }
    }
}
