# Polhem.OAuth2

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)

輕量的 .NET OAuth2 登入套件。桌面與主控台應用程式透過系統瀏覽器、loopback 回呼與 PKCE 登入，可在 Windows、macOS、Linux 上使用。
ASP.NET Core 與 ASP.NET（System.Web）應用程式使用授權碼流程，state 存放在 cookie。

支援的 provider：Google、Facebook、LINE、Microsoft Entra ID、Auth0、Okta。

## 套件

| 套件 | 目標框架 | 用途 |
|------|----------|------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0、net10.0 | 各 provider，以及桌面與主控台應用程式的登入 |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core 應用程式 |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net48 | System.Web 上的 ASP.NET Web Forms 與 MVC 應用程式 |

```sh
dotnet add package Polhem.OAuth2
```

每個 provider 各有自己的 options 型別：`GoogleOAuth2Options`、`FacebookOAuth2Options`、`LineOAuth2Options`、
`AzureOAuth2Options`（Microsoft Entra ID）、`Auth0OAuth2Options`、`OktaOAuth2Options`。Auth0 與 Okta 另外需要 `Domain`，
Okta 可以再指定 `AuthorizationServerId`。

## 桌面與主控台應用程式

`LoopbackOAuth2Client` 會在回呼網址上監聽、用預設瀏覽器開啟授權網址、等待 provider 導回，再用授權碼換 token。

```csharp
using Polhem.OAuth2;

var options = new GoogleOAuth2Options
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "http://127.0.0.1:0/callback"
};

var client = new LoopbackOAuth2Client(options);
AuthorizationResult result = await client.SignInAsync();

if (result.IsSuccess && result.UserInfo is { } user)
    Console.WriteLine($"{user.UserId} {user.UserName} {user.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception?.Message}");
```

- 回呼網址必須是 `http`，主機必須是 `localhost` 或 loopback 位址，而且要在 provider 後台登記。
  port 寫 0 時每次登入都會挑一個可用的 port，只適用於接受任意 loopback port 的 provider。
- client 一律使用 PKCE。隨桌面應用程式散佈的 client secret 可以被取出，所以只會送給要求它的 Google。
- 逾時（`Timeout`，預設 5 分鐘）、取消、provider 回傳錯誤，都會成為失敗結果。port 無法監聽時會擲出 `SocketException`。
- 要用其他方式開啟網址時設定 `OpenBrowser`，例如透過 UI 框架提供的啟動器。

這個設計的理由，以及各 provider 對 loopback 回呼的實測結果，記錄在
[ADR-004](docs/adr/adr-004-system-browser-loopback.zh-TW.md)。

### 登記 loopback 回呼網址

以下登記方式已於 2026-09-14 在各 provider 實測。

| Provider | 應用程式類型 | 回呼網址 | port |
|----------|--------------|----------|------|
| Google | Desktop app | `http://127.0.0.1:0/callback` | 任意 port 都被接受 |
| Microsoft Entra ID | Mobile and desktop applications，登記為 `http://localhost` | `http://localhost:0` | 被忽略 |
| Auth0 | Native | `http://127.0.0.1:53682/callback` | 必須一致 |
| Okta | Native，Client authentication 選 None 並要求 PKCE | `http://localhost:53682/callback` | 必須一致 |
| LINE | LINE Login channel 的 Callback URL | `http://localhost:53682/callback` | 必須一致 |
| Facebook | Facebook Login 的 Valid OAuth Redirect URIs | `http://localhost:53682/callback` | 登記實際使用的 port |

- Okta：授權伺服器需要一個存取政策，並有允許 authorization code 的規則。沒有的話，登入會因政策評估失敗而被拒。
- Facebook 拒絕 `127.0.0.1`，請改用 `localhost`。
- 沒寫 port 的回呼網址等於 port 80，監聽它通常需要管理員權限，所以請寫出 port。

## ASP.NET Core

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();
builder.Services.AddSingleton(provider =>
{
    var accessor = provider.GetRequiredService<IHttpContextAccessor>();
    var options = new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:7032/auth/callback",
        UsePkce = true
    };

    var manager = new OAuth2Manager(accessor);
    manager.RegisterClient("Google", new OAuth2Client(options, accessor));
    return manager;
});

var app = builder.Build();
app.UseSession();
```

```csharp
public class AuthController(OAuth2Manager oauth2Manager) : Controller
{
    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        oauth2Manager.RedirectToAuthorization("Google");
        return new EmptyResult();
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback()
    {
        AuthorizationResult result = await oauth2Manager.ValidateAuthorization();
        return result.IsSuccess && result.UserInfo is { } user
            ? Content($"{user.UserId} {user.UserName} {user.Email}")
            : Content($"The sign-in failed: {result.Exception?.Message}");
    }
}
```

## ASP.NET（System.Web）

System.Web 的專案無法用 `dotnet` CLI 建置，所以沒有可執行的 sample。用法相同，透過靜態的 `OAuth2Manager`：

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNet;

// Global.asax.cs
protected void Application_Start()
{
    var options = new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:44300/auth/callback",
        UsePkce = true
    };
    OAuth2Manager.RegisterClient("Google", new OAuth2Client(options));
}
```

