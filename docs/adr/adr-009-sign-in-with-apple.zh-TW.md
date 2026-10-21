# ADR-009：Sign in with Apple 走網頁流程，client secret 由套件簽署

[English](adr-009-sign-in-with-apple.md) | **繁體中文**

## 狀態

已採納（2026-10-20）。2026-10-20 於 pull request [#27](https://github.com/polhem-dev/polhem-oauth2/pull/27) 與
[#28](https://github.com/polhem-dev/polhem-oauth2/pull/28) 實作，並於同日以真實的 Apple 帳號測試，結果見「測試結果」。

## 背景

本套件支援 iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式登入（ADR-006）。App Store 審查指南 4.8 要求：提供第三方登入的應用程式，
也要提供一個符合其隱私要求的同等登入選項，實務上就是 Sign in with Apple。

Apple 和既有的 provider 不同的地方，正好落在套件原本的假設上：

- client secret 不是固定字串，而是以 Sign in with Apple key 的私鑰（Apple 只發一次的 `.p8` 檔）用 ES256 簽出的 JSON Web Token。
  到期時間超過建立後 15777000 秒（約六個月）的 secret 會被拒絕。
- Apple 沒有 user information 端點。使用者 ID 與 email 只在 ID token 裡，姓名只在第一次授權時由瀏覽器 POST 過來的 `user` 參數裡。
- 要求 `name` 或 `email` scope 時，Apple 從它自己的網站以 form POST 回到回呼網址（`response_mode=form_post`），而不是 GET 導向。
- return URL 必須是有網域名稱的 https，localhost 與 IP 位址都會被拒絕（見「測試結果」）。網頁流程無法登記 custom scheme。

## 決策

### Options 與 client secret

- `AppleOAuth2Options` 預設使用 `https://appleid.apple.com/.well-known/openid-configuration` 的端點，預設 scope 為 `name email`。
  `ClientId` 是 Services ID。
- 設定 `TeamId`、`KeyId` 與 `PrivateKey`（`.p8` 檔的內容）時由套件簽署 client secret。也接受放在 `ClientSecret` 的預先簽好的
  secret，此時由呼叫端在到期前更換。兩者都設定時以 `PrivateKey` 為準。
- 套件簽出的 secret：`iss` 為 Team ID、`sub` 為 client ID、`aud` 為 `https://appleid.apple.com`、`kid` 為 Key ID，有效一小時，
  到期前十分鐘重簽。效期短，secret 萬一出現在 log 裡損害也小；簽署的成本又遠低於隨它送出的請求。簽章採 RFC 7518 要求的 IEEE P1363 格式。
- 因為 netstandard2.0 沒有 `ECDsa.ImportFromPem`，所有 target 都由套件自己讀取金鑰檔。建立 client 時，下列情況會拒絕這組 options：
  金鑰不是含公鑰的 PKCS#8 P-256 金鑰、私鑰與該公鑰不配對、Key ID 或 Team ID 缺少或前後有空白、設定了 `UserInfoEndpoint`、
  `ClientAuthentication` 不是 `ClientSecretPost`（Apple discovery 文件列出的唯一方式）。所有訊息都不含金鑰內容。
- 之後簽署 secret 時若平台的加密功能失敗，會轉成 `OAuth2Exception`，讓登入照 ADR-003 的預期失敗，而不是丟出 `CryptographicException`。

### 使用者資訊

- 使用者是 token 端點回傳的 ID token 的 payload，以 ADR-008 的檢查讀取：audience 必須是 client ID、issuer 必須是
  `https://appleid.apple.com`，且必須有 `sub`。`UserInfo.RawJson` 就是這份 payload。
- 姓名由 `user` 參數的字串 `name.firstName` 與 `name.lastName` 組成。這個參數由瀏覽器 POST，所以忽略其中的 email、改用 ID token 的；
  不是這種 JSON 物件或長度超過 4096 字元的值一律忽略，也只採用字串。`AuthorizationCallback` 新增 `User` 屬性與接受它的建構式。
- Apple 只在使用者第一次授權應用程式時送出姓名，之後 `UserName` 為 null，應用程式要在第一次登入時自行保存。
- `OAuth2Client.GetUserInfoAsync(accessToken)` 與其他 OpenID Connect provider 一樣丟出 `NotSupportedException`：前端以 ID token
  證明登入者身分（ADR-007）。

### 回呼與 cookie

- 要求 `name` 或 `email` scope 時才送 `response_mode=form_post`，其他情況不送；只要求 `openid` 或不要求 scope 時，Apple 接受以 query 導回。
  `OAuth2Client.UsesFormPostResponse` 回報這件事。
- 兩個網頁 manager 從 POST 回呼的 form 讀參數，並把這類 client 的進行中登入放在 `SameSite=None` 的 cookie。決策內容、為何 state
  仍把回呼綁定在瀏覽器上、以及哪些情況瀏覽器不會送出這個 cookie，記在 ADR-005。
- PKCE 維持開啟。Apple 的 discovery 文件沒有列出它，但 Apple 接受這些參數，且會拒絕 verifier 錯誤的換 code 請求。

### Public client

- `LoopbackOAuth2Client` 與 `AppOAuth2Client` 建立時以 `ArgumentException` 拒絕這組 options，判斷依據是 provider 的能力，而不是型別檢查。
  Apple 的網頁流程既不接受 loopback 也不接受 custom scheme 的回呼網址，而且每個 token 請求都要 client secret，應用程式保管不了。
- 行動應用程式經 ADR-006 的後端中轉登入，不需要任何修改：中轉走的是網頁流程，form POST 也包含在內。

## 範圍外

- iOS 原生的 Sign in with Apple（`ASAuthorizationController`），它的 authorization code 要由後端以 bundle ID 當 client ID 換取；
  以及 `/auth/revoke` 的 token 撤銷，應用程式提供刪除帳號功能時，審查指南 5.1.1(v) 要求這麼做。兩者都需要另外設計。
- 桌面應用程式以 Apple 登入，這需要應用程式自己的後端。
- 驗證前端送來的 ID token，與 ADR-008 相同。

## 影響

- 網頁應用程式設定 `PrivateKey`、`KeyId`、`TeamId`、`ClientId` 與 https 的 `RedirectUri` 即可用 Apple 登入，回呼要同時接受 GET 與 POST，
  並排除在 antiforgery 驗證之外。
- 需要姓名的應用程式要在第一次登入時保存。使用者對該應用程式停用 Sign in with Apple 後再登入，Apple 會再送一次姓名。
- 使用者隱藏 email 時，`UserInfo.Email` 可能是 Apple 私密轉寄服務的 `@privaterelay.appleid.com` 地址。Apple 文件說明這時 ID token
  （在 `RawJson` 裡）會有 `is_private_email` claim；這點沒有實測。
- 針對 Apple 開發時，return URL 需要一個公開的 https 主機（例如 tunnel），因為 Apple 拒絕 localhost。
- `tests/Polhem.OAuth2.UnitTests/AppleOAuth2ProviderTests.cs` 涵蓋 options、簽出的 secret 與重簽、金鑰檢查、`user` 參數與 ID token；
  `AspNetCoreOAuth2ManagerTests.cs` 與 `AspNetOAuth2ManagerTests.cs` 涵蓋 form POST 回呼。fake provider 在 `/apple` 底下模仿 Apple，
  device tests 經中轉登入它。

## 測試結果

2026-10-20，Apple Developer 網站的 Services ID 頁面拒絕以 `localhost` 作為網域與 return URL。以 IP 位址作為 return URL 時，表單接受，
但儲存設定時被拒絕。

以已登記的 Services ID 呼叫授權端點：

| 請求 | 結果 |
|------|------|
| `name email` 搭配 `response_mode=query` | `invalid_request`：「response_mode must be form_post when name or email scope is requested.」 |
| `name email` 搭配 `form_post` 與 PKCE | 登入頁 |
| 只要求 `openid`，或不要求 scope，搭配 `query` | 登入頁 |
| `profile` | `invalid_request`：「Invalid client scope」 |

對不存在的 authorization code，無論 client secret 有效、過期或由別的金鑰簽署，token 端點都回 `invalid_grant`：它先檢查 code，
所以 client secret 是以真實登入驗證的。

`samples/OAuthAspNetCore` 網頁應用程式經公開的 tunnel，以真實的 Apple 帳號登入，client secret 由套件簽署：

- token 端點接受了 client secret，回傳有效一小時的 access token、refresh token 與 ID token。
- 第一次登入時 POST 了 `user` 參數，含 `name.firstName`、`name.lastName` 與 `email`，姓名有值。之後的登入沒有 `user` 參數，也沒有姓名。
- ID token 含 `iss`、`aud`、`exp`、`iat`、`sub`、`at_hash`、`email`、布林值的 `email_verified`、`auth_time` 與 `nonce_supported`。
  `sub` 的格式是 `<六位數字>.<32 位十六進位>.<四位數字>`。
- refresh 成功，回傳 access token 與 ID token，沒有新的 refresh token。
- code verifier 與 challenge 不符時，換 code 被以 `invalid_grant` 拒絕。
- 只要求 `openid` 以及不要求 scope 時，Apple 以 GET 導回，登入成功；對先前已分享過 email 的帳號，ID token 仍含 email。
- 使用者選擇分享自己的 email，所以沒有測到私密轉寄地址。

`samples/OAuthMaui` 經同一個網頁應用程式的後端中轉登入，在 iPhone 17 Pro 模擬器、Mac Catalyst 與 Android 模擬器上都成功，
各自回傳 ID token 的使用者 ID 與 email（各平台相同），沒有姓名，因為這個帳號先前已授權過該應用程式。

[run 39521534642](https://github.com/polhem-dev/polhem-oauth2/actions/runs/39521534642) 的 device tests 在 iOS、Mac Catalyst、Android 與
Windows 上執行了單元測試（包括 client secret 的簽署），並在 iOS、Mac Catalyst 與 Android 上經中轉登入 fake provider 的 Sign in with Apple
模式，走 form POST 回呼。
