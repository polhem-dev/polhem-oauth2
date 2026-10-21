# 前後端分離的應用程式

[English](../../en/guides/separate-back-end.md) | **繁體中文**

桌面應用程式的範例為了簡潔，一次呼叫就完成登入並取得使用者資訊，適合自己使用登入結果的應用程式。
若前端登入後還要登入另一個後端，不要把前端取得的使用者資訊交給後端當作身份證明：後端無法分辨它是否遭到偽造。
只送 token 也不夠：使用者登入過的其他應用程式也持有這位使用者的有效 token，而使用者資訊端點一樣會回傳這位使用者。依優先順序：

1. 讓後端自己跑登入流程，token 就不會離開後端：網頁前端見 [ASP.NET Core](aspnetcore.md)，.NET MAUI 應用程式見
   [經由後端中轉登入](maui.md#經由後端中轉登入)。
2. 送出 OpenID Connect 登入取得的 ID token，由後端驗證，例如用 JwtBearer 驗證：以 provider 公開的金鑰驗簽章、
   檢查 issuer 與有效期限，以及 audience 是前端的 client ID。不檢查 audience 的驗證可能接受其他應用程式的 token。
   函式庫不驗證前端送來的 ID token。
3. 送出 access token，由後端呼叫 `GetUserInfoAsync`：用於不發標準 ID token 的 Facebook 與 GitHub。
   Google 與 LINE 也支援，但用它們的 ID token 較好。

```csharp
OAuth2Client client = oauth2Manager.GetClient("Facebook")!;
var result = await client.GetUserInfoAsync(accessToken, cancellationToken);
if (!result.IsSuccess)
    return Unauthorized();
// result.UserInfo 就是使用者；接著發出後端自己的 session。
```

- `GetUserInfoAsync` 取得使用者資訊之前，會先向 provider 確認 token 是發給 `ClientId` 或 `AcceptedClientIds` 之一的，
  例如同一個 Google Cloud 專案的 iOS 與 Android client。這項查證無法關閉。
- 支援 Google、LINE、Facebook 與 GitHub。Facebook 與 GitHub 以 client secret 查證，所以 options 需要 `ClientSecret`。
  它們也只能查證發給 `ClientId` 的 token，所以取得 token 的前端（例如桌面或行動應用程式）要和後端用同一個 Facebook 或 GitHub 應用程式。
- Microsoft Entra ID、Auth0、Okta、Sign in with Apple 與 `OidcOAuth2Options` 擲出 `NotSupportedException`：它們是 OpenID Connect
  provider，請改用 ID token。
- provider 拒絕、已過期、或發給其他 client 的 token 是失敗結果，網路失敗與逾時也是。缺少 client secret，以及 GitHub 拒絕 client 憑證時，會往外拋。
- LINE 與 Facebook 的查證只接受放在 query 的 token，所以會記錄請求 URI 的 HTTP client 會把它寫進 log。
  每次查證都是一個送往 provider 的請求，而 provider 會限制回應的數量，所以要限制後端這個端點被呼叫的頻率。

理由與各 provider 的查證方式記錄在 [ADR-007](../../adr/adr-007-access-token-from-front-end.zh-TW.md)。
