# ADR-011：應用程式有後端時，由後端登入

[English](adr-011-back-end-signs-in.md) | **繁體中文**

## 狀態

已採納（2026-11-05），取代 ADR-007。2026-11-05 於 pull request
[#45](https://github.com/polhem-dev/polhem-oauth2/pull/45)（桌面中轉）、
[#46](https://github.com/polhem-dev/polhem-oauth2/pull/46)（移除 access token 查證）與
[#47](https://github.com/polhem-dev/polhem-oauth2/pull/47)（範例與 device tests）實作。測試記錄在「測試結果」。

## 背景

ADR-007 讓有後端的應用程式用三種方式告訴後端是誰登入，依優先順序：後端自己跑登入（網頁用 ADR-005，行動應用程式用
ADR-006）、前端送 ID token 由後端驗證、前端送 access token，由後端以 `OAuth2Client.GetUserInfoAsync(accessToken)` 向
provider 查證它發給哪個 client。

後兩種存在，是因為前端可能自己登入。桌面應用程式別無選擇：ADR-006 只中轉行動應用程式，所以有後端的桌面應用程式以
`LoopbackOAuth2Client` 登入後，只能把 token 交給後端。Facebook 與 GitHub 不發 ID token，只剩 access token 查證一途。

這項查證有其他方式沒有的代價。LINE 與 Facebook 的文件只把 token 放在 query，token 因此可能進到 HTTP client 的 log。每次查證都是
一個送往 provider 的請求，而 provider 會限制回應的數量。Facebook 與 GitHub 只能查證發給「發出查證的那個應用程式」的 token，
前端與後端必須共用同一個應用程式。而前端是 public client，卻持有 provider 的 token，GitHub 甚至連 client secret 都在前端。

常見做法也指向同一個方向。IETF 對瀏覽器端應用程式的最佳實務，建議業務應用程式與處理個資的應用程式採用 backend for
frontend，讓 token 留在伺服器。微軟 .NET MAUI 的 `WebAuthenticator` 文件，則因為應用程式保管不了 client secret，建議以網站後端
作為應用程式與 provider 之間的中間層。

## 決策

- 沒有自家後端的應用程式直接登入，並自己使用登入結果：桌面用 `LoopbackOAuth2Client`，行動裝置用 `AppOAuth2Client`。不受影響。
- 有後端的應用程式由後端登入，不分 provider，OpenID Connect provider 也一樣：
  - 網頁前端走 ADR-005 的網頁登入；
  - 行動應用程式走 ADR-006 的中轉；
  - 桌面應用程式，以及 .NET MAUI 應用程式的 Windows 平台，走同一個中轉，中轉現在接受 loopback 回呼網址。
- 前端不把 token 或使用者資訊交給後端當作登入者的身分證明。函式庫不驗證這類東西，文件也不再建議這麼做。
- `OAuth2Client.GetUserInfoAsync(accessToken)`、`OAuth2Options.AcceptedClientIds` 與各 provider 的 token 查證在 1.5.0 移除，
  不經過 `[Obsolete]`：ADR-010 允許 1.x 的 minor 版這麼做，它們在 2026-10-21 隨 1.4.0 發佈，而 polhem-dev 組織的 repo 都沒有呼叫它們。

### 桌面中轉

- Polhem.OAuth2.AspNetCore 的中轉接受 loopback 應用程式回呼網址：`http`、位址 `127.0.0.1` 或 `[::1]`，以及一段 path，不帶
  query、user information 或 fragment。它必須是 `Uri` 寫出的形式，中轉之後導向的網址才會正是它比對過的那一個。`localhost`
  與這兩個位址的其他寫法都會被拒絕，與 ADR-004 相同。
- 不帶 port 登記的 loopback 網址，比對時接受同一位址與 path 的任意 port，如 RFC 8252 第 7.3 節所述；帶 port 登記時只接受那個
  port。`OAuth2Options.IsAppRedirectUri` 不變，所以 `AppOAuth2Client` 仍然拒絕 http。
- 請求的 loopback 網址可以在最後帶 `?state=` 與 43 到 128 個 base64url 字元，query 裡不能有其他東西，中轉會把代碼接在它後面。
  瀏覽器裡任何網頁都能對 loopback 位址送出請求（ADR-004），沒有 state 的話，找到 port 的網頁可以用它自己的代碼結束這次登入。
  那個代碼因為 verifier 不符而兌換不了，但使用者的登入會因此失敗。
- Polhem.OAuth2 的 `LoopbackRelayClient` 產生 PKCE 的 code verifier、帶實際 port 與 state 的應用程式回呼網址，向應用程式取得
  後端的登入網址並開啟，等待帶著該 state 的導回，再回傳代碼與 verifier。它與 `LoopbackOAuth2Client` 共用監聽程式。預期中的
  失敗，也就是錯誤導回、沒有代碼的導回、逾時與取消，都是失敗結果（ADR-003）。
- 應用程式自己把代碼與 verifier 送給後端，後端以 `RedeemAppCodeAsync` 兌換。登入網址與兌換端點是應用程式與自家後端之間的約定，
  與行動應用程式相同，函式庫不定義它們。

## 範圍外

- Polhem.OAuth2.AspNet（System.Web）的中轉。其他框架的後端可以用 `OAuth2Client` 依相同步驟實作。
- 把 token 直接交給裝置的原生登入元件，例如 iOS 的 Sign in with Apple 按鈕，ADR-009 已經排除。
- 把 provider 的 token 交給應用程式。必須自己呼叫 provider API 的應用程式，請直接登入，或由後端代為呼叫 API。

## 影響

- 一條規則涵蓋所有有後端的應用程式，provider 的 token 與 client secret 不會到前端。有後端的桌面應用程式也能用 Sign in with Apple
  與 GitHub 登入，而且不必隨程式附上 client secret。
- 使用者電腦上任何程式都能監聽 loopback port。不帶 port 登記時，這樣的程式可以發起一次導回自己 port 的中轉登入，再用自己的
  verifier 兌換代碼；RFC 8252 第 8.3 節對 loopback 回呼接受這一點。`AppRedirectUris` 的文件寫明這點，並建議能固定使用同一個
  port 的應用程式帶 port 登記。
- 呼叫 `GetUserInfoAsync(accessToken)` 的後端會無法編譯。1.5.0 的發佈說明描述遷移方式：把登入移到後端。
- `tests/Polhem.OAuth2.UnitTests/AspNetCoreAppRelayLoopbackTests.cs` 涵蓋登記與比對規則，`LoopbackRelayClientTests.cs` 涵蓋
  client，`AspNetCoreLoopbackRelayTests.cs` 涵蓋從 client 經真實中轉到兌換出使用者的一次登入。fake provider 登記了 loopback
  網址，並為它的 OpenID Connect 模式註冊了中轉 client。

## 測試結果

2026-11-05：

- 單元測試在 .NET 10 上全部通過，包括一次由 `LoopbackRelayClient` 經 `OAuth2Manager` 中轉的登入。
- 在本機執行 fake provider，追蹤它的 Auth0 與 OpenID Connect 模式經中轉導回 `http://127.0.0.1:53999/callback?state=...` 的登入。
  兩者都回傳 `?state=...&code=...`，代碼都兌換出 fake 使用者。在此之前，中轉沒有以 `OidcOAuth2Options` 測試過。
- loopback 規則的對抗式審查試過位址的其他寫法（`127.1`、`127.0.0.2`、`[0:0:0:0:0:0:0:1]`、`[::ffff:127.0.0.1]`、結尾的點）、
  大寫的 scheme、port 的前導零、port 0 與 65536、反斜線、編碼過的斜線，以及在 state query 裡夾帶第二個 `code`。中轉全部拒絕。
- device tests 的 [run 42029623202](https://github.com/polhem-dev/polhem-oauth2/actions/runs/42029623202) 在 Windows、iOS、
  Mac Catalyst 與 Android 上通過。Windows 以 `LoopbackRelayClient` 經中轉登入 fake provider；Mac Catalyst 由應用程式經中轉登入
  OpenID Connect 模式。
- 在 macOS 上，OAuthConsole sample 加上 `--back-end`，經 OAuthAspNetCore sample 以真實帳號登入，後端以不帶 port 的
  `http://127.0.0.1/callback` 登記：
  - GitHub，使用後端的網頁 client：兌換出的使用者有數字帳號 ID、名稱與 email。client secret 留在後端。
  - Facebook：兌換出的使用者有數字 ID、名稱與 email。

  這兩家都不發 ID token，有後端的前端原本只能用本決策移除的 access token 查證。
