using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using OAuthSamples;

namespace OAuthMaui;

/// <summary>
/// Signs in through the back-end relay of the ASP.NET Core sample (ADR-006): opens the back end's sign-in URL with a code
/// challenge, receives a single-use code on the relay redirect URI, and redeems it with the code verifier.
/// </summary>
/// <remarks>
/// The back end returns only the user information, never a provider token. A real application would receive its own
/// session from its back end instead.
/// </remarks>
internal static class BackEndSignIn
{
    private static readonly HttpClient s_http = new();

    public static async Task<BackEndUser?> SignInAsync(OAuthAppRelay relay, string providerName)
    {
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var start = new Uri(relay.BackendUrl, $"auth/app/{Uri.EscapeDataString(providerName)}"
            + $"?redirect_uri={Uri.EscapeDataString(relay.RedirectUri)}&code_challenge={challenge}");
        var result = await WebAuthenticator.Default.AuthenticateAsync(start, new Uri(relay.RedirectUri));

        if (result.Properties.TryGetValue("error", out string? error))
            throw new InvalidOperationException($"The back end reported '{error}'.");
        if (!result.Properties.TryGetValue("code", out string? code))
            throw new InvalidOperationException("The back end returned no code.");

        using var response = await s_http.PostAsync(new Uri(relay.BackendUrl, "auth/app/redeem"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client"] = providerName,
            ["code"] = code,
            ["code_verifier"] = verifier
        }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BackEndUser>();
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
