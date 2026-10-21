# Okta

[English](../../en/providers/okta.md) | **繁體中文**

`OktaOAuth2Options` 透過 OpenID Connect 以 Okta org 登入，結果的 provider 名稱是 `Okta`。

## Options

- `Domain`、`ClientId` 與 `RedirectUri` 為必填。`Domain` 是 org 的主機名稱，可以帶 `https://`，也可以不帶。
  所有 provider 共用的 options，見[所有 provider 共用的 Options](../guides/options.md)。
- Okta 預設使用 `default` 授權伺服器，可用 `AuthorizationServerId` 指定其他伺服器；設為空值時改用 org 授權伺服器。
  Okta 只在 Integrator Free Plan 的 org，或有 API Access Management 的 org 裡建立 `default` 伺服器，其他 org 請設為空值。
- 授權伺服器需要一個存取政策，並有允許 authorization code 的規則。沒有的話，登入會因政策評估失敗而被拒。
- 除非另外指定，Okta 會把網頁應用程式登記為 `client_secret_basic`。應用程式不接受請求本文裡的 secret 時，請把
  `ClientAuthentication` 設為 `ClientSecretBasic`；2026-09-26 實測的 Okta org 網頁應用程式兩種方式都接受。

## 桌面與主控台應用程式

| 應用程式類型 | 回呼網址 | port |
|--------------|----------|------|
| Native，Client authentication 選 None 並要求 PKCE | `http://localhost:53682/callback` | 必須一致 |

這個登記方式已於 2026-09-14 實測，沒送 client secret 也換 token 成功
（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。

## .NET MAUI 應用程式

把自訂 scheme 登記為 Native 應用程式的 Sign-in redirect URIs；在 iOS、Mac Catalyst 與 Android 上都登入成功。
見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

把回呼網址登記為 Web 應用程式的 Sign-in redirect URIs，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

`Scopes` 含 `offline_access`、且應用程式允許 Refresh Token grant 時，Okta 會發 refresh token。

## 前後端分離的應用程式

`GetUserInfoAsync` 擲出 `NotSupportedException`：由前端送出 ID token，後端驗證。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
