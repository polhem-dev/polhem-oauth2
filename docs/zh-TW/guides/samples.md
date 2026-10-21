# Samples

[English](../../en/guides/samples.md) | **繁體中文**

| Sample | 示範 |
|--------|------|
| [OAuthConsole](../../../samples/OAuthConsole) | 主控台應用程式的桌面登入，任何作業系統都能執行 |
| [OAuthDesktop](../../../samples/OAuthDesktop) | .NET 上的 Windows Forms 桌面登入 |
| [OAuthWinForms](../../../samples/OAuthWinForms) | .NET Framework 4.8 上的 Windows Forms 桌面登入 |
| [OAuthAspNetCore](../../../samples/OAuthAspNetCore) | ASP.NET Core，包括給 OAuthMaui 用的中轉端點 |
| [OAuthMaui](../../../samples/OAuthMaui) | .NET MAUI：Android、iOS 與 Mac Catalyst 的直連與後端中轉登入，Windows 的 loopback 登入。需要 MAUI workload，不在 solution 裡。 |

[LoopbackRedirectProbe](../../../tools/LoopbackRedirectProbe/README.zh-TW.md) 可以在動工前先確認 provider 接不接受某個
loopback 回呼網址。

## 設定

所有 sample 與 loopback 回呼網址實測工具，都從 repo 根目錄的同一份 `OAuthConfig.json` 讀取 provider 設定。
先把 `OAuthConfig.example.json` 複製成 `OAuthConfig.json` 再填入，建置時會複製到每個專案的輸出目錄。
`OAuthConfig.json` 已被 git 忽略；不要把憑證填進 `OAuthConfig.example.json`。

provider 在後台是桌面、Web 與行動 App 的 client 分開登記的，所以每個 provider 依 client 類型各有一組 client：

```json
{
  "Providers": {
    "Okta": {
      "Domain": "",
      "Desktop": { "ClientId": "", "RedirectUri": "http://localhost:53682/callback" },
      "Web": { "ClientId": "", "ClientSecret": "", "RedirectUri": "https://localhost:7032/auth/callback" }
    }
  }
}
```

- 主控台與 Windows Forms sample 和實測工具讀 `Desktop`，ASP.NET Core sample 讀 `Web`。client 區段可以寫該 provider
  options 型別的屬性，例如 `Scopes` 與 `UsePkce`；沒寫的沿用型別的預設值。
- 兩組 client 共用、而且不是憑證的欄位寫在 provider 層：Auth0 與 Okta 的 `Domain`、Okta 的 `AuthorizationServerId`、
  Azure 的 `Tenant`。這層出現其他欄位會報錯，憑證因此不會不小心被共用。
- 後台其實只登記一個 client 的 provider（例如一個 LINE channel 登記兩個回呼網址），兩組填相同的 `ClientId`
  與 `ClientSecret` 即可。
- `ClientId` 還留空的區段會被略過，所以 sample 只會出現你已經填好的 provider。
- OAuthMaui sample 在 iOS 與 Mac Catalyst 讀 `iOS`，在 Android 讀 `Android`，在 Windows 讀 `Desktop`。它也會讀每個 `Web`
  區段的 `ClientId`，用來決定哪些 provider 提供經由後端的登入。`iOS` 與 `Android` 區段不能有 `ClientSecret`，因為 App 無法保密
  secret：有的話載入會失敗。OAuthMaui 建置時只打包 `ClientId`、`RedirectUri`、`Scopes`、`UsePkce` 與共用欄位；打包內容若會出現
  名稱含 `Secret` 或 `PrivateKey` 的欄位，建置就會失敗。Google 與 LINE 不接受直接導回
  Android App（[ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md)），所以沒有 `Android` 區段，sample 在 Android 上
  改由後端登入它們。GitHub 與 Apple 要求 client secret，所以既沒有 `iOS` 也沒有 `Android` 區段。
- Apple 的 `Web` 區段以 `PrivateKeyPath` 指向 repo 外的 `.p8` 檔，例如 `~/.config/polhem-oauth2/AuthKey_<key id>.p8`，sample 從它讀取金鑰。
  直接把 `PrivateKey` 寫進設定檔會報錯。Apple 拒絕 localhost 的回呼網址，所以 `RedirectUri`（以及中轉用的 `AppRelay.BackendUrl`）
  要指向轉送到 ASP.NET Core sample 的公開 https 主機，例如 tunnel。
- 最上層的 `AppRelay` 區段設定後端中轉：`BackendUrl` 是 ASP.NET Core sample 的網址，`RedirectUri` 是 App 的中轉回呼網址，
  ASP.NET Core sample 會用它呼叫 `AddOAuth2AppRelay` 登記。中轉走 HTTPS，所以模擬器必須信任 ASP.NET Core 的開發憑證。
  Android 模擬器上先執行 `adb reverse tcp:7032 tcp:7032`，讓 `localhost` 連到主機。
