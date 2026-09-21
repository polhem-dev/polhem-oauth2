# Polhem.OAuth2

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)

輕量的 .NET OAuth2 登入套件。桌面與主控台應用程式透過系統瀏覽器、loopback 回呼與 PKCE 登入，可在 Windows、macOS、Linux 上使用。
Android、iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式透過 `WebAuthenticator` 登入，可以直連，也可以經由自己的後端中轉。
ASP.NET Core 與 ASP.NET（System.Web）應用程式使用搭配 PKCE 的授權碼流程，每次登入各自保存在一個加密的 cookie。

支援的 provider：Google、Facebook、LINE、Microsoft Entra ID、Auth0、Okta。

## 套件

| 套件 | 目標框架 | 用途 |
|------|----------|------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0、net10.0 | 各 provider、桌面、主控台與 .NET MAUI 應用程式的登入，以及其他伺服器端框架 |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core 應用程式 |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net472 | System.Web 上的 ASP.NET Web Forms 與 MVC 應用程式 |

```sh
dotnet add package Polhem.OAuth2
```

## Options

每個 provider 各有自己的 options 型別：`GoogleOAuth2Options`、`FacebookOAuth2Options`、`LineOAuth2Options`、
`AzureOAuth2Options`（Microsoft Entra ID）、`Auth0OAuth2Options`、`OktaOAuth2Options`。

- `ClientId` 與 `RedirectUri` 為必填。client 建立時會複製並檢查 options，之後再修改原物件不會有影響；options 不合法時當場擲出 `ArgumentException`。
- Auth0 與 Okta 需要 `Domain`，例如 `your-tenant.auth0.com`。Okta 預設使用 `default` 授權伺服器，
  可用 `AuthorizationServerId` 指定其他伺服器；設為空值時改用 org 授權伺服器。
- Microsoft Entra ID 預設使用 `common` tenant。只註冊在單一 tenant 的應用程式，要把 `Tenant` 設成該 tenant 的 ID 或網域名稱。
- 所有端點都必須是絕對的 `https` URI。
- `UsePkce` 預設為 `true`。

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

if (result.IsSuccess)
    Console.WriteLine($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception.Message}");
```

- 回呼網址必須是 `http`，主機必須是 `localhost` 或 loopback 位址，而且要在 provider 後台登記。
  port 寫 0 時每次登入都會挑一個可用的 port，只適用於接受任意 loopback port 的 provider。
- client 一律使用 PKCE。隨桌面應用程式散佈的 client secret 可以被取出，所以不會送出，只有 Google 例外。
- 逾時（`Timeout`，預設 5 分鐘）、取消、provider 回傳錯誤，都會成為失敗結果。port 無法監聽時擲出 `SocketException`，
  找不到預設瀏覽器時擲出 `Win32Exception`（iOS 無法啟動處理程序，擲出 `PlatformNotSupportedException`）。
- 要用其他方式開啟網址時設定 `OpenBrowser`，例如搭配 UI 框架的啟動器寫成 `uri => launcher.LaunchUriAsync(uri)`。
- 這段範例使用最上層陳述式。在 .NET Framework 的 Windows Forms 應用程式裡怎麼登入，見 [OAuthWinForms](samples/OAuthWinForms) sample。
- 目標框架為 .NET Framework 4.7.2、並在開啟 FIPS 模式的機器上執行的應用程式，需要 ASP.NET（System.Web）一節「部署前確認」裡的設定。
- 要再登入另一個後端時，見[前後端分離的應用程式](#前後端分離的應用程式)。

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

- Google：實測的 client 有登記回呼網址。Google 的文件對 Desktop app 是否需要登記說法不一，並把 client secret 列為已安裝應用程式的選填參數。
- Okta：授權伺服器需要一個存取政策，並有允許 authorization code 的規則。沒有的話，登入會因政策評估失敗而被拒。
- Facebook 拒絕 `127.0.0.1`，請改用 `localhost`。實測時 app 是開發模式還是上線模式，沒有記錄。
- 沒寫 port 的回呼網址等於 port 80，監聽它通常需要管理員權限，所以請寫出 port。

## .NET MAUI 應用程式

Android、iOS 或 Mac Catalyst 上的應用程式有兩種登入方式（[ADR-006](docs/adr/adr-006-app-sign-in.zh-TW.md)）：

- **直連**：給沒有自己後端的應用程式。`AppOAuth2Client` 以 `WebAuthenticator` 開啟登入頁，provider 導回應用程式的 URI scheme，
  client 再以 PKCE、不帶 client secret 換取授權碼。
- **經由後端中轉**：給要登入自己 ASP.NET Core 後端的應用程式。後端以 web client 身分登入，交給應用程式一個只能用一次的
  code，應用程式再以它兌換使用者資訊。Google 與 LINE 不接受直接導回 Android 應用程式，所以在 Android 上必須用這個方式。

`WebAuthenticator` 在 Windows 上無法使用，所以 MAUI 應用程式的 Windows 平台跟桌面應用程式一樣，以 `LoopbackOAuth2Client` 登入。

### 直連登入

```csharp
using Polhem.OAuth2;

