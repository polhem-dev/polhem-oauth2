# ADR-004：桌面應用程式改由系統瀏覽器加 loopback 回呼登入

[English](adr-004-system-browser-loopback.md) | **繁體中文**

## 狀態

已採納（2026-09-14）。2026-09-14 首發前修訂，涵蓋 `OAuth2Client`、client secret 的規則與監聽程式。2026-09-19 修訂，
寫明這個流程實際測過的平台，以及 iOS 上的例外。2026-09-21 修訂，說明接受 `localhost` 與 RFC 8252 不同之處。

## 背景

Bee.OAuth2 有兩個桌面套件：給 .NET Framework 4.8 的 `Bee.OAuth2.WinForms`，與給 Windows 上 .NET 的 `Bee.OAuth2.Desktop`。
兩者都把 provider 的登入頁放在內嵌的 WebView2 控制項裡顯示，從「導向回呼網址」這個導覽動作讀出授權碼，
所以回呼網址不需要真的有人在監聽。

這個做法有幾個問題：

- RFC 8252（OAuth 2.0 for Native Apps）要求原生應用程式不要使用內嵌的瀏覽器：應用程式看得到使用者在 provider 頁面上輸入的內容，
  使用者無法確認網址列，也用不到自己瀏覽器裡的登入狀態與密碼管理員。provider 可以拒絕內嵌瀏覽器，Google 的政策就是如此。
- WebView2 只有 Windows 有，登入流程因此綁在 Windows Forms 上，而且每一種 .NET 都要各自一個套件。
- sample 用到的某些回呼網址，例如 `https://login.microsoftonline.com/common/oauth2/nativeclient`，
  只有在內嵌瀏覽器攔截導覽時才能運作。

## 決策

- 核心套件提供 `LoopbackOAuth2Client`。它的 `SignInAsync` 會在回呼網址的 loopback 位址上監聽、用預設瀏覽器開啟授權網址、
  等待導回，再用授權碼換 token。這就是 RFC 8252 第 7.3 節描述的 loopback 介面導回。它與網頁套件共用同一套 `OAuth2Client` 流程。
- 移除 `Polhem.OAuth2.WinForms` 與 `Polhem.OAuth2.Desktop`。這個流程只需要基底類別庫的型別，所以放在 netstandard2.0 的核心裡，
  不依賴任何 UI 框架。套件數量由五個減為三個。下方的 provider 實測在主控台應用程式中進行；ADR-006 的裝置測試在 Android、iOS、
  Mac Catalyst 與 Windows 上執行監聽程式，並在 Windows 上透過預設瀏覽器登入。
- 監聽程式自己接受 TCP 連線，不使用 `HttpListener`。Windows 上的 `HttpListener` 建在 http.sys 之上，
  `http://127.0.0.1:53682/` 這類字首需要先保留 URL，而且它無法自動挑選可用的 port。
- 監聽規則：
  - 只綁定 loopback 位址。回呼網址必須是 `http`，主機必須是 `localhost` 或 loopback 位址；其他網址在建立 client 時就會被拒絕。
    RFC 8252 第 8.3 節不建議使用 `localhost`，這個 client 仍然接受：Facebook 拒絕 `127.0.0.1`，Microsoft Entra ID 只能登記
    `http://localhost`，下方的 provider 實測結果都有記錄。比對請求的 `Host` 標頭，回應了該節所顧慮的問題。
  - `localhost` 會同時綁定 IPv4 與 IPv6 的 loopback 位址，因為瀏覽器可能把這個名稱解析成其中任何一個。
    只有在機器不支援 IPv6 時才略過 IPv6；如果另一個程式已經在那裡使用同一個 port，就直接啟動失敗，
    否則授權碼可能被送到那個程式手上。
  - port 寫 0 時，每次登入都會挑一個可用的 port，適用於接受任意 loopback port 的 provider。每次登入都送出帶著實際 port 的回呼網址；
    options 在建立 client 時就複製，之後不會被修改。
  - 同時讀取多條連線，所以瀏覽器預先建立卻閒置的連線，不會拖慢導回。
- 只有路徑是回呼路徑、`Host` 標頭是回呼網址的主機與 port、而且帶著本次登入 state 的請求，才會結束等待。
  瀏覽器裡開著的任何網頁都能對 loopback 位址發出請求，透過 DNS rebinding 還能使用自己的主機名稱，
  所以其他請求會收到回應但被忽略，既不能結束這次登入，也不能塞進授權碼。路徑會先解開百分比編碼再比對。
- 導回後瀏覽器顯示的頁面只說「已收到回應」，因為換 token 是之後才進行的。
- `OpenBrowser` 讓應用程式用其他方式開啟網址，例如透過 UI 框架的啟動器。它接收跳脫過的絕對 URI，並回傳一個 task。
- 失敗的處理，補充 ADR-003：在 `Timeout` 內沒有導回，轉成帶 `TimeoutException` 的失敗結果；等待期間或換 token 期間的取消，
  轉成帶 `OperationCanceledException` 的失敗結果。port 無法監聽（`SocketException`）、同一個 client 同時登入第二次
  （`InvalidOperationException`）、`OpenBrowser` 為 null 而找不到預設瀏覽器（`Win32Exception`；iOS 無法啟動處理程序，
  擲出 `PlatformNotSupportedException`），則往外拋。
