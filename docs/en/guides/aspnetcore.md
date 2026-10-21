# ASP.NET Core

**English** | [繁體中文](../../zh-TW/guides/aspnetcore.md)

Add the Polhem.OAuth2.AspNetCore package, register a client for each provider, and redirect to the provider from a
sign-in endpoint:

```csharp
using Polhem.OAuth2;

builder.Services.AddControllers();
builder.Services.AddOAuth2Client("Google", new GoogleOAuth2Options
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "https://localhost:7032/auth/callback"
});

var app = builder.Build();
app.MapControllers();
```

```csharp
using Microsoft.AspNetCore.Mvc;
using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;

public class AuthController(OAuth2Manager oauth2Manager) : ControllerBase
{
    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        return Redirect(oauth2Manager.CreateAuthorizationUrl(HttpContext, "Google"));
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback()
    {
        AuthorizationResult result = await oauth2Manager.CompleteAuthorizationAsync(HttpContext, HttpContext.RequestAborted);
        return result.IsSuccess
            ? Content($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}")
            : Content($"The sign-in failed: {result.Exception.Message}");
    }
}
```

- `AddOAuth2Client` registers the client, `OAuth2Manager` and ASP.NET Core data protection. The client is created by the
  call, so invalid options stop the application at startup. It takes an optional `HttpClient`, which the client keeps for
  the life of the application, so one from `IHttpClientFactory` would outlive its handler; `AddOAuth2ClientWithHttpClientFactory`
  takes a function that returns the client for each request instead, which honors the factory. Pass none unless the application
  needs its own: the default replaces its pooled connections regularly to follow DNS changes, which `new HttpClient()` does not.
  The default also does not follow redirects, which would resend the body of a token request; set `AllowAutoRedirect` to false
  on the handler of a client of your own for the same reason.
- `oauth2Manager.GetClient("Google")` returns the client, for example to call `RefreshTokenAsync`.
- The public members of `OAuth2Manager` are virtual, and a protected constructor creates a manager with no clients. A test
  of a controller can pass a class derived from it that overrides what the test needs, without a service provider or a
  provider to sign in to.
- ASP.NET Core has an `AuthorizationResult` of its own, in `Microsoft.AspNetCore.Authorization`, the namespace of `[Authorize]`.
  A file that imports both namespaces and names the type gets error CS0104. Declare the variable with `var`, or add
  `using AuthorizationResult = Polhem.OAuth2.AuthorizationResult;`.
- A provider that returns with a form POST, such as [Sign in with Apple](../providers/apple.md), needs its callback mapped
  for GET and POST and left out of antiforgery validation; see
  [Web applications: how a sign-in is kept](web-sign-in-state.md).
- How the sign-in is kept between the redirect and the callback, and what several servers need, is described in
  [Web applications: how a sign-in is kept](web-sign-in-state.md). A .NET MAUI application can sign in through this back end;
  see [.NET MAUI applications](maui.md#signing-in-through-the-back-end).

The [OAuthAspNetCore](../../../samples/OAuthAspNetCore) sample is a complete application.
