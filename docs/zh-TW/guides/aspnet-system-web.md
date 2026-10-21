# ASP.NET（System.Web）

[English](../../en/guides/aspnet-system-web.md) | **繁體中文**

System.Web 的專案無法用 `dotnet` CLI 建置，所以沒有可執行的 sample。Polhem.OAuth2.AspNet 套件的用法與 ASP.NET Core 版相同，
透過靜態的 `OAuth2Manager`：

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

從導向到回呼之間登入狀態怎麼保存，見[網頁應用程式：登入狀態怎麼保存](web-sign-in-state.md)。

## 部署前確認

- 目標框架為 .NET Framework 4.7.2 或更新的版本，並在 `web.config` 設定 `<httpRuntime targetFramework="4.7.2" />`（或你使用的更新版本）。
  非同步頁面與作業系統的 TLS 預設值都取決於這個設定。
- 在 .NET Framework 上，核心套件相依於 System.Text.Json。NuGet 為它及其相依套件加入 `web.config` 的 binding redirect 要保留。
- 有多台伺服器可能收到回呼時，每一台的 `web.config` 都要設定相同的 `<machineKey>`。
- 在 .NET Framework 上，預設 `HttpClient` 的 handler 沒有連線壽命設定，所以 client 會對 token 與使用者資訊端點主機的 `ServicePoint`
  設定 `ConnectionLeaseTimeout`，連線會定期汰換以跟上 DNS 變更。這個設定對整個程序裡連到這些主機的連線都有效。傳入自己的
  `HttpClient` 的應用程式要自行設定，並像預設的一樣把 handler 的 `AllowAutoRedirect` 設為 false。
- 在開啟 FIPS 模式的機器上，目標框架為 .NET Framework 4.7.2 的應用程式，開始登入時可能擲出 `CryptographicException`：
  對這類應用程式，.NET Framework 會擋下 PKCE 使用的 managed SHA-256 實作。請把目標框架改為 .NET Framework 4.8 或更新的版本，
  或把 `Switch.System.Security.Cryptography.UseLegacyFipsThrow` 開關設為 `false`。
  見 [Managed cryptography classes do not throw a CryptographyException in FIPS mode](https://learn.microsoft.com/dotnet/framework/migration-guide/retargeting/4.8.x#managed-cryptography-classes-do-not-throw-a-cryptographyexception-in-fips-mode)。
