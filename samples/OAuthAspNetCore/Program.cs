using OAuthSamples;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register every provider the shared settings file configures for a web client, under the name AuthController uses.
var oauthConfig = OAuthConfig.Load();
foreach (var client in oauthConfig.GetClients(OAuthClientType.Web))
    builder.Services.AddOAuth2Client(client.ProviderName, client.Options);

// The OAuthMaui sample signs in through these web clients as well, when the settings file has an AppRelay section (ADR-006).
if (oauthConfig.AppRelay is { } relay)
    builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(relay.RedirectUri));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
