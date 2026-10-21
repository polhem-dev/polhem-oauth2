# 結果、token 與錯誤

[English](../../en/guides/results-and-errors.md) | **繁體中文**

- 成功的結果有 `ProviderName`、`UserInfo` 與 `Token`；失敗的結果有 `Exception`。
- `Token` 帶著 access token，以及 provider 有回傳時的 refresh token、ID token、有效期限與 scope。
  用 client 的 `RefreshTokenAsync` 取得新的 token。有些 provider 每次都會發新的 refresh token，所以要保留最新一次回應裡的那個。
- provider 發不發 refresh token 取決於 provider 與請求；各 provider 文件的「Token 與使用者」一節說明何時會發：
  [Google](../providers/google.md#token-與使用者)、[Facebook](../providers/facebook.md#token-與使用者)、
  [LINE](../providers/line.md#token-與使用者)、[Microsoft Entra ID](../providers/microsoft-entra-id.md#token-與使用者)、
  [Auth0](../providers/auth0.md#token-與使用者)、[Okta](../providers/okta.md#token-與使用者)、
  [GitHub](../providers/github.md#token-與使用者)、[Sign in with Apple](../providers/apple.md#token-與使用者)。
- `Exception` 只收納登入預期會發生的失敗，例如 HTTP 請求失敗、state 不相符、provider 回傳錯誤。
  設定或程式錯誤（例如 client 名稱沒有註冊）會往外拋。見 [ADR-003](../../adr/adr-003-exception-semantics.zh-TW.md)。
- provider 回傳的錯誤是 `OAuth2Exception`：`Error` 是錯誤代碼，例如 `access_denied`；`ErrorDescription` 是 provider 提供的說明。
  導回網址裡的錯誤，任何送連結給使用者的人都能設定，所以顯示前要先編碼。有些 provider 的 token 端點以自己的格式回報錯誤，
  一樣會成為 `OAuth2Exception`；[Facebook](../providers/facebook.md#錯誤) 與 [GitHub](../providers/github.md#錯誤) 的文件有說明。
