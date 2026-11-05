using System.Net.Sockets;
using System.Text.Json;
using OAuthSamples;
using Polhem.OAuth2;

// Usage: dotnet run -- <Google|Facebook|Line|Azure|Auth0|Okta|GitHub> [--back-end]
// With --back-end the sample signs in through the relay of the ASP.NET Core sample, which must be running, instead of
// signing in to the provider directly (ADR-006).
bool throughBackEnd = args.Contains("--back-end", StringComparer.Ordinal);
string providerName = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? "Google";

OAuthConfig config;
try
{
    config = OAuthConfig.Load();
}
catch (FileNotFoundException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 2;
}
catch (InvalidDataException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 2;
}
catch (JsonException ex)
{
    await Console.Error.WriteLineAsync($"{OAuthConfig.FileName} is not valid JSON: {ex.Message}");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Keep the process running so that SignInAsync can return a canceled result.
    e.Cancel = true;
    cancellation.Cancel();
};

return throughBackEnd
    ? await SignInThroughBackEndAsync(config, providerName, cancellation.Token)
    : await SignInDirectlyAsync(config, providerName, cancellation.Token);

static async Task<int> SignInDirectlyAsync(OAuthConfig config, string providerName, CancellationToken cancellationToken)
{
    OAuth2Options options;
    LoopbackOAuth2Client client;
    try
    {
        options = config.GetClient(providerName, OAuthClientType.Desktop);
        client = new LoopbackOAuth2Client(options);
    }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
    {
        await Console.Error.WriteLineAsync(ex.Message);
        return 2;
    }

    Console.WriteLine($"Sign in with {providerName} in the browser that opens. Press Ctrl+C to cancel.");

    AuthorizationResult result;
    try
    {
        result = await client.SignInAsync(cancellationToken);
    }
    catch (SocketException ex)
    {
        await Console.Error.WriteLineAsync($"Cannot listen on {options.RedirectUri}: {ex.Message}");
        return 2;
    }

    if (!result.IsSuccess || result.UserInfo is not { } user)
    {
        await Console.Error.WriteLineAsync($"The sign-in failed: {result.Exception?.Message}");
        return 1;
    }

    Console.WriteLine($"Provider:  {result.ProviderName}");
    Console.WriteLine($"User ID:   {user.UserId}");
    Console.WriteLine($"User name: {user.UserName}");
    Console.WriteLine($"Email:     {user.Email}");
    return 0;
}

static async Task<int> SignInThroughBackEndAsync(OAuthConfig config, string providerName, CancellationToken cancellationToken)
{
    if (config.AppRelay is not { DesktopRedirectUri: { } desktopRedirectUri } relay)
    {
        await Console.Error.WriteLineAsync($"{OAuthConfig.FileName} needs AppRelay.DesktopRedirectUri, such as http://127.0.0.1/callback, to sign in through the back end.");
        return 2;
    }

    LoopbackRelayClient client;
    try
    {
        // The back end accepts the registered loopback URI on any port, so the client listens on a free one.
        client = new LoopbackRelayClient(new UriBuilder(desktopRedirectUri) { Port = 0 }.Uri.AbsoluteUri);
    }
    catch (Exception ex) when (ex is ArgumentException or UriFormatException)
    {
        await Console.Error.WriteLineAsync($"AppRelay.DesktopRedirectUri is not valid: {ex.Message}");
        return 2;
    }

    Console.WriteLine($"Sign in with {providerName} through {relay.BackendUrl} in the browser that opens. Press Ctrl+C to cancel.");

    var result = await client.SignInAsync(
        (redirectUri, codeChallenge) => OAuthBackEnd.CreateSignInUrl(relay, providerName, redirectUri.AbsoluteUri, codeChallenge),
        cancellationToken);
    if (!result.IsSuccess)
    {
        await Console.Error.WriteLineAsync($"The sign-in failed: {result.Exception.Message}");
        return 1;
    }

    OAuthBackEndUser? user;
    try
    {
        using var http = new HttpClient();
        user = await OAuthBackEnd.RedeemAsync(http, relay, providerName, result.Code, result.CodeVerifier, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        await Console.Error.WriteLineAsync($"The back end did not redeem the code: {ex.Message}");
        return 1;
    }

    if (user is null)
    {
        await Console.Error.WriteLineAsync("The back end did not return a user.");
        return 1;
    }

    Console.WriteLine($"Provider:  {providerName}, through the back end");
    Console.WriteLine($"User ID:   {user.UserId}");
    Console.WriteLine($"User name: {user.UserName}");
    Console.WriteLine($"Email:     {user.Email}");
    return 0;
}
