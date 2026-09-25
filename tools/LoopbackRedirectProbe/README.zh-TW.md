# Loopback 回呼網址實測工具

[English](README.md) | **繁體中文**

一個主控台工具：用系統瀏覽器，搭配 loopback 回呼網址（例如 `http://127.0.0.1:53682/callback`），對單一 provider
登入一次。它透過 Polhem.OAuth2 的 `LoopbackOAuth2Client` 登入，和應用程式用的是同一份程式碼，
會顯示 provider 接不接受這個回呼網址、授權碼能不能換到 token。

## 準備

1. 在 provider 的後台登記要測試的回呼網址。
2. 把 repo 根目錄的 `OAuthConfig.example.json` 複製成 `OAuthConfig.json`。每個要測的 provider，在它的 `Desktop` 區段
   填入 client 憑證，以及在該 provider 登記的 `RedirectUri`。`OAuthConfig.json` 已被 git 忽略，不要把憑證 commit 進去。
   建置時會複製到輸出目錄，工具就是從那裡讀。這份檔案與所有 sample 共用，結構說明在
   [repo README](../../README.zh-TW.md) 的 Samples 一節。

各 provider 比對回呼網址的規則不同，所以每個 `Desktop` 區段各有自己的 `RedirectUri`。port 一定要寫：沒寫 port 的網址
等於 port 80，而 macOS 不允許沒有管理員權限的程式監聽它。port 寫 `0` 會自動挑一個可用的 port，適用於接受任意 loopback port 的 provider。

## 執行

```bash
cd tools/LoopbackRedirectProbe
dotnet run -- --provider Google
```

| 選項 | 說明 |
|------|------|
| `--provider` | `Google`、`Facebook`、`Line`、`Azure`、`Auth0` 或 `Okta` |
| `--redirect` | 選填。這次執行改用這個 loopback 回呼網址，取代設定檔裡該 provider `Desktop` 區段的 `RedirectUri`。 |
| `--settings` | 設定檔路徑，預設是輸出目錄下、建置時從 repo 根目錄複製過來的 `OAuthConfig.json`。 |
| `--timeout` | 等待導回的秒數，預設 180。 |

## 判讀結果

| 結束代碼 | 輸出 | 意義 |
|----------|------|------|
| 0 | `The provider accepted the loopback redirect, and the code exchange succeeded.` | 這個回呼網址從頭到尾都可用。 |
| 1 | `No redirect with the state of this sign-in arrived before the timeout.` | 瀏覽器始終沒有回到回呼網址，provider 的頁面通常會顯示回呼網址錯誤。 |
| 1 | `The provider accepted the redirect, but the code exchange failed` | 回呼網址被接受了，但換 token 失敗，例如 provider 要求 client secret。 |
| 1 | `The sign-in failed` | provider 帶著錯誤導回（例如 `access_denied`），或它的回應無法解讀。 |
| 2 | 其他訊息 | 參數、設定檔或監聽的 socket 有問題。 |

工具會印出登入帳號的使用者 ID、名稱與 email，不印 token 回應的任何內容。
