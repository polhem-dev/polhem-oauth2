using System.Text.Json;
using OAuthAspNetCore.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register every provider configured in OAuthConfig.json under the name that AuthController uses.
var config = JsonSerializer.Deserialize<OAuthConfig>(
    File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, "OAuthConfig.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OAuthConfig();

if (config.GoogleOAuth is { } google)
    builder.Services.AddOAuth2Client("Google", google);
if (config.FacebookOAuth is { } facebook)
    builder.Services.AddOAuth2Client("Facebook", facebook);
if (config.LineOAuth is { } line)
    builder.Services.AddOAuth2Client("Line", line);
if (config.AzureOAuth is { } azure)
    builder.Services.AddOAuth2Client("Azure", azure);
if (config.Auth0OAuth is { } auth0)
    builder.Services.AddOAuth2Client("Auth0", auth0);
if (config.OktaOAuth is { } okta)
    builder.Services.AddOAuth2Client("Okta", okta);

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

app.Run();
