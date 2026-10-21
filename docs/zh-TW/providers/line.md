# LINE

[English](../../en/providers/line.md) | **繁體中文**

`LineOAuth2Options` 透過 LINE Login channel 以 LINE 帳號登入，結果的 provider 名稱是 `LINE`。

## Options

`ClientId` 與 `RedirectUri` 為必填，桌面與網頁 client 另外設定 `ClientSecret`。所有 provider 共用的 options，
見[所有 provider 共用的 Options](../guides/options.md)。

## 桌面與主控台應用程式

| 後台登記 | 回呼網址 | port |
|----------|----------|------|
| LINE Login channel 的 Callback URL | `http://localhost:53682/callback` | 必須一致 |

- 這個登記方式已於 2026-09-14 實測（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。
- channel 必須包含 Mobile app 類型：只有 Web app 類型的 channel 要求 client secret，而公開 client 不會送。
- 有設定 client secret 時，`LoopbackOAuth2Client` 會把它送給 LINE，因為 2026-09-26 的實測中，只供網頁應用程式使用的
  channel 沒有 secret 時拒絕 refresh。

## .NET MAUI 應用程式

- iOS 與 Mac Catalyst 以回呼網址 `line3rdp.<bundle id>://auth` 直連登入，後台登記為 channel 的 iOS bundle ID。
- Android 經由後端中轉登入。LINE 拒絕導回 Android 應用程式（「Invalid redirect custom scheme」）。
- LINE 在所有測過的平台上，都拒絕一般的自訂 scheme，例如 `com.example.app:/oauth2redirect`。

見[.NET MAUI 應用程式](../guides/maui.md)，以及 [ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

把回呼網址登記為 channel 的 Callback URL，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。一個 channel 可以登記兩個回呼網址，
同時服務桌面與網頁 client。

## Token 與使用者

- LINE 一律會發 refresh token。
- `UserId` 在同一個 provider 底下的每個 channel 都相同。
- LINE 只在 ID token 裡提供 email，而且要 channel 有權限讀取、使用者也同意才會有。函式庫從 token 端點回傳的 ID token
  讀出 email，會檢查這個 token 是發給這個 client 的，但不檢查簽章。

## 前後端分離的應用程式

由前端送出 ID token，後端驗證。`GetUserInfoAsync` 也接受 LINE 的 access token；查證時 token 放在請求的 query 裡。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
