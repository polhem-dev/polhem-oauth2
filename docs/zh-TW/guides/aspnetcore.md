# ASP.NET Core

[English](../../en/guides/aspnetcore.md) | **繁體中文**

加入 Polhem.OAuth2.AspNetCore 套件，為每個 provider 註冊一個 client，再從登入端點導向 provider：

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

- `AddOAuth2Client` 會註冊 client、`OAuth2Manager` 與 ASP.NET Core 的 data protection。client 在呼叫當下就建立，
  所以 options 不合法時應用程式在啟動時就會停下來。另外可以傳入選填的 `HttpClient`；client 會在應用程式的整個生命週期持有它，
  所以來自 `IHttpClientFactory` 的 `HttpClient` 會活得比它的 handler 久；改用 `AddOAuth2ClientWithHttpClientFactory` 傳入
  每次請求取得 client 的函式，factory 才會生效。除非應用程式需要自己的，否則不要傳：
  預設的 `HttpClient` 會定期汰換連線池裡的連線以跟上 DNS 變更，`new HttpClient()` 不會。
  預設的 `HttpClient` 也不跟隨轉址，以免把 token 請求的本文轉送出去；自己的 `HttpClient` 基於同樣理由，請把 handler 的 `AllowAutoRedirect` 設為 false。
- `oauth2Manager.GetClient("Google")` 取得 client，例如用來呼叫 `RefreshTokenAsync`。
- `OAuth2Manager` 的公開成員都是 virtual，protected 建構式會建立一個沒有任何 client 的 manager。測試 controller 時，可以傳入繼承它、
  只覆寫測試需要之成員的類別，不需要 service provider，也不需要真的登入 provider。
- ASP.NET Core 自己也有一個 `AuthorizationResult`，位於 `Microsoft.AspNetCore.Authorization`，也就是 `[Authorize]` 所在的命名空間。
  同一個檔案兩個命名空間都匯入、又寫出型別名稱時，會得到 CS0104 錯誤。改用 `var` 宣告變數，或加上
  `using AuthorizationResult = Polhem.OAuth2.AuthorizationResult;`。
- 以 form POST 導回的 provider（例如 [Sign in with Apple](../providers/apple.md)），回呼要同時接受 GET 與 POST，並排除在
  antiforgery 驗證之外；見[網頁應用程式：登入狀態怎麼保存](web-sign-in-state.md)。
- 從導向到回呼之間登入狀態怎麼保存，以及多台伺服器需要什麼，見[網頁應用程式：登入狀態怎麼保存](web-sign-in-state.md)。
  .NET MAUI 應用程式可以經由這個後端登入；見[.NET MAUI 應用程式](maui.md#經由後端中轉登入)。

[OAuthAspNetCore](../../../samples/OAuthAspNetCore) sample 是一個完整的應用程式。
