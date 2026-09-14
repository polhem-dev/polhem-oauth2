using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The platform-independent part of the OAuth2 authorization code flow. Each client derives from it and supplies an
    /// <see cref="IStateStorage"/>, for example <see cref="LoopbackOAuth2Client"/> for desktop and console applications.
    /// </summary>
    public abstract class BaseOAuth2Client
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseOAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        public BaseOAuth2Client(OAuth2Options options)
        {
            UsePkce = options.UsePkce;
            Provider = CreateProvider(options);
        }

        private static IOAuth2Provider CreateProvider(OAuth2Options options)
        {
            switch (options)
            {
                case GoogleOAuth2Options googleOptions:
                    return new GoogleOAuth2Provider(googleOptions);
                case LineOAuth2Options lineOptions:
                    return new LineOAuth2Provider(lineOptions);
                case AzureOAuth2Options azureOptions:
                    return new AzureOAuth2Provider(azureOptions);
                case FacebookOAuth2Options facebookOptions:
                    return new FacebookOAuth2Provider(facebookOptions);
                case Auth0OAuth2Options auth0Options:
                    return new Auth0OAuth2Provider(auth0Options);
                case OktaOAuth2Options oktaOptions:
                    return new OktaOAuth2Provider(oktaOptions);
                default:
                    throw new NotSupportedException("Unsupported OAuth provider.");
            }
        }

        /// <summary>
        /// Gets the OAuth2 provider.
        /// </summary>
        public IOAuth2Provider Provider { get; private set; }

        /// <summary>
        /// Gets the storage that keeps the state and the PKCE code verifier between the redirect and the callback.
        /// </summary>
        public abstract IStateStorage StateStorage { get; }

        /// <summary>
        /// Gets a value indicating whether the flow uses PKCE.
        /// </summary>
        public bool UsePkce { get; private set; }

        /// <summary>
        /// Stores the state, and the PKCE code verifier when PKCE is used, then builds the authorization URL.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <returns>The URL to send the user to.</returns>
        public string GetAuthorizationUrl(string state)
        {
            StateStorage.SaveState(state);

            string codeChallenge = string.Empty;
            if (UsePkce)
            {
                string codeVerifier = Pkce.GenerateCodeVerifier();
                codeChallenge = Pkce.GenerateCodeChallenge(codeVerifier);
                StateStorage.SaveCodeVerifier(codeVerifier);
            }
            return Provider.GetAuthorizationUrl(state, codeChallenge);
        }

        /// <summary>
        /// Compares the state returned to the callback with the stored state, then removes the stored state.
        /// </summary>
        /// <param name="returnedState">The state returned by the provider.</param>
        /// <returns>
        /// <see langword="true"/> if a non-empty state was returned and it equals the stored state; otherwise,
        /// <see langword="false"/>. A missing state does not match even when no state is stored.
        /// </returns>
        public bool ValidateState(string? returnedState)
        {
            string? storedState = StateStorage.GetState();
            StateStorage.RemoveState();
            return !string.IsNullOrEmpty(returnedState) && returnedState == storedState;
        }

        /// <summary>
        /// Exchanges the authorization code for an access token, sending the stored PKCE code verifier when PKCE is used.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <returns>The access token.</returns>
        public async Task<string> GetAccessTokenAsync(string authorizationCode)
        {
            string codeVerifier = string.Empty;
            if (UsePkce)
            {
                codeVerifier = StateStorage.GetCodeVerifier() ?? string.Empty;
                StateStorage.RemoveCodeVerifier();
            }
            return await Provider.GetAccessTokenAsync(authorizationCode, codeVerifier);
        }

        /// <summary>
        /// Retrieves user information with an access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The user information as a JSON string.</returns>
        public Task<string> GetUserInfoAsync(string accessToken)
        {
            return Provider.GetUserInfoAsync(accessToken);
        }

        /// <summary>
        /// Exchanges the authorization code for an access token and retrieves the user information.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <returns>A successful result with the access token and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// Only the failures an OAuth2 exchange is expected to produce become a failed result: an <see cref="OAuth2Exception"/>,
        /// an HTTP failure, a request timeout, or a response that is not valid JSON. Any other exception propagates to the caller.
        /// </remarks>
        public async Task<AuthorizationResult> ValidateAuthorization(string? authorizationCode)
        {
            try
            {
                if (authorizationCode is not { } code || string.IsNullOrWhiteSpace(code))
                    throw new OAuth2Exception("The authorization code is empty.");

                string accessToken = await GetAccessTokenAsync(code);
                if (string.IsNullOrEmpty(accessToken))
                    throw new OAuth2Exception("The token response does not contain an access token.");

                string userInfo = await GetUserInfoAsync(accessToken);
                if (string.IsNullOrWhiteSpace(userInfo))
                    throw new OAuth2Exception("The user information response is empty.");

                return new AuthorizationResult()
                {
                    ProviderName = Provider.ProviderName,
                    IsSuccess = true,
                    AccessToken = accessToken,
                    UserInfo = Provider.ParseUserJson(userInfo)
                };
            }
            catch (OAuth2Exception ex)
            {
                return Failure(ex);
            }
            catch (HttpRequestException ex)
            {
                return Failure(ex);
            }
            catch (TaskCanceledException ex)
            {
                return Failure(ex);
            }
            catch (JsonException ex)
            {
                return Failure(ex);
            }
        }

        private static AuthorizationResult Failure(Exception exception)
        {
            return new AuthorizationResult()
            {
                IsSuccess = false,
                Exception = exception
            };
        }
    }
}
