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
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">An endpoint of <paramref name="options"/> is not an absolute https URI.</exception>
        /// <exception cref="NotSupportedException">No provider matches the type of <paramref name="options"/>.</exception>
        public BaseOAuth2Client(OAuth2Options options) : this(options, null)
        {
        }

        internal BaseOAuth2Client(OAuth2Options options, HttpClient? httpClient)
        {
            Provider = OAuth2Provider.Create(options, httpClient);
            UsePkce = options.UsePkce;
        }

        /// <summary>
        /// Gets the storage that keeps the state and the PKCE code verifier between the redirect and the callback.
        /// </summary>
        public abstract IStateStorage StateStorage { get; }

        /// <summary>
        /// Gets a value indicating whether the flow uses PKCE.
        /// </summary>
        public bool UsePkce { get; protected set; }

        /// <summary>
        /// Gets the OAuth2 provider.
        /// </summary>
        internal OAuth2Provider Provider { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the client is a public client, which cannot keep its client secret
        /// confidential and therefore does not send it.
        /// </summary>
        internal bool IsPublicClient { get; set; }

        /// <summary>
        /// Stores the state, and the PKCE code verifier when PKCE is used, then builds the authorization URL.
        /// </summary>
        /// <param name="state">A random value that protects against cross-site request forgery.</param>
        /// <returns>The URL to send the user to.</returns>
        public string GetAuthorizationUrl(string state)
        {
            StateStorage.SaveState(state);

            string? codeChallenge = null;
            if (UsePkce)
            {
                string codeVerifier = Pkce.GenerateCodeVerifier();
                codeChallenge = Pkce.GenerateCodeChallenge(codeVerifier);
                StateStorage.SaveCodeVerifier(codeVerifier);
            }
            return Provider.GetAuthorizationUrl(state, Provider.Options.RedirectUri, codeChallenge);
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
        /// Exchanges the authorization code for tokens and retrieves the user information.
        /// </summary>
        /// <param name="authorizationCode">The authorization code returned by the provider.</param>
        /// <returns>A successful result with the tokens and user information, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// Only the failures an OAuth2 exchange is expected to produce become a failed result: an <see cref="OAuth2Exception"/>,
        /// which includes an error returned by the token endpoint, an HTTP failure, a request timeout, or a response that is not
        /// valid JSON. Any other exception propagates to the caller.
        /// </remarks>
        public async Task<AuthorizationResult> ValidateAuthorization(string? authorizationCode)
        {
            try
            {
                if (authorizationCode is not { } code || string.IsNullOrWhiteSpace(code))
                    throw new OAuth2Exception("The authorization code is empty.");

                TokenResponse token = await ExchangeCodeAsync(code).ConfigureAwait(false);
                UserInfo userInfo = await Provider.GetUserInfoAsync(token, CancellationToken.None).ConfigureAwait(false);

                return new AuthorizationResult()
                {
                    ProviderName = Provider.ProviderName,
                    IsSuccess = true,
                    Token = token,
                    UserInfo = userInfo
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

        // WARNING: Every await in this class must use ConfigureAwait(false). Applications on System.Web or Windows Forms that
        // block on the returned task would otherwise deadlock. The state storage is read before the first await on purpose,
        // because it can depend on the HTTP context of the current request.
        private Task<TokenResponse> ExchangeCodeAsync(string authorizationCode)
        {
            string? codeVerifier = null;
            if (UsePkce)
            {
                codeVerifier = StateStorage.GetCodeVerifier();
                StateStorage.RemoveCodeVerifier();
            }
            return Provider.ExchangeCodeAsync(authorizationCode, Provider.Options.RedirectUri, codeVerifier, IsPublicClient, CancellationToken.None);
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
