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
                    UserId = OAuth2Json.GetString(root, "userId"),
                    UserName = OAuth2Json.GetString(root, "displayName"),
                    // The LINE profile response may not include an email address.
                    Email = OAuth2Json.GetString(root, "email"),
                    RawJson = json
                };
            }
        }
    }
}
