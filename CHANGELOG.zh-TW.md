# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.OAuth2、Polhem.OAuth2.AspNet 與 Polhem.OAuth2.AspNetCore 的重要變更。格式依循
[Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

### 新增

- `OAuth2Options.IsAppRedirectUri`：檢查一個回呼網址能否把登入結果送回應用程式。這就是 `AppOAuth2Client` 原本套用的規則；
  Polhem.OAuth2.AspNetCore 的後端中轉現在對 `OAuth2AppRelayOptions.AppRedirectUris` 呼叫它，不再複寫一份。

### 變更

- 登入所指的應用程式回呼網址已不在 `OAuth2AppRelayOptions.AppRedirectUris` 裡時，`OAuth2Manager.RedirectToAppAsync` 擲出
  `InvalidOperationException`，不再導向該網址。回呼請求中斷時它也會停止，與 `CompleteAuthorizationAsync` 一致，
  因此不需要再傳入 `HttpContext.RequestAborted`。

### 修正

- Facebook 的 token 端點回傳的錯誤，與其他 provider 一樣成為 `OAuth2Exception`。Facebook 回傳的是 Graph API 的錯誤物件，
  先前被當成沒有錯誤代碼的回應，擲出 `HttpRequestException`。`Error` 是 Graph API 錯誤的數字代碼，`ErrorDescription` 是它的訊息。

### 安全性

- 後端中轉以 ASP.NET Core data protection 保護它存進 `IDistributedCache` 的資料。1.1.0 在代碼兌換之前，把中轉登入的使用者資訊
  未加保護地存在快取裡，並且信任快取回傳的任何內容，因此快取必須跟應用程式本身一樣可信。兩個版本的伺服器共用同一個快取的期間，
  一個版本發出的代碼不會被另一個版本兌換。

## [1.1.0] - 2026-09-19

### 新增

- `AppOAuth2Client`：Android、iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式登入。它透過 `WebAuthenticator` 之類的委派開啟登入頁，
  接受自訂 scheme 與 `https` 回呼網址，一律使用 PKCE，因此不需要 client secret。見 [ADR-006](docs/adr/adr-006-app-sign-in.zh-TW.md)。
- Polhem.OAuth2.AspNetCore 的後端中轉：`AddOAuth2AppRelay`、`OAuth2AppRelayOptions`，以及
  `OAuth2Manager.RedirectToAppAuthorization`、`RedirectToAppAsync`、`RedeemAppCodeAsync`，讓應用程式經由自己的後端登入；
  provider 的 token 留在後端，應用程式拿到一個只能用一次的 code。
- Polhem.OAuth2 的 net10.0 版本標示為 AOT 相容，由 trim 與 AOT analyzer 檢查。
- OAuthMaui sample，以及 OAuthAspNetCore sample 的中轉端點。

### 變更

- `AppOAuth2Client` 以 `fb<app id>` 回呼網址登入 Facebook 時，token 請求會補上 Facebook 要求的結尾斜線。
- `LoopbackOAuth2Client.SignInAsync` 的文件列出 `PlatformNotSupportedException`：iOS 上的預設瀏覽器會擲出這個例外。

### 修正

- Microsoft Entra ID provider 在個人 Microsoft 帳戶登入時也會填入 `UserName`：這類帳戶不傳 `name` claim，改由名字與姓氏組成
  （[#2](https://github.com/polhem-dev/polhem-oauth2/issues/2)）。

## [1.0.0] - 2026-09-14

以 Polhem 名義發佈的第一版，延續自 [Bee.OAuth2](https://github.com/jeff377/bee-oauth2)；以下變更都是相對於 Bee.OAuth2 的最後一版。
應用程式如何遷移，見 README 的[從 Bee.OAuth2 遷移](README.zh-TW.md#從-beeoauth2-遷移)。

### 新增

- `LoopbackOAuth2Client`：桌面與主控台應用程式透過系統瀏覽器與 loopback 回呼登入，一律使用 PKCE，可在 Windows、macOS、Linux 上使用。
  見 [ADR-004](docs/adr/adr-004-system-browser-loopback.zh-TW.md)。
- `OAuth2Client`：不依賴 HTTP 框架的授權碼流程，分成 `CreateAuthorizationRequest` 與 `CompleteAuthorizationAsync` 兩個步驟。
- `TokenResponse`：帶著 access token、refresh token、ID token，以及 token 類型、有效期限與 scope。
  `AuthorizationResult.Token` 帶著它，每個 client 的 `RefreshTokenAsync` 可以取得新的 token。
- `AddOAuth2Client`：在 ASP.NET Core 應用程式註冊 client、`OAuth2Manager` 與 data protection。
- `OAuth2Exception`：協定層級的失敗擲出這個例外。`Error` 與 `ErrorDescription` 帶著 provider 回傳的錯誤代碼與說明。
- `AzureOAuth2Options.Tenant`：給只註冊在單一 Microsoft Entra ID tenant 的應用程式使用。
- Okta provider（`OktaOAuth2Options`）。`AuthorizationServerId` 設為空值時改用 org 授權伺服器。
- LINE provider 從 ID token 讀取 email。
- 每個 client 都可以傳入 `HttpClient`，非同步方法都接收 `CancellationToken`。
- Polhem.OAuth2 新增 net10.0 目標框架，這個目標沒有任何套件相依。
- 公開 API 加上 nullable 參考型別標註。

### 變更

- 套件 ID 與命名空間由 `Bee.OAuth2` 改名為 `Polhem.OAuth2`。
- 網頁套件把每次登入各自保存在一個 cookie 裡，以 ASP.NET Core 的 data protection 或 `MachineKey` 保護，
  不再使用 session 與 `OAUTH2_STATE_KEY` 環境變數。見 [ADR-005](docs/adr/adr-005-web-sign-in-cookie.zh-TW.md)。
- `OAuth2Manager` 提供 `CreateAuthorizationUrl`、`RedirectToAuthorization`、`CompleteAuthorizationAsync` 與 `GetClient`。
  ASP.NET Core 版的方法接收 `HttpContext`，System.Web 版的 `RegisterClient` 接收 options。
- `UsePkce` 預設為 `true`。
- 網頁端的 client 只要設了 client secret 就一律送出，開啟 PKCE 時也一樣；loopback client 只送給 Google。
- client 建立時會複製並檢查 options。端點必須是絕對的 https URI，Auth0 與 Okta 的 `Domain` 只接受 https。
- `AuthorizationResult` 與 `UserInfo` 改為 sealed 且唯讀，`AuthorizationResult.AccessToken` 改為 `Token`。
- Polhem.OAuth2.AspNet 的目標框架由 .NET Framework 4.8 改為 4.7.2。
- Polhem.OAuth2.AspNetCore 的目標框架由 net8.0 改為 net10.0，並改參照 ASP.NET Core 共用框架，不再參照 `Microsoft.AspNetCore.Http.Abstractions` 套件。
- JSON 改用 System.Text.Json 解析，netstandard2.0 組建以套件參照它。套件不再相依 `Bee.Base` 與 `Newtonsoft.Json`。
  見 [ADR-001](docs/adr/adr-001-drop-bee-base.zh-TW.md)。
- 使用者資訊裡值為 JSON null 的欄位，現在是 `null` 而不是空字串，備援欄位因此會生效。
- Facebook 改用 Graph API v26.0，Google 改用現行的授權與使用者資訊端點。
- `AuthorizationResult.Exception` 只收納 OAuth2 登入預期會發生的失敗，其他例外會往外拋。token 端點回傳的錯誤會成為 `OAuth2Exception`。
  例外訊息會帶 HTTP 狀態碼，但不帶回應內容。見 [ADR-003](docs/adr/adr-003-exception-semantics.zh-TW.md)。
- Microsoft Entra ID 的使用者識別碼讀取 `sub`，這是 OpenID Connect 使用者資訊端點回傳的欄位。

### 移除

- `Bee.OAuth2.WinForms` 與 `Bee.OAuth2.Desktop`，以及內嵌 WebView2 的登入視窗。請改用 `LoopbackOAuth2Client`。
- 各 provider 類別、`IOAuth2Provider`、`BaseOAuth2Client`、`IStateStorage`、`PkceHelper`、`OAuth2StateCryptor`，
  以及網頁套件的 `OAuth2Client` 與 `StateStorage`。應用程式改用 options 型別搭配 client 或 manager。

### 安全性

- 每次登入都產生新的隨機 state 與 PKCE code verifier，網頁應用程式在回呼之前以加密並驗證的方式保存它們。
  登入用的 cookie 以 `__Host-` 為前綴，並設為 Secure、HTTP-only、SameSite=Lax。
- loopback 監聽程式只接受 Host 標頭為回呼網址主機、且帶著本次登入 state 的請求，並且同時讀取多條連線。
- provider 端點必須使用 https。

[Unreleased]: https://github.com/polhem-dev/polhem-oauth2/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
