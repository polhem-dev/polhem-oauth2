using System.Net.Http.Json;

namespace OAuthSamples
{
    /// <summary>
    /// The requests an application sends to the ASP.NET Core sample to sign in through its back-end relay (ADR-006): the
    /// sign-in URL that the browser opens, and the redemption of the code that the relay returns.
    /// </summary>
    /// <remarks>
    /// The paths are those of <c>AuthController</c> in the ASP.NET Core sample. They are a contract between that sample and
    /// these applications, not something the library defines.
    /// </remarks>
    public static class OAuthBackEnd
    {
        /// <summary>
        /// Returns the URL that starts a relayed sign-in with a provider of the back end.
        /// </summary>
        /// <param name="relay">The relay settings.</param>
        /// <param name="clientName">The provider name the back end registered the client under.</param>
        /// <param name="appRedirectUri">The redirect URI of the application, which the relay returns to.</param>
        /// <param name="codeChallenge">The S256 code challenge of the application's code verifier.</param>
        /// <returns>The sign-in URL.</returns>
        public static Uri CreateSignInUrl(OAuthAppRelay relay, string clientName, string appRedirectUri, string codeChallenge)
        {
            return new Uri(relay.BackendUrl, $"auth/app/{Uri.EscapeDataString(clientName)}"
                + $"?redirect_uri={Uri.EscapeDataString(appRedirectUri)}&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        }

        /// <summary>
        /// Redeems the code that the relay returned for the user. A real back end would issue its own session here; the
        /// sample returns the user so the application can show it.
        /// </summary>
        /// <param name="http">The HTTP client.</param>
        /// <param name="relay">The relay settings.</param>
        /// <param name="clientName">The provider name the sign-in started with.</param>
        /// <param name="code">The code that the relay returned.</param>
        /// <param name="codeVerifier">The code verifier whose challenge started the sign-in.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The user, or null when the back end returned none.</returns>
        /// <exception cref="HttpRequestException">The request failed, or the back end refused the code.</exception>
        public static async Task<OAuthBackEndUser?> RedeemAsync(
            HttpClient http, OAuthAppRelay relay, string clientName, string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client"] = clientName,
                ["code"] = code,
                ["code_verifier"] = codeVerifier
            });
            using var response = await http.PostAsync(new Uri(relay.BackendUrl, "auth/app/redeem"), content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OAuthBackEndUser>(cancellationToken).ConfigureAwait(false);
        }
    }
}
