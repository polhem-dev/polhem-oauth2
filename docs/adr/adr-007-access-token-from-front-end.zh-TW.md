# ADR-007：前端送來的 access token，要等 provider 說出它的 client 才回傳使用者

[English](adr-007-access-token-from-front-end.md) | **繁體中文**

## 狀態

已採納（2026-10-15）。2026-10-15 於 pull request
[#9](https://github.com/polhem-dev/polhem-oauth2/pull/9)、[#10](https://github.com/polhem-dev/polhem-oauth2/pull/10)、
[#11](https://github.com/polhem-dev/polhem-oauth2/pull/11) 與 [#18](https://github.com/polhem-dev/polhem-oauth2/pull/18) 實作。
各項查證於 2026-10-15 以無效的 token 實測，2026-10-16 以真實帳號與其他 client 的 token 實測，記錄在「測試結果」。

## 背景

有自己帳號系統的後端，可能會收到前端（.NET MAUI、桌面或單頁應用程式）自己登入 provider 後取得的 access token。
後端需要知道這個 token 背後的使用者，才能對應到自己的帳號並發出自己的 session。

只拿 token 呼叫使用者資訊端點是不夠的。使用者登入同一個 provider 的其他應用程式時，那個應用程式也會拿到這位使用者的有效 token。
它可以把 token 送給後端，而使用者資訊端點一樣回傳這位使用者，於是它就以這位使用者的身分登入了後端。這就是 token substitution。
token 是發給哪個 client 的，只有 provider 知道。

另外兩種取得使用者的方式不會碰到這個問題：

- 後端自己跑登入流程：網頁應用程式用 ADR-005，行動應用程式用 ADR-006 的後端中轉。token 從不離開後端。
- 前端送出 OpenID Connect 登入取得的 ID token。它的 audience 是前端的 client ID，後端在本機驗證簽章、issuer、有效期限與 audience。

不是每個 provider 都發 ID token。Facebook Login 除了 iOS 的 Limited Login 之外不發，GitHub 也沒有給使用者登入用的 OpenID Connect。

## 決策

- 取得使用者的方式，依優先順序：
  1. 後端自己跑登入流程（ADR-005、ADR-006）：後端只需要知道誰登入時使用。
  2. 前端送 ID token，由後端驗證：前端自己登入，而且 provider 支援 OpenID Connect 時使用。
  3. 前端送 access token，後端呼叫 `OAuth2Client.GetUserInfoAsync(accessToken)`：provider 沒有標準 ID token 時使用。
- `GetUserInfoAsync` 在取得使用者資訊之前，先向 provider 詢問 token 是發給哪個 client 的。provider 回報的每一個 client ID，
  都必須是 `OAuth2Options.ClientId` 或 `OAuth2Options.AcceptedClientIds` 之一，以 ordinal 比對。清單為空時只接受 `ClientId`。
  這項查證沒有跳過的選項，因為跳過它的後端會接受任何應用程式的 token。
- 各 provider 的查證方式：

  | Provider | 查證 | 接受的條件 |
  |----------|------|-----------|
  | Google | `POST https://oauth2.googleapis.com/tokeninfo`，token 放在 form body | `aud` 被接受，`azp` 存在時也要被接受。同一個 Google Cloud 專案的 client 可以互相取得 token，所以兩者可能不同 |
  | LINE | `GET https://api.line.me/oauth2/v2.1/verify`，token 放在 query | `client_id` 被接受 |
  | Facebook | `GET https://graph.facebook.com/<版本>/debug_token`，token 放在 query，app access token 放在 `Authorization` 標頭 | `data.is_valid` 為 `true`、`data.type` 為 `USER`，且 `data.app_id` 被接受 |
  | GitHub | `POST <API>/applications/<client ID>/token`，client ID 與 secret 放在 Basic 標頭，token 放在 JSON body。`<API>` 是使用者資訊端點去掉結尾的 `/user` | GitHub 在 `ClientId` 底下找得到這個 token，而且回應寫的是 `ClientId` |

- Microsoft Entra ID、Auth0 與 Okta 不支援。`GetUserInfoAsync` 擲出 `NotSupportedException`，訊息指向 ID token，
  或指向發給後端自己 API、以 JSON Web Token 驗證的 access token：
  - Entra ID：微軟要求應用程式把 Microsoft Graph 的 access token 當成不透明的，也沒有端點能說出它的 client。
  - Auth0：沒有 audience 時（`Auth0OAuth2Options` 不會送），Auth0 發的是只有 `/userinfo` 接受的 opaque token，而且沒有 introspection 端點。
  - Okta：introspection（RFC 7662）可以用後端的憑證說出 client，但一個 client 能否 introspect 另一個 client 的 token，
    官方文件寫得模糊；而且 ID token 不必為每個 token 發請求就能回答這個問題。
- Facebook 與 GitHub 需要 `ClientSecret`，沒有設定時 `GetUserInfoAsync` 擲出 `InvalidOperationException`。
  兩者都只能查證發給「以其憑證發出查證請求的那個應用程式」的 token，所以 `AcceptedClientIds` 在這兩家不會多接受任何 client，
  前端與後端要用同一個 Facebook app 或 GitHub 應用程式。Facebook 對其他 app 的 token 回錯誤 100，GitHub 則找不到它。
- 失敗依循 ADR-003。失敗結果收納：provider 拒絕 token、沒有說出 client、說出的 client 不被接受，以及 token 長於函式庫接受上限時的
  `OAuth2Exception`；`HttpRequestException`；請求逾時時的 `TaskCanceledException`；以及 `JsonException`。
  以下會擲出：token 為空時的 `ArgumentException`、沒有查證方式的 provider 的 `NotSupportedException`，以及缺少 secret、
  GitHub 拒絕憑證、或 GitHub 使用者資訊端點的路徑不是以 `/user` 結尾時的 `InvalidOperationException`。
- 函式庫自己產生的例外訊息不含 token 與請求 URI，由測試檢查。LINE 與 Facebook 的查證只有文件化 GET，所以 token 會放在 query；
  `GetUserInfoAsync` 的 remarks 說明如何讓它不出現在 HTTP client 的 log。
- 加密前後端之間的呼叫（例如 Polhem.JsonRpc 的 payload 套件）取代不了這項查證。加密證明的是呼叫端持有金鑰，
  而前端是 public client：它的金鑰可以從應用程式取出，或由任何人協商取得。token substitution 的攻擊者，正是能執行前端的人。

## 不在範圍內

- 驗證 ID token 或 JSON Web Token 格式的 access token。這是 JwtBearer 驗證這類函式庫的工作。
- token 在有效期間內被重放。後端在第一次呼叫後發出自己的 session，就可以不再收同一個 token；
  Polhem.JsonRpc 的 payload 套件也能在方法要求時拒絕重放的呼叫。
- 快取查證結果。

## 影響

- 後端可以接受 Google、LINE、Facebook 與 GitHub 的 access token，而不必信任前端。
- 不論 token 最後是否有效，查證都會發一個請求給 provider，而 provider 會限制回應的數量。remarks 請應用程式限制呼叫這個方法的端點被呼叫的頻率。
- `tests/Polhem.OAuth2.UnitTests/OAuth2ClientAccessTokenTests.cs` 涵蓋各 provider 接受與拒絕的 token、失敗的分類、不支援的 provider、
  `AcceptedClientIds` 的複製與驗證，以及 token 不會出現在例外訊息裡。

## 測試結果

2026-10-15 對每項查證送出無效的 token，需要時使用測試應用程式的憑證：

- Google：以 form body 送 token 的 POST 回 `invalid_token`；不帶 token 時回「Either access_token, id_token, or token_handle required」。端點會讀 body。
- LINE：GET 回 `invalid_request`。同一個路徑的 POST 是 ID token 驗證，回應要求 `id_token`，所以查證不能用 POST。
- Facebook：app access token 放在標頭時，App 自己的 token 回 `data.app_id`、`data.type` 為 `APP`、`data.is_valid` 為 `true`；
  無效的 token 以狀態碼 200 回 `data.is_valid` 為 `false` 與 `data.error`；錯誤的 app access token 回 401。
- GitHub：無效的 token 回 404，錯誤的 secret 回 401，body 沒有 `access_token` 回 422。
- Okta（決定不支援之前）：introspection 接受網頁應用程式的 Basic 憑證，對無效的 token 回 `{"active":false}`。

2026-10-16 由桌面前端以 `LoopbackOAuth2Client` 用真實帳號登入各 provider，再把 access token 交給 `GetUserInfoAsync`：

- Google：前端用桌面 client。tokeninfo 回傳 `aud` 與 `azp`，兩者都是桌面 client ID。`ClientId` 為該 client 的後端回傳了使用者。
  以同一個 Google Cloud 專案的網頁 client 為 `ClientId` 的後端，以「發給不被接受的 client」拒絕這個 token；
  `AcceptedClientIds` 列入桌面 client 後接受，只列 iOS client 時又拒絕。
- LINE：verify 回傳 `client_id`，也就是 channel ID，同一個 channel 的後端回傳了使用者。只有一個 channel 可用，所以另一個 client 以模擬進行：
  `ClientId` 不同的後端拒絕這個 token，`AcceptedClientIds` 列入該 channel 後接受。
- Facebook：後端設定了 `ClientSecret`。debug_token 回傳 `data.app_id`、`data.type` 為 `USER`、`data.is_valid` 為 `true`，後端回傳了使用者。
  同一位開發者另一個 app 的 user token（以 Graph API Explorer 產生）被拒絕：debug_token 以狀態碼 400 回錯誤 100，
  「The App_id in the input_token did not match the Viewing App」。`AcceptedClientIds` 列入那個 app 時後端一樣拒絕，
  所以 Facebook 和 GitHub 一樣只接受 `ClientId` 的 token。
- GitHub：前端與後端用同一個 GitHub App。查證回傳 `app.client_id`，也就是該 App 的 client ID，後端回傳了使用者。
  GitHub CLI（另一個 OAuth 應用程式）的有效 token 回 404 而被拒絕，`AcceptedClientIds` 列入 GitHub CLI 的 client ID 時也一樣。

所有失敗結果的訊息都不含 token。
