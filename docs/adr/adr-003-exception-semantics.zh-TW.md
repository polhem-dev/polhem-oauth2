# ADR-003：只有預期的 OAuth2 失敗會轉成失敗結果

[English](adr-003-exception-semantics.md) | **繁體中文**

## 狀態

已採納（2026-09-13）。2026-09-14 首發前修訂，涵蓋 `OAuth2Client` 與網頁登入的 cookie。

## 背景

完成登入時會回傳 `AuthorizationResult`。Bee.OAuth2 會攔下所有例外，放進失敗結果回傳。
結果是程式或設定上的錯誤（例如 client 沒有註冊、在 HTTP 請求之外呼叫）看起來跟一般的登入失敗一模一樣，
需要修正的問題很容易被忽略。

## 決策

- 協定層級的失敗擲出 `OAuth2Exception`：
  - provider 回傳的錯誤，無論在導回網址或來自 token 端點，錯誤代碼放在 `Error`，說明放在 `ErrorDescription`；
  - state 不存在或不相符，以及缺少授權碼或 PKCE code verifier；
  - token 回應裡沒有 access token，以及使用者資訊回應為空。
- 完成登入時，只把登入預期會發生的失敗轉成失敗結果：
  - `OAuth2Client.CompleteAuthorizationAsync`：`OAuth2Exception`、`HttpRequestException`、請求逾時時的 `TaskCanceledException`，以及 `JsonException`。
  - ASP.NET 與 ASP.NET Core 的 manager 另外還有：沒有 cookie 對應到 state 或登入太久以前開始時的 `OAuth2Exception`，
    以及 cookie 無法解密時的 `CryptographicException`。
  - `LoopbackOAuth2Client.SignInAsync` 另外還有：ADR-004 描述的失敗。
- 呼叫端的取消，會從 `OAuth2Client` 與網頁 manager 以 `OperationCanceledException` 往外拋；ASP.NET Core 在請求中斷時也一樣。
  取消代表沒有人在等這個結果，而不是登入失敗。loopback client 則如 ADR-004 所述，把取消轉成失敗結果。
- 其他例外一律往外拋。設定或程式錯誤擲出 `InvalidOperationException`：client 名稱沒有註冊、登入 cookie 裡的 client 已不再註冊、
  沒有目前的 HTTP context。options 不合法時，在建立 client 時擲出 `ArgumentException`。
- `RefreshTokenAsync` 沒有結果型別：成功時回傳新的 token，失敗時擲出例外。
- 例外訊息會帶 HTTP 狀態碼與 provider 的錯誤代碼，但不帶回應內容與錯誤說明，因為它們可能包含不該顯示給使用者的細節。

## 影響

- `AuthorizationResult.Exception` 只會是預期的失敗，應用程式可以直接告訴使用者，或當成一般事件記錄。
- 呼叫端要跟呼叫任何函式庫一樣，準備好處理其他例外。
- 導回網址裡的錯誤值來自 query string，任何送連結給使用者的人都能設定，所以應用程式顯示前要先編碼。
- `tests/Polhem.OAuth2.UnitTests/OAuth2ClientTests.cs` 涵蓋 state 不相符、導回帶錯誤、缺少授權碼或 code verifier、網路失敗、
  token 端點回傳錯誤、逾時時的失敗結果，以及取消往外拋。`OAuth2ProviderTests.cs` 涵蓋 token 回應與錯誤回應的讀取方式，
  `AspNetCoreOAuth2ManagerTests.cs` 與 `AspNetOAuth2ManagerTests.cs` 涵蓋 cookie 相關的失敗與設定錯誤。
