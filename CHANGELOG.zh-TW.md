# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.OAuth2、Polhem.OAuth2.AspNet 與 Polhem.OAuth2.AspNetCore 的重要變更，每個版本只列幾項重點。每個版本都連到
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下的發佈說明，那裡完整描述每一項變更，並說明每項破壞性變更影響誰、
如何遷移。破壞性變更在這裡以 **破壞性：** 標示。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。

> **1.x 的 minor 版本可能含有破壞性變更**，在這裡以 **破壞性：** 標示，遷移步驟寫在該版的發佈說明。patch 版本不含破壞性變更。
> 從 2.0.0 起，版號依循[語意化版本](https://semver.org/lang/zh-TW/)。參照這些套件時，請使用限制在一個 minor 版本內的範圍，
> 例如 `[1.4,1.5)`，而不是 `1.*`。這項政策與它的理由見 [ADR-010](docs/adr/adr-010-breaking-changes-in-1-x-minors.zh-TW.md)。

## [1.4.0] - 2026-10-21

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.4.0.md](docs/zh-TW/changelogs/1.4.0.md)

- `GitHubOAuth2Options` 以 GitHub 登入，GitHub App 與 OAuth App 都可以。
- `OAuth2Client.GetUserInfoAsync(accessToken)` 先由 provider 確認 token 發給哪個 client，再回傳前端送來的 access token 所屬的使用者。
- `OidcOAuth2Options` 可以登入其他任何 OpenID Connect provider，端點可以手動設定，也可以從 discovery 文件讀取。
- `AppleOAuth2Options` 讓網頁應用程式以 Apple 登入，網頁套件的 manager 也接受它以 form POST 送回的回呼。
- **破壞性：** Google、Auth0、Okta 與 Microsoft Entra ID 會以 client 與使用者資訊檢查登入取得的 ID token。
- **破壞性：** OpenID Connect provider 的 `email_verified` claim 為 false 時，`UserInfo.Email` 是 null。
- 送往 provider 的請求會在 `User-Agent` 標明函式庫、向 token 端點要求 JSON，以狀態碼 200 回傳的 token 錯誤也會帶著 provider 的錯誤代碼。

## [1.3.0] - 2026-09-26

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.3.0.md](docs/zh-TW/changelogs/1.3.0.md)

- **破壞性：** token 回應指定 `Bearer` 以外的 token type 時，登入或 refresh 會失敗。
- **破壞性：** 未傳入 `HttpClient` 時使用的共用 `HttpClient` 不再跟隨轉址。
- Polhem.OAuth2 新增 net8.0 組建，其預設 `HttpClient` 會汰換連線池的連線並跟上 DNS 變更，與 .NET 10 相同。
- `OAuth2Options.ClientAuthentication` 可以把 client secret 放在 HTTP Basic 標頭送出。
- `AddOAuth2AppRelay` 可以從組態區段讀取設定，ASP.NET Core 的 `OAuth2Manager` 可以被繼承。
- 讀取回應內容時會遵守取消，網頁套件與 loopback client 的幾個登入失敗也已修正。

## [1.2.0] - 2026-09-25

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.2.0.md](docs/zh-TW/changelogs/1.2.0.md)

- **破壞性：** 後端中轉以 data protection 保護快取裡的資料，因此代碼不會被另一個版本的伺服器兌換。
- **破壞性：** 中轉會拒絕已不在清單裡的應用程式回呼網址，以及超過 10 分鐘的 `CodeLifetime`。
- **破壞性：** Facebook token 端點的錯誤改為 `OAuth2Exception`，不再是 `HttpRequestException`。
- `OAuth2Client.Create(options, httpClientFactory)` 與 `AddOAuth2ClientWithHttpClientFactory` 從 factory 取得每個 `HttpClient`，例如 `IHttpClientFactory`。
- 中轉登入可以不靠例外開始，也可以回傳網址而不導向：`TryRedirectToAppAuthorization` 與 `Create…Url` 系列方法。
- 授權端點自帶的 query 會被保留；每一家 OpenID Connect provider 的 `UserInfo.UserName` 都會退而使用名字與姓氏。

## [1.1.0] - 2026-09-19

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.1.0.md](docs/zh-TW/changelogs/1.1.0.md)

- `AppOAuth2Client` 讓 Android、iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式登入。
- Polhem.OAuth2.AspNetCore 的後端中轉讓應用程式經由自己的後端登入，provider 的 token 留在後端。
- Polhem.OAuth2 的 net10.0 組建標示為 AOT 相容。
- Microsoft Entra ID provider 在個人 Microsoft 帳戶登入時也會填入 `UserName`。

## [1.0.0] - 2026-09-14

詳細內容與遷移步驟：[docs/zh-TW/changelogs/1.0.0.md](docs/zh-TW/changelogs/1.0.0.md)

- **破壞性：** Bee.OAuth2 改名為 Polhem.OAuth2：套件 ID、命名空間與目標框架都改變，桌面套件由 `LoopbackOAuth2Client` 取代。
- **破壞性：** provider 類別與 state 儲存型別不再公開，`AuthorizationResult` 與 `UserInfo` 改為 sealed 且唯讀。
- **破壞性：** 網頁套件把每次登入保存在受保護的 cookie，不再使用 session state，方法也改名。
- **破壞性：** PKCE 預設開啟，建立 client 時會檢查 options，端點必須使用 https，非預期的例外會往外拋。
- **破壞性：** 使用者資訊裡值為 JSON null 的欄位是 `null`，Microsoft Entra ID 的使用者識別碼改讀 `sub`。
- `LoopbackOAuth2Client` 讓桌面與主控台應用程式透過系統瀏覽器登入，每個 client 都會回傳 token，也可以更新 token。

[1.4.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.4.0
[1.3.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.3.0
[1.2.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.2.0
[1.1.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
