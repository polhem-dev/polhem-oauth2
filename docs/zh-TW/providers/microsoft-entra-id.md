# Microsoft Entra ID

[English](../../en/providers/microsoft-entra-id.md) | **繁體中文**

`AzureOAuth2Options` 透過 Microsoft Entra ID 與 OpenID Connect，以 Microsoft 的公司、學校或個人帳號登入，
結果的 provider 名稱是 `Azure`。

## Options

- `ClientId` 與 `RedirectUri` 為必填。所有 provider 共用的 options，
  見[所有 provider 共用的 Options](../guides/options.md)。client ID 以 GUID 的方式與 ID token 比對，大小寫不影響。
- Microsoft Entra ID 預設使用 `common` tenant。只註冊在單一 tenant 的應用程式，要把 `Tenant` 設成該 tenant 的 ID
  或網域名稱。

## 桌面與主控台應用程式

| 平台 | 回呼網址 | port |
|------|----------|------|
| Mobile and desktop applications，登記為 `http://localhost` | `http://localhost:0` | 被忽略 |

- 這個登記方式已於 2026-09-14 實測，沒送 client secret 也換 token 成功
  （[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。
- 只在內嵌瀏覽器裡才能運作的網址，例如 `https://login.microsoftonline.com/common/oauth2/nativeclient`，
  無法搭配系統瀏覽器使用。

## .NET MAUI 應用程式

- iOS 與 Mac Catalyst 登記 iOS/macOS 平台，回呼網址為 `msauth.<bundle id>://auth`；或在「行動應用程式與桌面應用程式」下
  登記自訂 URI。
- Android 登記 Android 平台，填套件名稱與簽章雜湊，回呼網址為 `msauth://<package>/<signature hash>`；或登記自訂 URI。
- Microsoft Entra ID 只能登記兩個斜線的自訂 scheme，也就是 `<scheme>://<path>`。
- Microsoft 要求個人帳號確認應用程式來自可信任的來源。

見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

把回呼網址登記為 web 平台的 redirect URI，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

- `Scopes` 含 `offline_access` 時，Microsoft Entra ID 會發 refresh token。
- `UserId` 是 `sub` claim，同一個使用者登入不同的應用程式時，這個值也不同。需要跨應用程式一致的識別時，
  `Token.IdToken` 裡的 `oid` claim 在每個應用程式都相同；使用前要先驗證這個 token。

## 前後端分離的應用程式

`GetUserInfoAsync` 擲出 `NotSupportedException`：由前端送出 ID token，後端驗證。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
