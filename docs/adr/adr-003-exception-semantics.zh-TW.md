# ADR-003：只有預期的 OAuth2 失敗會轉成失敗結果

[English](adr-003-exception-semantics.md) | **繁體中文**

## 狀態

已採納（2026-09-13）

## 背景

`ValidateAuthorization` 回傳 `AuthorizationResult`。Bee.OAuth2 會攔下所有例外，放進失敗結果回傳。
結果是程式或設定上的錯誤（例如 client 沒有註冊、在 HTTP 請求之外呼叫）看起來跟一般的登入失敗一模一樣，
需要修正的問題很容易被忽略。

## 決策

- 協定層級的失敗擲出 `OAuth2Exception`：授權碼為空、token 回應裡沒有 access token、使用者資訊回應為空，
  以及 state 不存在、找不到對應的已註冊 client、或與儲存的 state 不相符。
- `ValidateAuthorization` 只把 OAuth2 交換預期會發生的失敗轉成失敗結果：
  - `BaseOAuth2Client`：`OAuth2Exception`、`HttpRequestException`、`TaskCanceledException`（請求逾時），
    以及 JSON 解析錯誤。
  - ASP.NET 與 ASP.NET Core 的 manager 另外會把解碼 state 時發生的 `FormatException` 與
    `CryptographicException` 轉成失敗結果。
- 其他例外一律往外拋。設定或程式錯誤（例如 client 名稱沒有註冊、沒有 HTTP context）擲出 `InvalidOperationException`。
- 例外訊息只帶 HTTP 狀態碼，不帶回應內容，因為回應內容可能包含不該顯示給使用者的細節。

## 影響

- `AuthorizationResult.Exception` 只會是預期的失敗，應用程式可以直接告訴使用者，或當成一般事件記錄。
- 呼叫端要跟呼叫任何函式庫一樣，準備好處理其他例外。
- `tests/Polhem.OAuth2.UnitTests/BaseOAuth2ClientTests.cs` 涵蓋 `BaseOAuth2Client` 的兩條路徑：
  授權碼為空、token 端點連不到時回傳失敗結果；儲存層發生非預期錯誤時往外拋。
- 已知缺口：state 的內部長度欄位格式錯亂（而不只是被竄改）時，目前仍會以非預期例外往外拋。
  預計讓解密流程改以 `CryptographicException` 回報這種情況。
