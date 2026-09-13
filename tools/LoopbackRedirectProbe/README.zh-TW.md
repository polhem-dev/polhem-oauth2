# Loopback 回呼網址實測工具

[English](README.md) | **繁體中文**

一個主控台工具：用系統瀏覽器，搭配 loopback 回呼網址（例如 `http://127.0.0.1:53682/callback`），對單一 provider
登入一次。它會顯示 provider 接不接受這個回呼網址、授權碼能不能換到 token，讓 Polhem.OAuth2 的桌面登入流程在動工前先確認可行。

## 準備

1. 在 provider 的後台登記要測試的回呼網址。
2. 把這個資料夾裡的 `probe.settings.example.json` 複製成 `probe.settings.json`，填入 client 憑證。
   `probe.settings.json` 已被 git 忽略，不要把憑證 commit 進去。

## 執行

```bash
cd tools/LoopbackRedirectProbe
dotnet run -- --provider Google --redirect http://127.0.0.1:0/callback
```

| 選項 | 說明 |
|------|------|
| `--provider` | `Google`、`Facebook`、`Line`、`Azure`、`Auth0` 或 `Okta` |
| `--redirect` | loopback 回呼網址。port 寫 `0` 會自動挑一個可用的 port，適用於接受任意 loopback port 的 provider。 |
| `--pkce` | `on`（預設）或 `off`。開啟 PKCE 時，只有本來就要求 client secret 的 provider 才會收到它。 |
| `--settings` | 設定檔路徑，預設是目前資料夾的 `probe.settings.json`。 |
| `--timeout` | 等待回呼的秒數，預設 180。 |

## 判讀結果

| 結束代碼 | 輸出 | 意義 |
|----------|------|------|
| 0 | `The provider accepted the loopback redirect, and the code exchange succeeded.` | 這個回呼網址從頭到尾都可用。 |
| 1 | `No callback arrived before the timeout.` | 瀏覽器始終沒有回到回呼網址，provider 的頁面通常會顯示回呼網址錯誤。 |
| 1 | `The provider accepted the redirect, but the code exchange failed` | 回呼網址被接受了，但換 token 失敗，例如 provider 要求 client secret。 |
| 2 | 其他訊息 | 參數、設定檔或監聽的 socket 有問題。 |

工具會印出登入帳號的使用者 ID、名稱與 email，但絕不會印出 token。