- loopback client 是 public client。它一律使用 PKCE，不看 `OAuth2Options.UsePkce` 的設定，這是 RFC 8252 對原生應用程式的要求；
  它也不送 client secret，只有 Google 例外。Google 的文件把 client secret 列為已安裝應用程式的選填參數，
  但 2026-09-26 不送 secret 的實測失敗：token 端點回應 `invalid_request`，所以 secret 是必要的，這個例外維持。
  下方實測的其他 provider 都在沒有 secret 的情況下換到 token。
  能夠保密 secret 的網頁 client，只要設了就一律送出。

## 各 provider 實測結果

每個 provider 都以 `tools/LoopbackRedirectProbe` 測試，這個工具透過 `LoopbackOAuth2Client` 登入。

| Provider | 應用程式類型 | 測試的回呼網址 | 結果 |
|----------|--------------|----------------|------|
| Google | Desktop app | `http://127.0.0.1:<可用 port>/callback`、`http://localhost:53682/callback` | 開啟 PKCE 時被接受，換 token 成功（2026-09-14）。登記的網址寫的是 port 0，實際以可用 port 導回仍被接受。 |
| Microsoft Entra ID | Mobile and desktop applications | `http://localhost:<可用 port>/`，後台登記為 `http://localhost` | 開啟 PKCE 時被接受，沒送 client secret 也換 token 成功（2026-09-14）。port 被忽略，結尾的 `/` 也不影響比對。`/callback` 這類路徑是否必須一致，以及 `127.0.0.1`，尚未測試。 |
| Auth0 | Native | `http://127.0.0.1:53682/callback`、`http://localhost:53682/callback` | 兩個網址開啟 PKCE 時都被接受，沒送 client secret 也換 token 成功（2026-09-14）。port 必須一致：以 `127.0.0.1` 加上可用 port 導回時，被錯誤頁面拒絕。 |
| Okta | Native（Client authentication 為 None、要求 PKCE） | `http://localhost:53682/callback`、`http://127.0.0.1:53682/callback` | 兩個網址都被接受，沒送 client secret 也換 token 成功（2026-09-14）。`default` 授權伺服器一開始因為沒有任何存取政策而拒絕請求；為這個 app 加上允許 authorization code 的政策與規則後解決。以 `localhost` 加上可用 port 導回時沒有回來，port 必須一致。 |
| LINE | — | `http://localhost:53682/callback` | 開啟 PKCE 時被接受，沒送 client secret 也換 token 成功（2026-09-14）。port 必須一致：後台只登記 `http://localhost/callback` 時，導回 port 53682 被當成無效的 `redirect_uri` 拒絕。使用者資訊沒有 email。`127.0.0.1` 尚未測試。 |
| Facebook | — | `http://localhost:53682/callback`、`http://127.0.0.1:53682/callback` | `localhost:53682` 開啟 PKCE 時被接受，沒送 client secret 也換 token 成功（2026-09-14）。`127.0.0.1:53682` 被拒：登入頁顯示應用程式的網路連線不安全。`localhost` 以可用 port 導回也被接受，雖然只登記了 port 53682。app 的模式（開發或上線）沒有記錄，上線狀態的 app 尚未測試。 |

以上結果記錄於 2026-09-14 修訂之前。LINE provider 現在會從 ID token 讀取 email，channel 有權限讀取時，之後的實測可以取得 email。

### 2026-09-26 重測

同一批應用程式，以探測工具的 `--refresh yes` 登入；Microsoft Entra ID、Auth0 與 Okta 另加 `--scopes "openid email profile offline_access"`。
工具會回報 token type、有效期、原樣回傳的 scope，以及回傳了哪些 token。

| Provider | 結果 |
|----------|------|
| Google | 不送 client secret（`--secret omit`）時，token 端點以 `invalid_request` 拒絕授權碼。 |
| Microsoft Entra ID | 有發 refresh token，refresh 成功。scope 回傳為 `openid email profile`，以空白分隔。 |
| Auth0 | 有發 refresh token，refresh 成功。refresh 的回應沒有新的 refresh token，表示這個 tenant 沒有啟用輪替。 |
| Okta | 沒有發 refresh token，核准的 scope 也沒有 `offline_access`：這個應用程式沒有被允許 refresh token grant。 |
| LINE | 不送 client secret 也換 token 與 refresh 成功，並發了 refresh token。核准的 scope 沒有 `email`，channel 尚未取得該權限。 |
| Facebook | 不送 client secret、以 PKCE 換 token 成功。token type 回傳為小寫的 `bearer`，沒有回傳 scope，也沒有發 refresh token。 |

## 影響

- 原本使用桌面套件的應用程式改用 `LoopbackOAuth2Client`，並在每個 provider 登記 loopback 回呼網址。
  `Caption`、`Width`、`Height`、`AuthorizationForm` 與桌面版的 `OAuth2Manager` 沒有替代品，因為 provider 的頁面改在使用者的瀏覽器裡開啟。
- 桌面應用程式無法保密 client secret：隨程式散佈的任何東西都能被取出。
- 沒寫 port 的回呼網址等於 port 80。在 macOS 上，實測工具沒有管理員權限時無法監聽這個 port，所以回呼網址應該寫出 port。
- 登入期間焦點會在瀏覽器上。Windows Forms 的 sample 在登入結束後會把視窗帶回前景。
- `tests/Polhem.OAuth2.UnitTests/LoopbackListenerTests.cs` 與 `LoopbackOAuth2ClientTests.cs` 涵蓋 port 的挑選、IPv6 的 port 衝突、
  忽略沒有 state 或 `Host` 標頭不符的請求、閒置的連線、逾時、取消，以及換 token 時送出的回呼網址。
  在 Windows 上，這些測試也會在 .NET Framework 執行，那裡用的是監聽程式的 netstandard2.0 組建。
