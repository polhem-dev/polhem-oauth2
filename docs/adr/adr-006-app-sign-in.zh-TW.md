# ADR-006：行動應用程式直連 provider 登入，或透過自家後端中轉

[English](adr-006-app-sign-in.md) | **繁體中文**

## 狀態

已採納（2026-09-19）。2026-09-19 實作完成；以函式庫登入的結果見「實測結果」。
2026-09-21 修訂：回呼網址的規則改為公開，後端中轉改為呼叫它，不再複寫一份；中轉會保護它的快取資料，在把登入結果送回時再確認一次應用程式回呼網址，能以請求帶來的值開始登入而不擲例外，並且限制了代碼效期的上限。

## 背景

Polhem.OAuth2 已涵蓋網頁應用程式（ASP.NET Core 與 System.Web）與桌面應用程式（`LoopbackOAuth2Client`，ADR-004），
但還沒有涵蓋 Android、iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式：

- `OAuth2Options` 只接受 `http` 與 `https` 的回呼網址，所以 `com.example.app:/oauth2redirect` 這類自訂 URI scheme
  會被每一個 client 拒絕。
- `LoopbackOAuth2Client` 用 `Process.Start` 開啟瀏覽器，在 iOS 與 Android 上做不到；行動應用程式也不能指望在 loopback port 上監聽。

