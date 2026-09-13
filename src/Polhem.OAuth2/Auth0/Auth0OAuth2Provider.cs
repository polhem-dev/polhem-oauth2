namespace Polhem.OAuth2
{
    /// <summary>
    /// The Auth0 OAuth2 provider.
    /// </summary>
    public class Auth0OAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Auth0OAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Auth0 OAuth2 options.</param>
        public Auth0OAuth2Provider(Auth0OAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "Auth0";

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
                    UserName = OAuth2Json.GetString(root, "name") ?? OAuth2Json.GetString(root, "nickname"),
                    Email = OAuth2Json.GetString(root, "email"),
                    RawJson = json
                };
            }
        }
    }
}
