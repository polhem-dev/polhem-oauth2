# Google

[English](../../en/providers/google.md) | **繁體中文**

`GoogleOAuth2Options` 透過 OpenID Connect 以 Google 帳號登入，結果的 provider 名稱是 `Google`。
Google 的桌面、Web、iOS 與 Android client 分開登記，各自是 Google Cloud console 裡一個對應類型的 OAuth client。

## Options

- `ClientId` 與 `RedirectUri` 為必填，桌面與網頁 client 另外設定 `ClientSecret`。所有 provider 共用的 options，
  見[所有 provider 共用的 Options](../guides/options.md)。
- 函式庫會檢查每次登入取得的 ID token：它必須由 `https://accounts.google.com` 或 `accounts.google.com` 發給 `ClientId`，
  而且與使用者資訊是同一個使用者（[ADR-008](../../adr/adr-008-generic-openid-connect-provider.zh-TW.md)）。

## 桌面與主控台應用程式

| 應用程式類型 | 回呼網址 | port |
|--------------|----------|------|
| Desktop app | `http://127.0.0.1:0/callback` | 任意 port 都被接受 |

- 這個登記方式已於 2026-09-14 實測（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。
- 實測的 client 有登記回呼網址。Google 的文件對 Desktop app 是否需要登記說法不一，並把 client secret 列為已安裝應用程式的
  選填參數。
- `LoopbackOAuth2Client` 會把 client secret 送給 Google，因為 2026-09-26 的實測中，Google 的 token 端點拒絕了 Desktop app
  client 不帶 secret 的授權碼。基於同樣理由，桌面應用程式要用 `GoogleOAuth2Options`，而不是
  [`OidcOAuth2Options`](openid-connect.md)，後者在桌面應用程式不送 secret。

## .NET MAUI 應用程式

- iOS 與 Mac Catalyst 以填了 bundle ID 的 iOS client 直連登入。回呼網址是
  `com.googleusercontent.apps.<client id>:/oauthredirect`，或與 bundle ID 相同的自訂 scheme，而且不需要 client secret：
  Google 依 client 類型決定。
- Android 經由後端中轉登入。Android client 拒絕導回自訂 scheme（「Custom URI scheme is not enabled for your Android
  client」）。

見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

登記一個 Web application 類型的 OAuth client，以回呼網址作為它的 redirect URI，再依
[ASP.NET Core](../guides/aspnetcore.md) 或 [ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。
網頁應用程式也可以用從 `https://accounts.google.com` 讀取的 [`OidcOAuth2Options`](openid-connect.md) 登入 Google。

## Token 與使用者

- Google 對桌面與行動應用程式都會發 refresh token，對網頁應用程式則只在 `AuthorizationEndpoint`
  帶著 `?access_type=offline&prompt=consent` 時發。
- `UserId` 是 `sub` claim，一個以字串表示的數字。

## 前後端分離的應用程式

由前端送出 ID token，後端驗證。`GetUserInfoAsync` 也接受 Google 的 access token，`AcceptedClientIds` 可以列出同一個
Google Cloud 專案的 iOS 與 Android client。見[前後端分離的應用程式](../guides/separate-back-end.md)。
