# 從 Bee.OAuth2 遷移

[English](../../en/guides/migrating-from-bee-oauth2.md) | **繁體中文**

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
- **回呼網址**：桌面應用程式要重新登記 loopback 回呼網址（見[登記 loopback 回呼網址](desktop.md#登記-loopback-回呼網址)）。
  只在內嵌瀏覽器裡才能運作的網址，例如 `https://login.microsoftonline.com/common/oauth2/nativeclient`，已經無法使用。
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
