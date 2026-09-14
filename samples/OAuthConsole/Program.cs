using System.Net.Sockets;
using System.Text.Json;
using OAuthConsole;
using Polhem.OAuth2;

// Usage: dotnet run -- <Google|Facebook|Line|Azure|Auth0|Okta>
string providerName = args.Length > 0 ? args[0] : "Google";

string configPath = Path.Combine(AppContext.BaseDirectory, "OAuthConfig.json");
var config = JsonSerializer.Deserialize<OAuthConfig>(
    File.ReadAllText(configPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OAuthConfig();

OAuth2Options? options = config.Find(providerName);
if (options is null)
{
    Console.Error.WriteLine($"OAuthConfig.json has no settings for '{providerName}'. Use Google, Facebook, Line, Azure, Auth0 or Okta.");
    return 2;
}

LoopbackOAuth2Client client;
try
{
    client = new LoopbackOAuth2Client(options);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Keep the process running so that SignInAsync can return a canceled result.
    e.Cancel = true;
    cancellation.Cancel();
};

Console.WriteLine($"Sign in with {providerName} in the browser that opens. Press Ctrl+C to cancel.");

AuthorizationResult result;
try
{
    result = await client.SignInAsync(cancellation.Token);
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Cannot listen on {options.RedirectUri}: {ex.Message}");
    return 2;
}

if (!result.IsSuccess || result.UserInfo is not { } user)
{
    Console.Error.WriteLine($"The sign-in failed: {result.Exception?.Message}");
    return 1;
}

Console.WriteLine($"Provider:  {result.ProviderName}");
Console.WriteLine($"User ID:   {user.UserId}");
Console.WriteLine($"User name: {user.UserName}");
Console.WriteLine($"Email:     {user.Email}");
return 0;
