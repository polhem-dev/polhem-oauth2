using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;
using Polhem.OAuth2.FakeProvider;

// Usage: dotnet run -- --port 7443 --ca-certificate <path>
// The CA certificate is written once the server listens, so a script can wait for the file before it starts the tests.
const string WebClientId = "fake-web";
const string WebClientSecret = "fake-web-secret";

var builder = WebApplication.CreateBuilder(args);
int port = builder.Configuration.GetValue("port", 7443);
string caCertificatePath = builder.Configuration["ca-certificate"]
    ?? throw new InvalidOperationException("Pass --ca-certificate with the path where the CA certificate is written.");
string origin = $"https://localhost:{port}";

using var certificates = TestCertificateAuthority.Create();
using var httpClient = certificates.CreateHttpClient();

builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port, listen => listen.UseHttps(certificates.ServerCertificate)));

// The relay signs in to this process as its web client, the way a back end signs in to Auth0.
builder.Services.AddOAuth2Client(FakeProviderValues.RelayClientName, new Auth0OAuth2Options
{
    Domain = $"localhost:{port}",
    ClientId = WebClientId,
    ClientSecret = WebClientSecret,
    RedirectUri = origin + "/auth/callback"
}, httpClient);
builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(FakeProviderValues.RelayRedirectUri));

var app = builder.Build();

new FakeAuthorizationServer(WebClientId, WebClientSecret).Map(app);

app.MapGet("/auth/app/{clientName}", (HttpContext context, OAuth2Manager manager, string clientName) =>
{
    try
    {
        manager.RedirectToAppAuthorization(context, clientName,
            context.Request.Query["redirect_uri"].ToString(), context.Request.Query["code_challenge"].ToString());
        return Results.Empty;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/auth/callback", async (HttpContext context, OAuth2Manager manager) =>
{
    var result = await manager.CompleteAuthorizationAsync(context, context.RequestAborted);
    return await manager.RedirectToAppAsync(context, result, context.RequestAborted)
        ? Results.Empty
        : Results.BadRequest("The sign-in was not started by an application.");
});

app.MapPost("/auth/app/redeem", async (HttpContext context, OAuth2Manager manager) =>
{
    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var user = await manager.RedeemAppCodeAsync(form["client"].ToString(), form["code"].ToString(), form["code_verifier"].ToString(), context.RequestAborted);
    return user is null
        ? Results.BadRequest("The code is not valid.")
        : Results.Json(new { userId = user.UserId, userName = user.UserName, email = user.Email });
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    certificates.WriteAuthorityPem(caCertificatePath);
    Console.WriteLine($"The fake provider listens on {origin}. The CA certificate is at {Path.GetFullPath(caCertificatePath)}.");
});

app.Run();