var options = new Auth0OAuth2Options
{
    Domain = "your-tenant.auth0.com",
    ClientId = "your-native-client-id",
    RedirectUri = "com.example.app:/oauth2redirect"
};

var client = new AppOAuth2Client(options, async (url, redirectUri, cancellationToken) =>
    (await WebAuthenticator.Default.AuthenticateAsync(url, redirectUri)).CallbackUri);

AuthorizationResult result = await client.SignInAsync();
```

- 回呼網址是自訂 scheme 或 `https` 網址。`http`、`javascript`、`data`、`file`、相對網址，以及帶 fragment 的網址都會被拒絕。
- 每個 provider 要求各自的導回形式與後台登記，例如 Facebook 是 `fb<app id>://authorize`，LINE 在 iOS 上是
  `line3rdp.<bundle id>://auth`。[ADR-006](docs/adr/adr-006-app-sign-in.zh-TW.md) 的表格列出每一家，以及實測過的平台。
- 不要設定 client secret：隨應用程式散佈的任何內容都能被取出。
- 使用者中途關閉登入，會成為帶 `OperationCanceledException` 的失敗結果；provider 回傳錯誤（例如 `access_denied`）則是帶
  `OAuth2Exception` 的失敗結果。
- 使用者資訊只留在應用程式裡，不能當作伺服器端的身份證明；要登入自己後端的應用程式，請改用後端中轉。

### 平台設定

應用程式必須能接收導回自己 scheme 的網址：

- iOS 與 Mac Catalyst：在 `Info.plist` 的 `CFBundleURLTypes` 列出 scheme。在 App Sandbox 裡執行的 Mac Catalyst 應用程式，
  另外需要 `com.apple.security.network.client` entitlement。
- Android：加一個繼承 `WebAuthenticatorCallbackActivity` 的 activity，以 intent filter 接收該 scheme；並在
  `AndroidManifest.xml` 的 `<queries>` 宣告 Custom Tabs 服務，Android 11 以後要有這項宣告才能開啟它。

```csharp
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "com.example.app")]
public class CallbackActivity : WebAuthenticatorCallbackActivity
{
}
```

```xml
<queries>
  <intent>
    <action android:name="android.support.customtabs.action.CustomTabsService" />
  </intent>
</queries>
```

### 經由後端中轉登入

後端照 ASP.NET Core 一節登記 web client，再登記應用程式的回呼網址：

```csharp
builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("com.example.app:/relay"));
```

- 應用程式產生 PKCE 的 code verifier，帶著自己的回呼網址與 S256 code challenge 開啟後端的網址。該端點呼叫
  `oauth2Manager.RedirectToAppAuthorization(HttpContext, "Google", redirectUri, codeChallenge)`。
- provider 導回後端原本的回呼端點。`CompleteAuthorizationAsync` 之後，
  `oauth2Manager.RedirectToAppAsync(HttpContext, result, cancellationToken)` 會把由應用程式發起的登入，帶著一次性 code
  導回應用程式並回傳 true；瀏覽器裡的登入則回傳 false。
- 應用程式把 code 與它的 verifier POST 給後端，後端以 `oauth2Manager.RedeemAppCodeAsync` 取得使用者資訊，或得到 null。
  接著由後端發給應用程式自己的 session；provider 的 token 留在後端。
- code 存在 `IDistributedCache`。有多台伺服器時，改用分散式快取並共用 data protection 金鑰，跟網頁登入一樣。
- [OAuthAspNetCore](samples/OAuthAspNetCore) 與 [OAuthMaui](samples/OAuthMaui) 兩個 sample 示範了兩端的寫法。

## ASP.NET Core

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
  所以 options 不合法時應用程式在啟動時就會停下來。另外可以傳入選填的 `HttpClient`。
- `oauth2Manager.GetClient("Google")` 取得 client，例如用來呼叫 `RefreshTokenAsync`。

## ASP.NET（System.Web）

System.Web 的專案無法用 `dotnet` CLI 建置，所以沒有可執行的 sample。用法相同，透過靜態的 `OAuth2Manager`：

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

部署前確認：

