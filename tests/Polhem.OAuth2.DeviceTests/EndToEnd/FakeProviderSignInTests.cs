using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Polhem.OAuth2.FakeProvider;
using Polhem.OAuth2.UnitTests;

namespace Polhem.OAuth2.DeviceTests.EndToEnd;

/// <summary>
/// Signs in to tests/Polhem.OAuth2.FakeProvider through the browser of the platform, in each mode that the platform table
/// of ADR-006 lists. A user canceling the browser cannot be automated here and is checked by hand.
/// </summary>
public class FakeProviderSignInTests
{
    [AppSignInFact]
    [DisplayName("A direct sign-in through WebAuthenticator returns the user of the fake provider")]
    public async Task AppSignIn_FakeProvider_ReturnsUser()
    {
        var client = new AppOAuth2Client(FakeProvider.CreateOptions(FakeProviderValues.ClientId, FakeProviderValues.AppRedirectUri), AuthenticateAsync);

        var result = await client.SignInAsync().WithTimeout();

        Assert.True(result.IsSuccess, result.Exception?.ToString());
        Assert.True(FakeProvider.IsKnownUser(result.UserInfo));
    }

    [AppSignInFact]
    [DisplayName("A direct sign-in that the provider denies returns a failed result with the error code")]
    public async Task AppSignIn_ProviderDenies_ReturnsFailedResult()
    {
        var client = new AppOAuth2Client(FakeProvider.CreateOptions(FakeProviderValues.DeniedClientId, FakeProviderValues.AppRedirectUri), AuthenticateAsync);

        var result = await client.SignInAsync().WithTimeout();

        Assert.False(result.IsSuccess);
        Assert.Equal("access_denied", Assert.IsType<OAuth2Exception>(result.Exception).Error);
    }

    [AppSignInFact]
    [DisplayName("A relayed sign-in to the GitHub mode of the fake provider returns the numeric ID and the primary email address")]
    public async Task RelaySignIn_FakeGitHub_RedeemsUserWithPrimaryEmail()
    {
        using var user = await RelaySignInAsync(FakeProviderValues.GitHubRelayClientName);

        Assert.Equal(FakeProviderValues.GitHubUserId.ToString(CultureInfo.InvariantCulture), user.RootElement.GetProperty("userId").GetString());
        Assert.Equal(FakeProviderValues.UserName, user.RootElement.GetProperty("userName").GetString());
        Assert.Equal(FakeProviderValues.Email, user.RootElement.GetProperty("email").GetString());
    }

    [AppSignInFact]
    [DisplayName("A direct sign-in to an OpenID Connect provider read from its discovery document returns the user of the ID token")]
    public async Task AppSignIn_FakeOidcDiscovery_ReturnsUserOfIdToken()
    {
        var options = await FakeProvider.CreateOidcOptionsAsync(FakeProviderValues.ClientId, FakeProviderValues.AppRedirectUri).WithTimeout();
        var client = new AppOAuth2Client(options, AuthenticateAsync);

        var result = await client.SignInAsync().WithTimeout();

        Assert.True(result.IsSuccess, result.Exception?.ToString());
        Assert.Equal("Fake OpenID Connect", result.ProviderName);
        Assert.Empty(options.UserInfoEndpoint);
        Assert.True(FakeProvider.IsKnownUser(result.UserInfo));
    }

    [AppSignInFact]
    [DisplayName("A sign-in through the back-end relay returns the user after the application redeems its code")]
    public async Task RelaySignIn_FakeProvider_RedeemsUser()
    {
        using var user = await RelaySignInAsync(FakeProviderValues.RelayClientName);

        Assert.Equal(FakeProviderValues.UserId, user.RootElement.GetProperty("userId").GetString());
        Assert.Equal(FakeProviderValues.Email, user.RootElement.GetProperty("email").GetString());
    }

    [AppSignInFact]
    [DisplayName("A relayed sign-in to the Sign in with Apple mode, which returns to the back end with a form POST, returns the user with the name of the user parameter")]
    public async Task RelaySignIn_FakeAppleFormPost_RedeemsUserWithName()
    {
        using var user = await RelaySignInAsync(FakeProviderValues.AppleRelayClientName);

        Assert.Equal(FakeProviderValues.UserId, user.RootElement.GetProperty("userId").GetString());
        Assert.Equal(FakeProviderValues.UserName, user.RootElement.GetProperty("userName").GetString());
        Assert.Equal(FakeProviderValues.Email, user.RootElement.GetProperty("email").GetString());
    }

    private static async Task<JsonDocument> RelaySignInAsync(string clientName)
    {
        string verifier = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var start = FakeProvider.GetUri($"auth/app/{clientName}"
            + $"?redirect_uri={Uri.EscapeDataString(FakeProviderValues.RelayRedirectUri)}&code_challenge={challenge}");

        var callback = await AuthenticateAsync(start, new Uri(FakeProviderValues.RelayRedirectUri), CancellationToken.None).WithTimeout();
        string? code = CallbackQuery.FromUri(callback).Code;
        Assert.False(string.IsNullOrEmpty(code), callback.ToString());

        using var http = new HttpClient();
        using var response = await http.PostAsync(FakeProvider.GetUri("auth/app/redeem"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client"] = clientName,
            ["code"] = code!,
            ["code_verifier"] = verifier
        })).WithTimeout();
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    [LoopbackSignInFact]
    [DisplayName("A loopback sign-in through the default browser returns the user of the fake provider")]
    public async Task LoopbackSignIn_FakeProvider_ReturnsUser()
    {
        var client = new LoopbackOAuth2Client(FakeProvider.CreateOptions(FakeProviderValues.ClientId, "http://127.0.0.1:0/callback"));

        var result = await client.SignInAsync().WithTimeout();

        Assert.True(result.IsSuccess, result.Exception?.ToString());
        Assert.True(FakeProvider.IsKnownUser(result.UserInfo));
    }

    [LoopbackSignInFact]
    [DisplayName("A loopback sign-in to an OpenID Connect provider read from its discovery document returns the user of the ID token")]
    public async Task LoopbackSignIn_FakeOidcDiscovery_ReturnsUserOfIdToken()
    {
        var options = await FakeProvider.CreateOidcOptionsAsync(FakeProviderValues.ClientId, "http://127.0.0.1:0/callback").WithTimeout();

        var result = await new LoopbackOAuth2Client(options).SignInAsync().WithTimeout();

        Assert.True(result.IsSuccess, result.Exception?.ToString());
        Assert.True(FakeProvider.IsKnownUser(result.UserInfo));
    }

    // An ephemeral session shares no cookies with the browser, so iOS and Mac Catalyst skip the prompt that asks the user
    // whether the application may sign in, which a test cannot answer.
    private static Task<Uri> AuthenticateAsync(Uri url, Uri callbackUrl, CancellationToken cancellationToken)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = url,
                CallbackUrl = callbackUrl,
                PrefersEphemeralWebBrowserSession = true
            });
            return result.CallbackUri;
        });
    }
}
