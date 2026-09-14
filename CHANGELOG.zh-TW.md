# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.OAuth2、Polhem.OAuth2.AspNet 與 Polhem.OAuth2.AspNetCore 的重要變更。格式依循
[Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循[語意化版本](https://semver.org/lang/zh-TW/)。

## [1.0.0] - 2026-09-14

以 Polhem 名義發佈的第一版，延續自 [Bee.OAuth2](https://github.com/jeff377/bee-oauth2)；以下變更都是相對於 Bee.OAuth2 的最後一版。
應用程式如何遷移，見 README 的[從 Bee.OAuth2 遷移](README.zh-TW.md#從-beeoauth2-遷移)。

### 新增

- `LoopbackOAuth2Client`：桌面與主控台應用程式透過系統瀏覽器與 loopback 回呼登入，一律使用 PKCE，可在 Windows、macOS、Linux 上使用。
  見 [ADR-004](docs/adr/adr-004-system-browser-loopback.zh-TW.md)。
- Okta provider（`OktaOAuth2Options`）。
- Polhem.OAuth2 新增 net10.0 目標框架，這個目標沒有任何套件相依。
- 公開 API 加上 nullable 參考型別標註。

### 變更

- 套件 ID 與命名空間由 `Bee.OAuth2` 改名為 `Polhem.OAuth2`。
- Polhem.OAuth2.AspNetCore 的目標框架由 net8.0 改為 net10.0，並改參照 ASP.NET Core 共用框架，不再參照 `Microsoft.AspNetCore.Http.Abstractions` 套件。
- JSON 改用 System.Text.Json 解析，套件不再相依 `Bee.Base` 與 `Newtonsoft.Json`。見 [ADR-001](docs/adr/adr-001-drop-bee-base.zh-TW.md)。
- 使用者資訊裡值為 JSON null 的欄位，現在是 `null` 而不是空字串，備援欄位因此會生效。
- `AuthorizationResult.Exception` 只收納 OAuth2 登入預期會發生的失敗，其他例外會往外拋。例外訊息會帶 HTTP 狀態碼，但不再帶回應內容。
  見 [ADR-003](docs/adr/adr-003-exception-semantics.zh-TW.md)。

### 移除

- `Bee.OAuth2.WinForms` 與 `Bee.OAuth2.Desktop`，以及內嵌 WebView2 的登入視窗。請改用 `LoopbackOAuth2Client`。

### 安全性

- 被截斷、格式錯誤或遭竄改的 state 會在解密之前就以 `CryptographicException` 拒絕。加密後的 state 格式不變，既有的 `OAUTH2_STATE_KEY` 可以繼續使用。
- 回傳的 state 或儲存的 state 任一為空時，`ValidateState` 一律判定失敗。先前在沒有儲存 state 時，不帶 state 的請求會通過。

[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