- 目標框架為 .NET Framework 4.7.2 或更新的版本，並在 `web.config` 設定 `<httpRuntime targetFramework="4.7.2" />`（或你使用的更新版本）。
  非同步頁面與作業系統的 TLS 預設值都取決於這個設定。
- 在 .NET Framework 上，核心套件相依於 System.Text.Json。NuGet 為它及其相依套件加入 `web.config` 的 binding redirect 要保留。
- 有多台伺服器可能收到回呼時，每一台的 `web.config` 都要設定相同的 `<machineKey>`。
- 在開啟 FIPS 模式的機器上，目標框架為 .NET Framework 4.7.2 的應用程式，開始登入時可能擲出 `CryptographicException`：
  對這類應用程式，.NET Framework 會擋下 PKCE 使用的 managed SHA-256 實作。請把目標框架改為 .NET Framework 4.8 或更新的版本，
  或把 `Switch.System.Security.Cryptography.UseLegacyFipsThrow` 開關設為 `false`。
  見 [Managed cryptography classes do not throw a CryptographyException in FIPS mode](https://learn.microsoft.com/dotnet/framework/migration-guide/retargeting/4.8.x#managed-cryptography-classes-do-not-throw-a-cryptographyexception-in-fips-mode)。

## 網頁應用程式：登入狀態怎麼保存

- 每次登入都把 state、PKCE code verifier、回呼網址與 client 名稱，放在一個專屬的 cookie 裡，
  以 ASP.NET Core 的 data protection 或 `MachineKey` 加密並驗證。不需要 session，在多個分頁同時登入也不會互相覆蓋。
  見 [ADR-005](docs/adr/adr-005-web-sign-in-cookie.zh-TW.md)。
- cookie 名稱以 `__Host-` 開頭，並設為 `Secure`、HTTP-only、`SameSite=Lax`，所以登入的起點與回呼頁面都必須走 HTTPS。
- 登入必須在 10 分鐘內完成。回呼時會先刪除 cookie，再用授權碼換 token。
- 所有可能收到回呼的伺服器都必須能解開 cookie：ASP.NET Core 要共用 data protection 的金鑰環，System.Web 要使用相同的 machine key。

## 其他伺服器端框架

核心套件的 `OAuth2Client` 不依賴任何 HTTP 框架，走的是同一套流程。待完成的值要存放在只有發起登入的瀏覽器才拿得出來的地方，
例如加密的 HTTP-only cookie，並在處理完回呼後刪除。

```csharp
var client = new OAuth2Client(options);

// Start the sign-in. Keep and Redirect stand for code of your framework.
AuthorizationRequest request = client.CreateAuthorizationRequest();
Keep(request.Pending.State, request.Pending.CodeVerifier, request.Pending.RedirectUri);
Redirect(request.Url);

// Complete it in the callback.
var pending = new PendingAuthorization(keptState, keptCodeVerifier, keptRedirectUri);
var callback = new AuthorizationCallback(query["code"], query["state"], query["error"], query["error_description"]);
AuthorizationResult result = await client.CompleteAuthorizationAsync(callback, pending, cancellationToken);
```

## 結果、token 與錯誤

- 成功的結果有 `ProviderName`、`UserInfo` 與 `Token`；失敗的結果有 `Exception`。
- `Token` 帶著 access token，以及 provider 有回傳時的 refresh token、ID token、有效期限與 scope。
  用 client 的 `RefreshTokenAsync` 取得新的 token。有些 provider 每次都會發新的 refresh token，所以要保留最新一次回應裡的那個。
  Facebook 不發 refresh token。
- `Exception` 只收納登入預期會發生的失敗，例如 HTTP 請求失敗、state 不相符、provider 回傳錯誤。
  設定或程式錯誤（例如 client 名稱沒有註冊）會往外拋。見 [ADR-003](docs/adr/adr-003-exception-semantics.zh-TW.md)。
- provider 回傳的錯誤是 `OAuth2Exception`：`Error` 是錯誤代碼，例如 `access_denied`；`ErrorDescription` 是 provider 提供的說明。
  導回網址裡的錯誤，任何送連結給使用者的人都能設定，所以顯示前要先編碼。Facebook 的 token 端點回傳的是 Graph API 的錯誤：
  `Error` 是它的數字代碼，例如 `100`；`ErrorDescription` 是它的訊息。

## 識別使用者

- 以 provider 名稱加上 `UserInfo.UserId` 識別使用者，不要用 `Email`：email 可能變更，而且各 provider 是否驗證過 email 並不一致。
- provider 名稱是 `AuthorizationResult.ProviderName`：`Google`、`Facebook`、`LINE`、`Azure`（Microsoft Entra ID）、`Auth0` 或 `Okta`。
  它與 client 註冊時使用的名稱無關。
- Microsoft Entra ID 的 `UserId` 是 `sub` claim，同一個使用者登入不同的應用程式時，這個值也不同。
- LINE 只在 ID token 裡提供 email，而且要 channel 有權限讀取、使用者也同意才會有。函式庫從 token 端點回傳的 ID token 讀出 email，
  會檢查這個 token 是發給這個 client 的，但不檢查簽章。
- 函式庫不驗證 ID token。`Token.IdToken` 是 provider 原樣回傳的內容，要依賴其中的 claim 前請先自行驗證。

## 前後端分離的應用程式

桌面應用程式的範例為了簡潔，一次呼叫就完成登入並取得使用者資訊，適合自己使用登入結果的應用程式。
若前端登入後還要登入另一個後端，不要把前端取得的使用者資訊交給後端當作身份證明：後端無法分辨它是否遭到偽造。

- 前端登入後，透過 HTTPS 把 token 交給後端，由後端用這個 token 向 provider 取得使用者資訊。
- 後端採信 token 之前，要先確認它是發給自己 client ID 的，例如使用 provider 的 token 驗證端點，或驗證 ID token 的簽章與 audience。
  只呼叫使用者資訊端點無法確認這一點：其他應用程式替同一個使用者取得的 token，也會回傳同一個使用者。
- 函式庫沒有提供這些後端步驟。.NET MAUI 應用程式可以改為經由自己的 ASP.NET Core 後端中轉登入，見 .NET MAUI 一節。

## 從 Bee.OAuth2 遷移

| Bee.OAuth2 套件 | 替代 |
|-----------------|------|
| `Bee.OAuth2` | `Polhem.OAuth2` |
| `Bee.OAuth2.AspNet` | `Polhem.OAuth2.AspNet` |
| `Bee.OAuth2.AspNetCore` | `Polhem.OAuth2.AspNetCore` |
| `Bee.OAuth2.WinForms`、`Bee.OAuth2.Desktop` | `Polhem.OAuth2` 的 `LoopbackOAuth2Client` |

- **命名空間**：`Bee.OAuth2` 改為 `Polhem.OAuth2`，其他套件依此類推。
- **網頁端的註冊**：ASP.NET Core 以 `AddOAuth2Client` 註冊每個 client，System.Web 用 `OAuth2Manager.RegisterClient(name, options)`。
  不再使用 session 與 `OAUTH2_STATE_KEY`。
- **網頁端的方法**：`GetAuthorizationUrl` 改為 `CreateAuthorizationUrl`，`ValidateAuthorization` 改為 `CompleteAuthorizationAsync`；
  ASP.NET Core 版要傳入 `HttpContext`。升級前已開始的登入，升級後無法完成，使用者要重新登入。
- **桌面登入**從內嵌的 WebView2 視窗改為系統瀏覽器。同步的 `Authorization()`、`Caption`、`Width`、`Height`、`AuthorizationForm`，
  以及桌面版的 `OAuth2Client`、`OAuth2Manager`、`StateStorage` 都已移除，改呼叫 `LoopbackOAuth2Client.SignInAsync`。
- **回呼網址**：桌面應用程式要重新登記 loopback 回呼網址（見上表）。只在內嵌瀏覽器裡才能運作的網址，
  例如 `https://login.microsoftonline.com/common/oauth2/nativeclient`，已經無法使用。
- **結果與 token**：`AuthorizationResult` 改為唯讀。`AccessToken` 改為 `Token.AccessToken`；更新 token 改用 client 的 `RefreshTokenAsync`，回傳 `TokenResponse`。
- **不再公開的型別**：各 provider 類別、`IOAuth2Provider`、`BaseOAuth2Client`、`IStateStorage`、`PkceHelper`、`OAuth2StateCryptor`。
  應用程式改用 options 型別搭配 client 或 manager。
- **PKCE 與 client secret**：`UsePkce` 預設為 `true`。網頁端的 client 只要設了 client secret 就一律送出，開啟 PKCE 時也一樣。
- **端點**必須是 `https`。Auth0 與 Okta 的 `Domain` 接受主機名稱，可以帶 `https://`，也可以不帶。
- **例外**：`AuthorizationResult.Exception` 不再收納非預期的例外，它們會往外拋。
- **JSON null**：使用者資訊裡值為 JSON null 的欄位，現在是 `null`；Bee.OAuth2 回傳的是空字串。
  備援欄位因此會生效，例如 Auth0 的 `name` 為 JSON null 時改用 `nickname`。
- **目標框架**：`Polhem.OAuth2.AspNet` 的目標框架是 .NET Framework 4.7.2，`Polhem.OAuth2.AspNetCore` 是 net10.0。
- **相依套件**：不再相依 `Bee.Base` 與 `Newtonsoft.Json`。JSON 改用 System.Text.Json 解析，netstandard2.0 組建以套件參照它。

## Samples

所有 sample 與 loopback 回呼網址實測工具，都從 repo 根目錄的同一份 `OAuthConfig.json` 讀取 provider 設定。
先把 `OAuthConfig.example.json` 複製成 `OAuthConfig.json` 再填入，建置時會複製到每個專案的輸出目錄。
`OAuthConfig.json` 已被 git 忽略；不要把憑證填進 `OAuthConfig.example.json`。

provider 在後台是桌面、Web 與行動 App 的 client 分開登記的，所以每個 provider 依 client 類型各有一組 client：

```json
{
  "Providers": {
    "Okta": {
      "Domain": "",
      "Desktop": { "ClientId": "", "RedirectUri": "http://localhost:53682/callback" },
      "Web": { "ClientId": "", "ClientSecret": "", "RedirectUri": "https://localhost:7032/auth/callback" }
    }
  }
}
```

- 主控台與 Windows Forms sample 和實測工具讀 `Desktop`，ASP.NET Core sample 讀 `Web`。client 區段可以寫該 provider
  options 型別的屬性，例如 `Scopes` 與 `UsePkce`；沒寫的沿用型別的預設值。
- 兩組 client 共用、而且不是憑證的欄位寫在 provider 層：Auth0 與 Okta 的 `Domain`、Okta 的 `AuthorizationServerId`、
  Azure 的 `Tenant`。這層出現其他欄位會報錯，憑證因此不會不小心被共用。
- 後台其實只登記一個 client 的 provider（例如一個 LINE channel 登記兩個回呼網址），兩組填相同的 `ClientId`
  與 `ClientSecret` 即可。
- `ClientId` 還留空的區段會被略過，所以 sample 只會出現你已經填好的 provider。
- OAuthMaui sample 在 iOS 與 Mac Catalyst 讀 `iOS`，在 Android 讀 `Android`，在 Windows 讀 `Desktop`。`iOS` 與
  `Android` 區段不能有 `ClientSecret`，因為 App 無法保密 secret：有的話載入會失敗。OAuthMaui 建置時只打包
  `ClientId`、`RedirectUri`、`Scopes`、`UsePkce` 與共用欄位，不會打包任何 secret。Google 與 LINE 不接受直接導回
  Android App（[ADR-006](docs/adr/adr-006-app-sign-in.zh-TW.md)），所以沒有 `Android` 區段，sample 在 Android 上
  改由後端登入它們。
- 最上層的 `AppRelay` 區段設定後端中轉：`BackendUrl` 是 ASP.NET Core sample 的網址，`RedirectUri` 是 App 的中轉回呼網址，
  ASP.NET Core sample 會用它呼叫 `AddOAuth2AppRelay` 登記。中轉走 HTTPS，所以模擬器必須信任 ASP.NET Core 的開發憑證。
  Android 模擬器上先執行 `adb reverse tcp:7032 tcp:7032`，讓 `localhost` 連到主機。

| Sample | 示範 |
|--------|------|
| [OAuthConsole](samples/OAuthConsole) | 主控台應用程式的桌面登入，任何作業系統都能執行 |
| [OAuthDesktop](samples/OAuthDesktop) | .NET 上的 Windows Forms 桌面登入 |
| [OAuthWinForms](samples/OAuthWinForms) | .NET Framework 4.8 上的 Windows Forms 桌面登入 |
| [OAuthAspNetCore](samples/OAuthAspNetCore) | ASP.NET Core，包括給 OAuthMaui 用的中轉端點 |
| [OAuthMaui](samples/OAuthMaui) | .NET MAUI：Android、iOS 與 Mac Catalyst 的直連與後端中轉登入，Windows 的 loopback 登入。需要 MAUI workload，不在 solution 裡。 |

[LoopbackRedirectProbe](tools/LoopbackRedirectProbe/README.zh-TW.md) 可以在動工前先確認 provider 接不接受某個 loopback 回呼網址。

## 設計決策

設計背後的理由記錄在[架構決策紀錄](docs/adr/README.zh-TW.md)。

## 授權

[MIT](LICENSE.txt)。Copyright (c) Polhem contributors。

Polhem.OAuth2 延續自 [Bee.OAuth2](https://github.com/jeff377/bee-oauth2)。
