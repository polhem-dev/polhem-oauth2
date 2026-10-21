# 識別使用者

[English](../../en/guides/identifying-users.md) | **繁體中文**

- 以 provider 名稱加上 `UserInfo.UserId` 識別使用者，不要用 `Email`：email 可能變更，而且各 provider 是否驗證過 email 並不一致。
- provider 名稱是 `AuthorizationResult.ProviderName`：`Google`、`Facebook`、`LINE`、`Azure`（Microsoft Entra ID）、`Auth0`、`Okta`、`GitHub`、
  `Apple`，或 `OidcOAuth2Options` 的 `ProviderName`。
  它與 client 註冊時使用的名稱無關。
- `UserId` 是什麼、email 從哪裡來，各 provider 不同；各 provider 文件的「Token 與使用者」一節有說明。例如 Microsoft Entra ID
  與 Facebook 的 `UserId`，在使用者登入的每個應用程式都不同。
- 函式庫會讀取自己這次登入、直接從 token 端點取得的 ID token。Sign in with Apple，以及沒有使用者資訊端點的
  `OidcOAuth2Options`，從它取得使用者；LINE 則從它取得 email。Google、Auth0、Okta、Microsoft Entra ID，以及有使用者資訊端點的
  `OidcOAuth2Options`，在回應帶有 ID token 時，使用者資訊的 subject 必須與 ID token 相同。
- 這些檢查都要求 ID token 是發給這個 client 的。Google、Auth0、Sign in with Apple，以及設定了 `Issuer` 的 `OidcOAuth2Options`
  會檢查 issuer；Okta 與 Microsoft Entra ID 的 issuer 不由 options 固定，所以不檢查，LINE 也不檢查。簽章不檢查，
  因為經 TLS 直接從 token 端點取得的 token 不需要。各項檢查記錄在 [ADR-008](../../adr/adr-008-generic-openid-connect-provider.zh-TW.md)。
- 除此之外，`Token.IdToken` 是 provider 原樣回傳的內容，要在其他地方（例如另一個後端）依賴其中的 claim 前，請先自行驗證。