```csharp
// An MVC controller. In Web Forms, call OAuth2Manager.RedirectToAuthorization("Google") from the sign-in page,
// and await OAuth2Manager.ValidateAuthorization() on the callback page, which needs Async="true".
public class AuthController : Controller
{
    public ActionResult Login()
    {
        return Redirect(OAuth2Manager.GetAuthorizationUrl("Google"));
    }

    public async Task<ActionResult> Callback()
    {
        AuthorizationResult result = await OAuth2Manager.ValidateAuthorization();
        if (result.IsSuccess && result.UserInfo != null)
            return Content($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");

        return Content($"The sign-in failed: {result.Exception?.Message}");
    }
}
```

## 網頁應用程式：state cookie、session 與金鑰

- state 存放在標記為 `Secure` 的 cookie，所以回呼頁面必須走 HTTPS。
- 開啟 `UsePkce` 時，code verifier 存放在 session，所以必須啟用 session。
- state 裡帶著 client 註冊時的名稱。設定 `OAUTH2_STATE_KEY` 環境變數後，名稱會以 AES-CBC 加密、以 HMAC-SHA256 驗證。
  沒有金鑰時名稱只做 base64 編碼，既藏不住內容，也察覺不到竄改。

`OAUTH2_STATE_KEY` 是 64 位元組的亂數金鑰，以 base64 表示。每個行程只讀取一次，所以要在應用程式啟動前設定；
所有可能收到回呼的伺服器都要使用同一把金鑰。產生方式：

```sh
openssl rand 64 | openssl base64 -A
```

```csharp
Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)));
```

## 結果與錯誤

`AuthorizationResult.Exception` 只收納 OAuth2 登入預期會發生的失敗，例如 HTTP 請求失敗、state 無效、provider 回傳錯誤。
設定或程式錯誤（例如 client 名稱沒有註冊）會往外拋。見 [ADR-003](docs/adr/adr-003-exception-semantics.zh-TW.md)。

## 從 Bee.OAuth2 遷移

| Bee.OAuth2 套件 | 替代 |
|-----------------|------|
| `Bee.OAuth2` | `Polhem.OAuth2` |
| `Bee.OAuth2.AspNet` | `Polhem.OAuth2.AspNet` |
| `Bee.OAuth2.AspNetCore` | `Polhem.OAuth2.AspNetCore` |
| `Bee.OAuth2.WinForms`、`Bee.OAuth2.Desktop` | `Polhem.OAuth2` 的 `LoopbackOAuth2Client` |

- **命名空間**：`Bee.OAuth2` 改為 `Polhem.OAuth2`，其他套件依此類推。
- **桌面登入**從內嵌的 WebView2 視窗改為系統瀏覽器。同步的 `Authorization()`、`Caption`、`Width`、`Height`、`AuthorizationForm`，
  以及桌面版的 `OAuth2Client`、`OAuth2Manager`、`StateStorage` 都已移除，改呼叫 `LoopbackOAuth2Client.SignInAsync`。
- **回呼網址**：桌面應用程式要重新登記 loopback 回呼網址（見上表）。只在內嵌瀏覽器裡才能運作的網址，
  例如 `https://login.microsoftonline.com/common/oauth2/nativeclient`，已經無法使用。
- **例外**：`AuthorizationResult.Exception` 不再收納非預期的例外，它們會往外拋。
- **JSON null**：使用者資訊裡值為 JSON null 的欄位，現在是 `null`；Bee.OAuth2 回傳的是空字串。
  備援欄位因此會生效，例如 Entra ID 的 `oid` 為 JSON null 時改用 `sub`。
- **目標框架**：`Polhem.OAuth2.AspNetCore` 的目標框架是 net10.0（Bee.OAuth2.AspNetCore 是 net8.0）。
- **相依套件**：不再相依 `Bee.Base` 與 `Newtonsoft.Json`，JSON 改用 System.Text.Json 解析。
- **state 金鑰**：加密後的 state 格式不變，既有的 `OAUTH2_STATE_KEY` 可以繼續使用。見
  [ADR-001](docs/adr/adr-001-drop-bee-base.zh-TW.md)。

## Samples

每個 sample 都從 `OAuthConfig.json` 讀取 provider 設定。

| Sample | 示範 |
|--------|------|
| [OAuthConsole](samples/OAuthConsole) | 主控台應用程式的桌面登入，任何作業系統都能執行 |
| [OAuthDesktop](samples/OAuthDesktop) | .NET 上的 Windows Forms 桌面登入 |
| [OAuthWinForms](samples/OAuthWinForms) | .NET Framework 4.8 上的 Windows Forms 桌面登入 |
| [OAuthAspNetCore](samples/OAuthAspNetCore) | ASP.NET Core |

[LoopbackRedirectProbe](tools/LoopbackRedirectProbe/README.zh-TW.md) 可以在動工前先確認 provider 接不接受某個 loopback 回呼網址。

## 設計決策

設計背後的理由記錄在[架構決策紀錄](docs/adr/README.zh-TW.md)。

## 授權

[MIT](LICENSE.txt)。Copyright (c) Polhem contributors。

Polhem.OAuth2 延續自 [Bee.OAuth2](https://github.com/jeff377/bee-oauth2)。
