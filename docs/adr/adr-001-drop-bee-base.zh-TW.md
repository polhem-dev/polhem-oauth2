# ADR-001：移除 Bee.Base，state 加密改由套件自帶

[English](adr-001-drop-bee-base.md) | **繁體中文**

## 狀態

已採納（2026-09-13）

## 背景

本套件的前身 Bee.OAuth2 依賴 Bee.Base 3.4.0，用到兩樣東西：字串輔助方法（`StrFunc`），以及 `OAuth2StateCryptor`
背後的 AES-CBC-HMAC 加密實作。Bee.Base 還間接帶進了 Newtonsoft.Json，各 provider 直接使用它，卻沒有自己參照。

保留這個相依，每個 Polhem.OAuth2 套件的相依清單裡都會出現 Bee 的套件，而且會綁在一條已經不再開發的版本線上。
改用 Polhem 框架的共用基礎套件也行不通：框架只支援當前版本的 .NET，而 Polhem.OAuth2 仍要支援 netstandard2.0 與
.NET Framework 4.8。

## 決策

- `StrFunc.IsEmpty` 與 `StrFunc.IsNotEmpty` 改用 `string.IsNullOrWhiteSpace`。兩者語意相同，因為 `StrFunc`
  會先修剪字串，再與空字串比較。
- AES-CBC-HMAC 的實作複製進套件，成為 internal 型別 `AesCbcHmacCryptor` 與 `AesCbcHmacKeyGenerator`，
  位元組格式維持與 Bee.Base 3.4.0 相同。
- 在 JSON 解析改用 System.Text.Json 之前，先直接參照 Newtonsoft.Json。

### 為什麼位元組格式要保持相容

state 在導向 provider 之前產生，回呼時再讀回來。應用程式如果恰好在這兩個時間點之間升級，或是新舊版本的執行個體同時運作，
新程式碼就必須能用同一把 `OAUTH2_STATE_KEY`，解開舊程式碼產生的 state。維持格式也讓現有的金鑰繼續有效。

## 影響

- `tests/Polhem.OAuth2.UnitTests/AesCbcHmacCryptorTests.cs` 裡有一組用 Bee.Base 3.4.0 產生的密文，
  位元組格式一改，這些測試就會失敗。
- 加密型別是 internal，應用程式要自行產生 64 bytes 的亂數作為 `OAUTH2_STATE_KEY`，不能再呼叫函式庫的方法。
- 加密程式碼的修正從此由本 repo 負責。
