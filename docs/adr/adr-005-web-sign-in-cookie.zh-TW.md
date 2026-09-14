# ADR-005：每次網頁登入各自保存在一個加密的 cookie

[English](adr-005-web-sign-in-cookie.md) | **繁體中文**

## 狀態

已採納（2026-09-14）

## 背景

從導向 provider 到回呼之間，網頁應用程式要保存這次登入的 state、PKCE code verifier 與回呼網址，還要記得它屬於哪個已註冊的 client。
state 把回呼綁定在發起登入的瀏覽器上（RFC 6749 第 10.12 節），code verifier 則必須保密到送出 token 請求為止（RFC 7636）。

Bee.OAuth2 在每個瀏覽器只用一個 cookie 保存 state，把 code verifier 放在 session，client 名稱則放在 state 裡，
以環境變數 `OAUTH2_STATE_KEY` 的金鑰與套件自帶的 AES-CBC-HMAC 程式加密（ADR-001）。這個設計：

- 需要 session，伺服器超過一台時還要有共用的 session 儲存；
- 需要一把跟應用程式分開部署的金鑰；
- 每個瀏覽器只能有一個進行中的登入，在兩個分頁同時登入會互相覆蓋；
- 套件必須自己維護加密程式碼。

## 決策

- `OAuth2Client.CreateAuthorizationRequest` 每次登入都產生新的隨機 state；使用 PKCE 時，也產生新的 code verifier。
- 網頁 manager 把 client 名稱、state、code verifier、回呼網址與登入開始的時間，放在一個專屬的 cookie 裡，cookie 名稱以 state 結尾。
- cookie 的值以平台的 data protection 保護：ASP.NET Core 用 data protection，System.Web 用 `MachineKey.Protect`。兩者都會加密並驗證內容。
- cookie 屬性：
  - 名稱以 `__Host-` 開頭，瀏覽器只在 cookie 為 Secure、路徑為 `/`、沒有指定網域時才接受。
  - HTTP-only，且 `SameSite=Lax`：provider 以最上層的 GET 請求導回，會帶著 Lax 的 cookie。
  - 有效期限 10 分鐘。ASP.NET Core 另外把 cookie 標記為必要，避免 cookie 同意政策把它擋掉。
- 回呼時依回傳的 state 找到 cookie、在回應中刪除它、拒絕超過 10 分鐘前開始的登入，再把值交給 `OAuth2Client.CompleteAuthorizationAsync`；
  後者在使用其他值之前，會先比對 state。
- cookie 的格式只在 `src/Shared/PendingAuthorizationCookie.cs` 定義一次，由兩個網頁套件一起編譯。
  網頁套件不透過 InternalsVisibleTo 使用核心套件的 internal 成員，因為它們接受核心套件任何較新的版本。

ASP.NET Core 內建的 OAuth handler 也依循同樣的原則：以 data protection 保護每次登入的資料，並綁定一個專屬的 correlation cookie。

## 影響

- 不再需要 session 與 `OAUTH2_STATE_KEY`。AES-CBC-HMAC 程式碼已移除，ADR-001 中位元組相容的 state 格式也就不再有用途。
- 所有可能收到回呼的伺服器，在 ASP.NET Core 上要共用 data protection 的金鑰環，在 System.Web 上要使用相同的 machine key。
- 登入必須在 10 分鐘內完成。升級到這個設計之前已開始的登入，升級後無法完成，使用者要重新登入。
- 每個沒有完成的登入都會留下一個 cookie，最多 10 分鐘；在這段時間內反覆開始登入卻不完成的瀏覽器，送出的請求會比較大。
- 登入的起點與回呼頁面都必須走 HTTPS。
- `tests/Polhem.OAuth2.UnitTests/AspNetCoreOAuth2ManagerTests.cs`、`AspNetOAuth2ManagerTests.cs`（在 .NET Framework 上執行）
  與 `PendingAuthorizationCookieTests.cs`，涵蓋 cookie 的屬性、同時進行多個登入、cookie 遺失或遭竄改、逾期，以及回呼時刪除 cookie。