.NET MAUI 提供 `WebAuthenticator`：在 iOS 與 Mac Catalyst 上開啟 `ASWebAuthenticationSession`，在 Android 上開啟
Custom Tabs，並回傳 provider 導回的網址。在 .NET 10 上它無法在 Windows 運作
（[dotnet/maui#2702](https://github.com/dotnet/maui/issues/2702)）。

行動應用程式無法保密 client secret，因此是 public client（RFC 8252）。provider 是否允許這種 client 導回應用程式，
取決於 provider 與平台。2026-09-19 以一個用完即丟的 MAUI 應用程式實測每個 provider：自組授權網址、用
`WebAuthenticator` 開啟，並以 PKCE、不帶 client secret 換 token：

| Provider | 後台登記 | 回呼網址 | iOS | Mac Catalyst | Android |
|----------|----------|----------|-----|--------------|---------|
| Google | iOS client 類型，填 bundle ID | `com.googleusercontent.apps.<client id>:/oauthredirect`，或與 bundle ID 相同的自訂 scheme | 接受 | 接受，使用同一個 iOS client | — |
| Google | Android client 類型，填套件名稱與簽章憑證 | 上述任一形式 | — | — | 拒絕：「Custom URI scheme is not enabled for your Android client」 |
| Microsoft Entra ID | iOS/macOS 平台，或「行動應用程式與桌面應用程式」下的自訂 URI | `msauth.<bundle id>://auth`，或 `<scheme>://<path>` | 接受 | 接受 | — |
| Microsoft Entra ID | Android 平台，填套件名稱與簽章雜湊 | `msauth://<套件名稱>/<簽章雜湊>`，或 `<scheme>://<path>` | — | — | 接受 |
| Auth0 | Native 應用程式的 Allowed Callback URLs | 自訂 scheme | 接受 | 接受 | 接受 |
| Okta | Native 應用程式的 Sign-in redirect URIs | 自訂 scheme | 接受 | 接受 | 接受 |
| LINE | LINE Login channel 的 iOS bundle ID | `line3rdp.<bundle id>://auth` | 接受 | 接受 | — |
| LINE | LINE Login channel 的 Android 套件名稱與簽章 | `line3rdp.<套件名稱>://auth`，或自訂 scheme | — | — | 拒絕：「Invalid redirect custom scheme」 |
| Facebook | iOS 平台填 bundle ID；Android 不需要登記 Android 平台 | `fb<app id>://authorize` | 接受 | 接受 | 接受 |

表格補充：

- 所有被接受的組合，都在不帶 client secret 的情況下換到 token。Facebook 的這一點屬於實測到的行為：它的手動流程文件把 client secret 列為必要、
  也沒有提到 PKCE，所以 Facebook 隨時可以收回。
- Entra ID 的入口網站不接受 `<scheme>:/<path>` 形式的自訂 URI，必須寫成 `<scheme>://`。
- Facebook 實際導回 `fb<app id>://authorize/`，多一個結尾斜線；token 請求的 `redirect_uri` 必須帶這個斜線才會成功。
- Facebook 與 LINE 在所有測過的平台上，都拒絕一般的自訂 scheme `dev.polhem.redirectprobe:/oauth2redirect`。
- Auth0 把自訂 scheme 標為無法驗證的回呼網址（non-verifiable callback URI），每次登入都請使用者確認。
  Microsoft 則要求個人帳號確認應用程式來自可信任的來源。
- 綁定應用程式的 https 回呼網址（RFC 8252 第 7.2 節）尚未測試。

## 決策

### 應用程式的兩種登入方式

- **直連。** 應用程式是 public client。它在系統瀏覽器的 session 中開啟授權網址，provider 導回應用程式，
  應用程式以 PKCE 換 token。這給沒有自家後端的應用程式使用，例如只呼叫 provider API 的應用程式。
  使用者資訊留在應用程式裡，不能當作對伺服器的身分證明：要登入自家後端的應用程式，必須讓後端自己取得使用者。
- **後端中轉。** 應用程式開啟自家 ASP.NET Core 後端上的登入網址。後端以 confidential client 執行既有的網頁流程，
  再帶著一個短效、只能用一次的代碼導回應用程式。應用程式透過 HTTPS 向後端兌換這個代碼，並證明自己持有登入開始前
  產生的 verifier，取得使用者資訊。provider 的 token 留在後端，由後端發給應用程式自己的 session。
  它只用到既有的網頁流程；上表中無法直連的組合（Google 與 LINE 的 Android）必須使用這個方式。

同一份 `Polhem.OAuth2.AspNetCore` 註冊，同時服務瀏覽器使用者與應用程式使用者。

### 核心：`AppOAuth2Client`

- 在 `LoopbackOAuth2Client` 旁新增 sealed 的 `AppOAuth2Client`，以 provider options 建立：
  `AppOAuth2Client(OAuth2Options options, Func<Uri, Uri, CancellationToken, Task<Uri>> authenticate, HttpClient? httpClient = null)`。
  它和 `LoopbackOAuth2Client` 一樣提供 `SignInAsync` 與 `RefreshTokenAsync`。
- `authenticate` 接收授權網址、回呼網址與取消 token，開啟授權網址，並回傳 provider 導回的網址。
  因此核心套件不相依 MAUI：使用 `WebAuthenticator` 時，這個委派回傳 `WebAuthenticatorResult.CallbackUri`。
- 回傳的網址由 client 自己解析：從 query 或 fragment 讀取 `code`、`state`、`error` 與 `error_description`，
  解開百分比編碼並把 `+` 視為空白，同名參數取第一個值。網頁的 manager 則把重複的參數視為不存在。
  兩種做法都安全，因為 state 相符之前不會用到其他值；做法不同是因為網頁框架會把同名的值全部交出來，而把它們接在一起什麼都比對不到。
- 回呼網址：接受 `https` 或自訂 scheme 的絕對 URI；拒絕 `http`、`javascript`、`data`、`file`、相對 URI，
  以及帶 fragment 的 URI。自訂 scheme 不要求含有句點，因為 Facebook（`fb<app id>`）與 Android 上 Entra ID（`msauth`）
  規定的形式都沒有句點。`OAuth2Client` 維持只接受 `http` 與 `https`，網頁應用程式不會多出新的回呼形式。
  這條規則是公開的 `OAuth2Options.IsAppRedirectUri`，後端中轉對它的應用程式回呼網址也呼叫同一個方法，兩邊因此共用一份定義。
  ADR-005 排除的是核心套件的 internal 成員，不是它的公開 API。
- 一律使用 PKCE；沒設 client secret 就不送，設了則與 loopback client 相同，依 `OAuth2Provider.RequiresClientSecret` 決定。
- 失敗的處理，補充 ADR-003：`authenticate` 擲出的 `OperationCanceledException`（例如 `WebAuthenticator` 用來表示使用者關閉了登入的 `TaskCanceledException`）
  與呼叫端 token 的取消，轉成帶 `OperationCanceledException` 的失敗結果。state 不符、provider 回傳錯誤、缺少授權碼，
  轉成帶 `OAuth2Exception` 的失敗結果。`authenticate` 擲出的其他例外往外拋；同一個 client 同時第二次登入
  （`InvalidOperationException`）也往外拋。
- `AppOAuth2Client` 以沒有結尾斜線的 `fb<app id>` 回呼網址登入時，`FacebookOAuth2Provider` 會在 token 請求的回呼網址補上結尾斜線。
- 取消與失敗的對應邏輯與 `LoopbackOAuth2Client` 共用，不另外複製一份。

### ASP.NET Core：後端中轉

- 以 `services.AddOAuth2AppRelay(options => ...)` 註冊中轉。`OAuth2AppRelayOptions.AppRedirectUris` 列出後端可以導回的
  應用程式回呼網址，以完全相同比對；`CodeLifetime` 設定中轉代碼可兌換的期限。
- `OAuth2Manager.RedirectToAppAuthorization(context, clientName, appRedirectUri, codeChallenge)` 開始一次中轉登入。
  它拒絕未登記的應用程式回呼網址，把回呼網址與 S256 code challenge 跟這次登入一起存進 ADR-005 的加密 cookie，
  再以該 client 的網頁回呼網址導向 provider，所以不必在 provider 後台多登記回呼網址。在應用程式開啟的端點裡，這三個值都來自請求，
  因此 `TryRedirectToAppAuthorization` 接受同樣的值，並在 client 名稱、回呼網址或 code challenge 不合法時回傳 false，讓端點回應 400。
  會擲例外的那個方法保留給後端自己決定這些值的情況，那時值不合法屬於程式錯誤（ADR-003）。
- provider 導回既有的網頁回呼。呼叫 `CompleteAuthorizationAsync` 之後，`OAuth2Manager.RedirectToAppAsync(context, result)`
  遇到網頁登入回傳 false；遇到中轉登入則把使用者資訊存在一個新的隨機代碼下，帶著這個代碼導回應用程式的回呼網址
  （登入失敗時改帶錯誤），並回傳 true。
- `OAuth2Manager.RedeemAppCodeAsync(clientName, code, codeVerifier, cancellationToken)` 在代碼存在、未過期、屬於該 client，
  且 verifier 與存下的 challenge 相符時回傳使用者資訊，並移除這個代碼；否則回傳 null。
- 中轉代碼存放在 `IDistributedCache`：單台伺服器用記憶體快取，多台伺服器接收回呼時改用分散式快取。套件不另設自己的儲存抽象層。
  每筆資料都以 ASP.NET Core data protection 保護，使用自己的 purpose，因此不需要信任快取：讀得到快取也看不到使用者資訊，
  沒有金鑰的人寫進去的資料也不會被兌換。資料的 key 是代碼的雜湊。
- 代碼的效期由快取以它自己的時鐘在 `CodeLifetime` 到期時結束；`CodeLifetime` 最長 10 分鐘，也就是 RFC 6749 第 4.1.2 節對授權碼的建議上限。
  資料裡另外存了到期時間，用來應付快取把早該丟掉的資料又回傳的情況。這第二道檢查容許一分鐘的誤差，與登入 cookie 的做法相同，
  避免時鐘比發出代碼的伺服器快的那台伺服器提早結束效期。有註冊 `TimeProvider` 時，`OAuth2Manager` 會從它讀取時間，測試就是這樣推進時鐘的。
- `RedirectToAppAsync` 會再確認一次這次登入的應用程式回呼網址仍然有註冊，因為登入開始之後它可能已被移除。沒有註冊時擲出
  `InvalidOperationException`，與登入 cookie 裡的 client 已不再註冊的情況相同（ADR-003），而且不會導向。回呼請求中斷時它會停止，
  與 `CompleteAuthorizationAsync` 一致。
- 導回應用程式的網址與兌換的回應，都不帶任何 provider token。

### .NET 10 的平台對照

| 平台 | 直連 | 後端中轉 | Loopback |
|------|------|----------|----------|
| Android | `WebAuthenticator`，Google 與 LINE 除外 | `WebAuthenticator` 連到後端 | 不適用 |
| iOS | `WebAuthenticator` | `WebAuthenticator` 連到後端 | 不適用 |
| Mac Catalyst | `WebAuthenticator` | `WebAuthenticator` 連到後端 | 可行：沙箱需要 `com.apple.security.network.server`，沒有它時綁定 loopback port 會以「Permission denied」失敗 |
| Windows | `WebAuthenticator` 能在 Windows 運作前不可用 | 不提供 | `LoopbackOAuth2Client` |

Mac Catalyst 的 loopback 結果來自實測應用程式裡自寫的監聽程式，不是 `LoopbackOAuth2Client`。

### 範圍

- 不另發 `Polhem.OAuth2.Maui` 套件。核心維持不綁 UI 框架，MAUI 的串接程式碼放在 sample 與 README。
  另發 MAUI 套件的話，每次建置整個 solution 都需要 MAUI workload。
- 不為 System.Web 提供後端中轉。
- 不為桌面應用程式提供後端中轉，包括 MAUI 應用程式的 Windows 平台，它們維持使用 `LoopbackOAuth2Client`。
  `WebAuthenticator` 能在 Windows 運作後，再重新評估 Windows 平台。
- 中轉登入後，應用程式只拿到使用者資訊，拿不到 provider 的 token，所以裝置上不必保存任何 provider token。

## 實測結果

### 真實 provider

2026-09-19 以函式庫透過 `samples/OAuthMaui` 登入；後端中轉搭配 `samples/OAuthAspNetCore` 後端。iOS 在 iPhone 17 Pro 模擬器
（iOS 26.5），Mac Catalyst 在 macOS 26.6，Android 在 Android 15 的模擬器上。

| Provider | 直連，iOS | 直連，Mac Catalyst | 直連，Android | 後端中轉 |
|----------|-----------|--------------------|---------------|----------|
| Google | 成功 | 成功 | 不可行（見背景） | Mac Catalyst 成功 |
| Microsoft Entra ID | 僅實測 App | 成功 | 成功 | 未測 |
| Auth0 | 僅實測 App | 成功 | 成功 | 未測 |
| Okta | 僅實測 App | 成功 | 成功 | 未測 |
| LINE | 僅實測 App | 成功；使用者資訊沒有 email | 不可行（見背景） | 未測 |
| Facebook | 僅實測 App | 成功，結尾斜線由 `FacebookOAuth2Provider` 補上 | 成功 | 未測 |

- 「僅實測 App」表示背景一節那個用完即丟的 App 登入成功過，但沒有在 iOS 上以函式庫對該 provider 登入。iOS 與 Mac Catalyst
  走同一條程式路徑，也共用 provider 的後台登記。
- 後端中轉只在 Mac Catalyst 上對真實 provider 測過。iOS 與 Android 需要模擬器信任後端的開發憑證；這兩個平台的中轉由下方的
  端對端測試涵蓋。
- Windows 沒有對真實 provider 測過。
- 中途關閉登入，全部透過函式庫：Android 以返回鍵關閉 Custom Tabs；iOS 在直連與中轉登入前的系統提示按取消；Mac Catalyst
  關閉登入視窗。每一種都成為帶 `OperationCanceledException` 的失敗結果，sample 顯示為已取消。Mac Catalyst 上在 Google 自己的
  頁面按取消會回傳 `access_denied`，和其他 provider 錯誤一樣，成為帶 `OAuth2Exception` 的失敗結果。

### 自動化測試

- `tests/Polhem.OAuth2.DeviceTests` 在 Android、iOS、Mac Catalyst 與 Windows 上以 Release 執行核心套件的單元測試，核心套件因此
  會像在應用程式裡一樣被 trim，在 iOS 上也會預先編譯（AOT）。
- 同一個 App 登入 `tests/Polhem.OAuth2.FakeProvider`，它以 HTTPS 模仿 Auth0：在 Android、iOS 與 Mac Catalyst 上測直連成功、
  provider 回傳錯誤與後端中轉，在 Windows 上以 `LoopbackOAuth2Client` 與預設瀏覽器登入。
- Device Tests workflow 在四個平台上執行這兩者，只在手動觸發時執行，因為它比建置花的時間長得多。使用者中途關閉登入在那裡
  無法自動化，改為如上手動檢查。

## 影響

- 同時在 iOS 與 Android 上登入 Google 的應用程式，需要兩種類型的 client 各一個，client ID 與回呼網址都不同，
  因此應用程式的設定必須能依平台而異。
- 其他應用程式可以登記相同的自訂 scheme。PKCE 讓被攔截的授權碼無法兌換；中轉代碼也必須搭配發起登入的應用程式持有的
  verifier 才能兌換。
- `IDistributedCache` 沒有原子性的「讀取並移除」。同一個代碼的兩個兌換請求若同時抵達，可能都會成功；
  但兩者都需要發起登入的應用程式所持有的 verifier。
- 每台可能接收中轉回呼的伺服器，都必須共用 data protection 金鑰環（同 ADR-005）與中轉快取。金鑰環同時保護中轉快取裡的資料。
- 1.1.0 沒有保護這些資料。兩個版本的伺服器共用同一個快取的期間，一個版本發出的代碼不會被另一個版本兌換，應用程式需要重新登入；
  影響的時間長度就是一個代碼的效期。
- 中轉登入與網頁登入一樣，必須在 HTTPS 頁面開始與結束，並在登入 cookie 的效期內完成。
- 新增的 API 先宣告在 `PublicAPI.Unshipped.txt`，發佈 1.1.0 時移進 `PublicAPI.Shipped.txt`，所有新增的 API 都是這樣處理：
  public API analyzer 以 shipped 檔比對既有的 API。
