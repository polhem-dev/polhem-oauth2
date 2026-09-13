namespace Polhem.OAuth2
{
    /// <summary>
    /// The Microsoft Entra ID OAuth2 provider.
    /// </summary>
    public class AzureOAuth2Provider : OAuth2Provider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The Microsoft Entra ID OAuth2 options.</param>
        public AzureOAuth2Provider(AzureOAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName { get; } = "Azure";

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetAccessTokenParams(string authorizationCode, string codeVerifier = "")
        {
            var requestParams = base.GetAccessTokenParams(authorizationCode, codeVerifier);
            // NOTE: `response_mode` is defined for the authorization request, not for the token request.
            requestParams["response_mode"] = "query";
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
                    UserId = OAuth2Json.GetString(root, "oid") ?? OAuth2Json.GetString(root, "sub"),
                    UserName = OAuth2Json.GetString(root, "name"),
                    Email = OAuth2Json.GetString(root, "email") ?? OAuth2Json.GetString(root, "userPrincipalName"),
                    RawJson = json
                };
            }
        }
    }
}
