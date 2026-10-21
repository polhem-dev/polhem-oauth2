# Facebook

[English](../../en/providers/facebook.md) | **繁體中文**

`FacebookOAuth2Options` 透過 OAuth2 與 Facebook Login 以 Facebook 帳號登入，結果的 provider 名稱是 `Facebook`。

## Options

`ClientId` 與 `RedirectUri` 為必填，網頁應用程式另外設定 `ClientSecret`。所有 provider 共用的 options，
見[所有 provider 共用的 Options](../guides/options.md)。

## 桌面與主控台應用程式

| 後台登記 | 回呼網址 | port |
|----------|----------|------|
| Facebook Login 的 Valid OAuth Redirect URIs | `http://localhost:53682/callback` | 登記實際使用的 port |

- 這個登記方式已於 2026-09-14 實測（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。
- Facebook 拒絕 `127.0.0.1`，請改用 `localhost`。實測時 app 是開發模式還是上線模式，沒有記錄。
- ADR-004 與 ADR-006 的實測中 Facebook 接受不帶 client secret 的 PKCE，但官方文件沒有寫。

## .NET MAUI 應用程式

- 應用程式在 iOS、Mac Catalyst 與 Android 上以回呼網址 `fb<app id>://authorize` 直連登入，後台登記為填了 bundle ID 的
  iOS 平台；實測時不需要登記 Android 平台。
- Facebook 導回這個網址時多一個結尾斜線，函式庫會在 token 請求補上這個斜線。
- Facebook 在所有測過的平台上，都拒絕一般的自訂 scheme，例如 `com.example.app:/oauth2redirect`。

見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

在 Valid OAuth Redirect URIs 登記回呼網址，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

- Facebook 從不發 refresh token。
- 使用者登入的每個 app，`UserId` 都不同。

## 錯誤

Facebook 的 token 端點回傳的是 Graph API 的錯誤，而不是 OAuth2 錯誤：`OAuth2Exception.Error` 是它的數字代碼，
例如 `100`；`ErrorDescription` 是它的訊息。見[結果、token 與錯誤](../guides/results-and-errors.md)。

## 前後端分離的應用程式

Facebook 不發標準 ID token，所以由前端送出 access token，後端呼叫 `GetUserInfoAsync`。options 需要 `ClientSecret`，
前端也要和後端用同一個 Facebook app。查證時 token 放在請求的 query 裡。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
