# GitHub

[English](../../en/providers/github.md) | **繁體中文**

`GitHubOAuth2Options` 透過 GitHub App 或 OAuth App 以 GitHub 帳號登入，結果的 provider 名稱是 `GitHub`。

## Options

- `ClientId` 與 `RedirectUri` 為必填。`ClientSecret` 也要設定：GitHub 每次換 token 都要求 client secret。所有 provider 共用的
  options，見[所有 provider 共用的 Options](../guides/options.md)。
- options 要求的 scope 為 `read:user user:email`。GitHub App 不看 scope，權限取自 App 的設定：請給它唯讀的
  Email addresses 權限，否則使用者不公開 email 時，使用者資訊裡就沒有 email。

## 桌面與主控台應用程式

| 後台登記 | 回呼網址 | port |
|----------|----------|------|
| GitHub App 或 OAuth App 的 callback URL，登記為 `http://127.0.0.1/callback` | `http://127.0.0.1:0/callback` | 任意 port 都被接受。host 必須一致：`localhost` 沒有導回 |

- 這個登記方式已於 2026-10-15 實測（[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)）。
- `LoopbackOAuth2Client` 會把 client secret 送給 GitHub；不送的話，token 端點回應 `incorrect_client_credentials`。

## .NET MAUI 應用程式

應用程式只能經由後端中轉登入 GitHub，`AppOAuth2Client` 收到 `GitHubOAuth2Options` 時會擲出 `ArgumentException`。GitHub 有導回應用程式的自訂 scheme，但不帶 client secret 換 token 被拒，
而應用程式無法保密 client secret。見[.NET MAUI 應用程式](../guides/maui.md#經由後端中轉登入)，以及
[ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md) 的實測。

## 網頁應用程式

在 GitHub App 或 OAuth App 的設定登記回呼網址，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

- GitHub App 開啟使用者 token 到期時會發 refresh token，新建的 GitHub App 除非關閉，預設就是開啟。OAuth App 不發 refresh token。
- `UserId` 是數字帳號 ID，使用者改名後仍不變；login 則會變，還可能被其他帳號取走。
- 使用者不公開 email 時，函式庫會從 `/user/emails` 讀取主要且已驗證的 email；這個請求失敗時 `Email` 為 null，
  登入仍然成功。

## 錯誤

GitHub 的 token 端點有些錯誤以狀態碼 200 回傳，例如 `bad_verification_code`，一樣會成為 `OAuth2Exception`。
見[結果、token 與錯誤](../guides/results-and-errors.md)。

## 前後端分離的應用程式

GitHub 不發標準 ID token，所以由前端送出 access token，後端呼叫 `GetUserInfoAsync`。options 需要 `ClientSecret`，
前端也要和後端用同一個 GitHub 應用程式。GitHub 拒絕 client 憑證時會往外拋。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
