using Newtonsoft.Json.Linq;

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
        public override UserInfo ParseUserJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json), "JSON string cannot be null or empty.");

            var jObject = JObject.Parse(json);

            return new UserInfo
            {
                UserId = jObject["sub"]?.ToString(),
                UserName = jObject["name"]?.ToString() ?? jObject["nickname"]?.ToString(),
                Email = jObject["email"]?.ToString(),
                RawJson = json
            };
        }
    }
}
