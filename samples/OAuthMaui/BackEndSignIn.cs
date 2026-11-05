using System.Security.Cryptography;
using System.Text;
using OAuthSamples;
using Polhem.OAuth2;

namespace OAuthMaui;

/// <summary>
/// Signs in through the back-end relay of the ASP.NET Core sample (ADR-006). On Android, iOS and Mac Catalyst it opens the
/// back end's sign-in URL with <see cref="WebAuthenticator"/> and a custom scheme; on Windows it uses
/// <see cref="LoopbackRelayClient"/> and a loopback address. Either way it redeems the single-use code with its code verifier.
/// </summary>
/// <remarks>
/// The back end returns only the user information, never a provider token. A real application would receive its own
/// session from its back end instead.
/// </remarks>
internal static class BackEndSignIn
{
    private static readonly HttpClient s_http = new();

    public static async Task<OAuthBackEndUser?> SignInAsync(OAuthAppRelay relay, string providerName)
    {
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var start = OAuthBackEnd.CreateSignInUrl(relay, providerName, relay.RedirectUri, challenge);
        var result = await WebAuthenticator.Default.AuthenticateAsync(start, new Uri(relay.RedirectUri));

        if (result.Properties.TryGetValue("error", out string? error))
            throw new InvalidOperationException($"The back end reported '{error}'.");
        if (!result.Properties.TryGetValue("code", out string? code))
            throw new InvalidOperationException("The back end returned no code.");

        return await OAuthBackEnd.RedeemAsync(s_http, relay, providerName, code, verifier);
    }

    public static async Task<OAuthBackEndUser?> SignInOnDesktopAsync(OAuthAppRelay relay, string desktopRedirectUri, string providerName)
    {
        // The back end accepts the registered loopback URI on any port, so the client listens on a free one.
        var client = new LoopbackRelayClient(new UriBuilder(desktopRedirectUri) { Port = 0 }.Uri.AbsoluteUri)
        {
            OpenBrowser = url => Launcher.Default.OpenAsync(url)
        };

        var result = await client.SignInAsync((redirectUri, codeChallenge) =>
            OAuthBackEnd.CreateSignInUrl(relay, providerName, redirectUri.AbsoluteUri, codeChallenge));
        if (!result.IsSuccess)
            throw new InvalidOperationException($"The sign-in failed: {result.Exception.Message}", result.Exception);

        return await OAuthBackEnd.RedeemAsync(s_http, relay, providerName, result.Code, result.CodeVerifier);
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
