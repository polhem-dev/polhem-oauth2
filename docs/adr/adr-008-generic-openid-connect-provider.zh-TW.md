# ADR-008：以一個 options 型別登入任何 OpenID Connect provider

[English](adr-008-generic-openid-connect-provider.md) | **繁體中文**

## 狀態

已採納（2026-10-20）。2026-10-20 於 pull request [#23](https://github.com/polhem-dev/polhem-oauth2/pull/23) 與
[#24](https://github.com/polhem-dev/polhem-oauth2/pull/24) 實作，並於 2026-10-20 以 Keycloak、GitLab.com、Google 與 Okta 實測，
記錄在「測試結果」。專用的 OpenID Connect provider 於 2026-10-20 在
[#26](https://github.com/polhem-dev/polhem-oauth2/pull/26) 加上 ID token 檢查，並以 Google、Auth0、Okta 與 Microsoft Entra ID 實測。

## 背景

Keycloak、GitLab、AWS Cognito、Authentik、Zitadel 等許多身分提供者都遵循 OpenID Connect，函式庫卻無法登入它們。
`OAuth2Options` 只能在套件內衍生，而替每一家各寫一個 provider 並不划算，因為它們之間只差在端點。Auth0 與 Okta 已經說明了這點：
兩者的 provider 對應的是相同的標準 claim，差別只在端點如何由網域組出來。

OpenID Connect 也允許 provider 不提供使用者資訊端點（OpenID Connect Core 1.0 第 5.3 節），Apple 就沒有。這時使用者只在 token
回應的 ID token 裡。

## 決策

- `OidcOAuth2Options` 登入任何 OpenID Connect provider。端點可以手動設定，也可以從 discovery 文件讀取。預設 scope 是
  `openid profile email`，provider 對應 `sub`、名稱（依序為 `name`、名與姓、`preferred_username`、`nickname`）與 `email`。
- `ProviderName` 必填，沒有預設值。使用者以 provider 名稱加上使用者 ID 識別，而兩家 provider 可能把同一個 `sub` 發給不同的人。
  若有共用的預設名稱，應用程式一旦註冊第二家 provider 卻沒有命名，這些使用者就會被合併。
- **options 維持同步，discovery 是非同步的工廠方法**：`OidcOAuth2Options.FromDiscoveryAsync(authority)` 回傳 options，
  由應用程式補完後照常交給 client。client 在建立時就檢查 options，若改成第一次登入時才抓文件，檢查也只能跟著延後，
  而且每個 client 的第一次登入都會多一個請求。應用程式在啟動時呼叫一次並保留結果；函式庫不快取，也不提供非同步的
  相依性注入註冊方法。
- discovery 在回傳 options 之前會檢查文件：
  - `issuer` 必須等於 authority（OpenID Connect Discovery 1.0 第 4.3 節），以 ordinal 比對，但兩邊各忽略一個結尾斜線。
    Auth0 的 issuer 有結尾斜線，其他實測過的 provider 沒有。`Issuer` 保留文件裡的原值，因為 ID token 帶的就是這個值。
  - 端點與其他 options 型別經過相同的檢查：不含 fragment 的絕對 https URI。
  - 文件列出 `response_types_supported` 時必須包含 `code`，列出 `code_challenge_methods_supported` 時必須包含 `S256`。
    沒有這些清單的文件照常接受。
  - `token_endpoint_auth_methods_supported` 列出 `client_secret_post` 時，`ClientAuthentication` 為 `ClientSecretPost`，
    否則為規格的預設值 `ClientSecretBasic`。
  - 文件上限 1 MiB，`HttpClient.Timeout` 同時涵蓋標頭與內容。
  - 失敗依 ADR-003 分類：請求失敗、非成功的狀態碼或文件過大為 `HttpRequestException`，內容不是 JSON 物件為 `JsonException`，
    文件描述的 provider 無法使用為 `OAuth2Exception`。
- `UserInfoEndpoint` 可以留空。這時使用者就是 ID token 的 payload，並且必須設定 `Issuer` 與 `openid` scope。
- token 端點回傳的每個 ID token 使用前都會檢查：audience 必須包含 client ID；authorized party（`azp`）必須是 client ID，
  token 有其他 audience 時也必須有 `azp`；設定了 `Issuer` 時 issuer 必須相等；token 必須有 `sub`。有使用者資訊端點時，
  它回傳的 `sub` 必須等於 ID token 的 `sub`（OpenID Connect Core 1.0 第 5.3.2 節）。未通過的 token 擲出 `OAuth2Exception`，
  登入因此失敗。
- 不檢查 ID token 的簽章與有效期限。token 是經 TLS 直接從 token 端點取得的，OpenID Connect Core 1.0 第 3.1.3.7 節第 6 項接受以此
  取代簽章檢查。不支援加密的 ID token。
- 這些檢查是共用的程式碼：LINE 用它從 ID token 讀 email，Apple 也可以使用。LINE 的行為不變：不檢查 issuer，讀不了的 ID token
  只是少了 email，不會讓登入失敗。
- 專用的 OpenID Connect provider，也就是 Google、Auth0、Okta 與 Microsoft Entra ID，對登入取得的 ID token 做相同的檢查，並比對它
  與使用者資訊的 `sub`。沒有要求 `openid`、token 回應沒有 ID token 時照舊，不檢查。只有單憑 options 就能得知 issuer 時才檢查 issuer：
  - Google：`https://accounts.google.com` 或 `accounts.google.com`，即 Google 文件列出的兩個值。
  - Auth0：`https://<Domain>/`。Auth0 以請求所用的網域加上結尾斜線作為 issuer，而所有端點都由 `Domain` 組成，所以 custom domain
    也適用。
  - Okta：不檢查。授權伺服器的 issuer mode 決定 token 帶的是 Okta 網域還是 custom domain，因此 issuer 可能與 `Domain` 不同。
  - Microsoft Entra ID：不檢查。issuer 帶的是使用者所屬的 tenant，`common`、`organizations` 與 `consumers` 都無法事先確定。

  四家都檢查 audience。通用與專用的 provider 共用一個基底類別處理這項檢查與標準 claim 的對應；各家只提供名稱沒有值時改用的欄位。
- 對這個 options，`OAuth2Client.GetUserInfoAsync(accessToken)` 擲出 `NotSupportedException`，與 Okta 相同：OpenID Connect 的前端
  以 ID token 證明是誰登入（ADR-007）。

## 範圍外

- 驗證前端送來的 ID token。ASP.NET Core 的 JwtBearer 驗證與 `Microsoft.IdentityModel.Protocols.OpenIdConnect` 會處理，
  其中包含會輪替的簽章金鑰，函式庫不應重做。
- `nonce`（由 PKCE 取代，RFC 9700 第 2.1.1 節）、動態 client 註冊、implicit 與 hybrid flow，以及授權回應的 `iss` 參數（RFC 9207）。
- 把 Auth0 與 Okta 改用通用的 provider。它們的 options 保留 `Domain` 與 `AuthorizationServerId` 的便利性。

## 影響

- Keycloak、GitLab 與其他 OpenID Connect provider 可以用網頁、loopback 與應用程式的 client 登入，前提是 provider 接受它們的
  回呼網址。
- Google 的桌面 client 沒有 client secret 時，Google 會拒絕換 token，而這個 options 的 public client 不送 secret。請改用會送出
  secret 的 `GoogleOAuth2Options`。
- discovery 文件的 issuer 是範本的 provider，例如 Microsoft Entra ID 的 `common` tenant，無法以 discovery 讀取。
- 以 Google、Auth0、Okta 或 Microsoft Entra ID 登入時，使用者資訊的 subject 與 ID token 不同、ID token 無法讀取或發給其他 client，
  原本能成功的登入現在會失敗。「測試結果」裡的登入都通過了這項檢查。
- `tests/Polhem.OAuth2.UnitTests/IdTokenTests.cs`、`OidcOAuth2ProviderTests.cs`、`OpenIdConnectProviderTests.cs` 與
  `OidcDiscoveryTests.cs` 涵蓋各項檢查。
  fake provider 在 `/oidc` 下提供一個有 discovery 文件、沒有使用者資訊端點的 OpenID Connect provider，供 device tests 登入。

## 測試結果

2026-10-20 各 discovery 文件的 issuer 如下，結尾斜線的規則依此決定：

- Google、GitLab.com、Okta（custom 與 org 授權伺服器）與 Keycloak 26.4：沒有結尾斜線。
- Auth0：有結尾斜線。
- Microsoft Entra ID 的 `common` tenant：含 `{tenantid}` 的範本。

2026-10-20 以 `FromDiscoveryAsync` 讀取 options，並以真實帳號登入：

- Keycloak 26.4（Docker，使用測試用憑證機構簽發的憑證）：網頁 client 以 `client_secret_basic` 與 `client_secret_post` 各自登入，
  有無使用者資訊端點都成功，refresh 也成功；secret 錯誤時回 `unauthorized_client`。client 註冊不帶 port 的
  `http://127.0.0.1/callback` 時，loopback client 以隨機 port 登入成功；註冊 `localhost` 則不會匹配 `127.0.0.1`。應用程式 client
  以 custom scheme 回呼網址登入成功，未註冊的 scheme 被 Keycloak 拒絕。
- GitLab.com（非 confidential 的 application，scope 為 `openid profile email`）：回呼網址註冊不帶 port 的
  `http://127.0.0.1/callback`，loopback client 以隨機 port、PKCE、不送 secret 登入並 refresh 成功。有無使用者資訊端點取得的
  使用者相同。`sub` 是以字串表示的數字帳號 ID，ID token 帶有 `name`、`email`、`preferred_username` 與 `nickname`。
  使用者資訊另有 `sub_legacy` 欄位，函式庫不使用。
- Google 與 Okta：通用 options 取得的使用者（有無使用者資訊端點皆然），其使用者 ID、名稱與 email 與 `GoogleOAuth2Options`、
  `OktaOAuth2Options` 相同。以通用 options 的 public client 使用 Google 桌面 client 時，Google 回 `invalid_request`；改成
  confidential client 則成功。

[run 39354330719](https://github.com/polhem-dev/polhem-oauth2/actions/runs/39354330719) 的 device tests 登入 fake provider 的
OpenID Connect 模式：iOS、Mac Catalyst 與 Android 直連登入，Windows 經由 loopback client 登入。

2026-10-20 在加上 ID token 檢查後，以 loopback client 與真實帳號登入各專用 provider。每次登入都成功，ID token 都以單一 audience
發給這個 client，使用者資訊的 `sub` 都等於 ID token 的 `sub`：

- Google：issuer 是 `https://accounts.google.com`，ID token 帶有等於 client ID 的 `azp`，`sub` 是數字。
- Auth0：issuer 是網域加上結尾斜線，`sub` 的形式是 `<provider>|<id>`。
- Okta，`default` 授權伺服器：issuer 是 `https://<domain>/oauth2/default`。
- Microsoft Entra ID，`common` tenant，個人帳號與工作帳號各一次：issuer 帶的是各帳號所屬的 tenant，個人帳號是 consumers tenant。
  兩次 Microsoft Graph 的使用者資訊都回傳與 ID token 相同的 pairwise `sub`；個人帳號的名與姓是 `givenname` 與 `familyname`，
  工作帳號則是 `given_name`、`family_name` 加上 `name`。
