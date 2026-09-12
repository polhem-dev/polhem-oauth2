using Newtonsoft.Json.Linq;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The LINE Login OAuth2 provider.
    /// </summary>
    public class LineOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LineOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The LINE OAuth2 options.</param>
        public LineOAuth2Provider(LineOAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "LINE";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null or empty.</exception>
        public override UserInfo ParseUserJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty.");

            var jObject = JObject.Parse(json);

            return new UserInfo
            {
                UserId = jObject["userId"]?.ToString(),
                UserName = jObject["displayName"]?.ToString(),
                // The LINE profile response may not include an email address.
                Email = jObject["email"]?.ToString(),
                RawJson = json
            };
        }
    }
}
