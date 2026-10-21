# ASP.NET (System.Web)

**English** | [繁體中文](../../zh-TW/guides/aspnet-system-web.md)

There is no runnable sample for System.Web, because its projects cannot be built with the `dotnet` CLI. The
Polhem.OAuth2.AspNet package is used the same way as the ASP.NET Core one, through the static `OAuth2Manager`:

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNet;

// Global.asax.cs
protected void Application_Start()
{
    OAuth2Manager.RegisterClient("Google", new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:44300/auth/callback"
    });
}
```

```csharp
// An MVC controller. In Web Forms, call OAuth2Manager.RedirectToAuthorization("Google") from the sign-in page and return,
// and await OAuth2Manager.CompleteAuthorizationAsync() on the callback page, which needs Async="true".
public class AuthController : Controller
{
    public ActionResult Login()
    {
        return Redirect(OAuth2Manager.CreateAuthorizationUrl(HttpContext, "Google"));
    }

    public async Task<ActionResult> Callback()
    {
        AuthorizationResult result = await OAuth2Manager.CompleteAuthorizationAsync(HttpContext);
        if (result.IsSuccess)
            return Content(result.UserInfo.UserId + " " + result.UserInfo.UserName + " " + result.UserInfo.Email);

        return Content("The sign-in failed: " + result.Exception.Message);
    }
}
```

How the sign-in is kept between the redirect and the callback is described in
[Web applications: how a sign-in is kept](web-sign-in-state.md).

## Before deploying

- Target .NET Framework 4.7.2 or later, and set `<httpRuntime targetFramework="4.7.2" />`, or your later version, in
  `web.config`. Asynchronous pages and the operating system's TLS defaults depend on it.
- On .NET Framework the core package depends on System.Text.Json. Keep the binding redirects that NuGet adds for it and its
  dependencies in `web.config`.
- When more than one server can receive the callback, set the same `<machineKey>` in `web.config` on each of them.
- On .NET Framework the handler of the default `HttpClient` has no connection lifetime, so the client sets
  `ConnectionLeaseTimeout` on the `ServicePoint` of the token and user information hosts, and a connection is replaced
  regularly to follow DNS changes. The setting applies to the whole process for those hosts. An application that passes its own
  `HttpClient` sets it itself, with `AllowAutoRedirect` set to false on the handler as the default has.
- On a machine with FIPS mode enabled, an application that targets .NET Framework 4.7.2 can get a `CryptographicException`
  when a sign-in starts: for such applications .NET Framework blocks the managed SHA-256 implementation that PKCE uses.
  Target .NET Framework 4.8 or later, or set the `Switch.System.Security.Cryptography.UseLegacyFipsThrow` switch to `false`.
  See [Managed cryptography classes do not throw a CryptographyException in FIPS mode](https://learn.microsoft.com/dotnet/framework/migration-guide/retargeting/4.8.x#managed-cryptography-classes-do-not-throw-a-cryptographyexception-in-fips-mode).
