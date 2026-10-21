# Auth0

[English](../../en/providers/auth0.md) | **繁體中文**

`Auth0OAuth2Options` 透過 OpenID Connect 以 Auth0 tenant 登入，結果的 provider 名稱是 `Auth0`。

## Options

- `Domain`、`ClientId` 與 `RedirectUri` 為必填。`Domain` 是 tenant 的主機名稱，例如 `your-tenant.auth0.com`，
  可以帶 `https://`，也可以不帶。所有 provider 共用的 options，見[所有 provider 共用的 Options](../guides/options.md)。
- 端點可以在 query 帶 Auth0 的參數，例如 `https://tenant.auth0.com/authorize?audience=...`。

## 桌面與主控台應用程式

| 應用程式類型 | 回呼網址 | port |
|--------------|----------|------|
| Native | `http://127.0.0.1:53682/callback` | 必須一致 |

這個登記方式已於 2026-09-14 實測，沒送 client secret 也換 token 成功
（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。

## .NET MAUI 應用程式

把自訂 scheme 登記為 Native 應用程式的 Allowed Callback URLs；在 iOS、Mac Catalyst 與 Android 上都登入成功。
Auth0 把自訂 scheme 標為無法驗證的回呼網址（non-verifiable callback URI），每次登入都請使用者確認。
見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

把回呼網址登記為 Regular Web Application 的 Allowed Callback URLs，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

- `Scopes` 含 `offline_access`、且應用程式設定允許時，Auth0 會發 refresh token。
- Auth0 的 `name` 為 JSON null 時，使用者名稱改用 `nickname`。

## 前後端分離的應用程式

`GetUserInfoAsync` 擲出 `NotSupportedException`：由前端送出 ID token，後端驗證。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
