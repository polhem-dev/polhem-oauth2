# 其他 OpenID Connect provider

[English](../../en/providers/openid-connect.md) | **繁體中文**

`OidcOAuth2Options` 可以登入任何遵循 OpenID Connect 的 provider，例如 Keycloak、GitLab、AWS Cognito 或 Authentik。
各項檢查與實測過的 provider 記錄在 [ADR-008](../../adr/adr-008-generic-openid-connect-provider.zh-TW.md)。

## Options

在應用程式啟動時從 provider 的 discovery 文件讀一次端點，之後就和其他 options 一樣使用：

```csharp
var gitlab = await OidcOAuth2Options.FromDiscoveryAsync("https://gitlab.com");
gitlab.ProviderName = "GitLab";
gitlab.ClientId = "your-client-id";
gitlab.ClientSecret = "your-client-secret";
gitlab.RedirectUri = "https://localhost:7032/auth/callback";
builder.Services.AddOAuth2Client("GitLab", gitlab);
```

- `ProviderName` 必填。使用者以它加上 `UserId` 識別，而兩家 provider 可能把同一個使用者 ID 給不同的人，
  所以每家 provider 都要有自己的名稱，而且命名後不要再改。
- discovery 文件的 `issuer` 必須等於傳入的 authority，結尾斜線除外。結果不會快取，所以不要每次登入都呼叫，
  而且只傳入應用程式信任的 authority。
- 端點與 `Issuer` 也可以手動設定。沒有使用者資訊端點的 provider 讓 `UserInfoEndpoint` 保持空白：使用者改從登入取得的
  ID token 讀取，這需要設定 `Issuer` 與 `openid` scope。
- 預設 scope 為 `openid profile email`，使用者名稱依序取 `name` claim、名與姓、`preferred_username`、`nickname`。
- 所有 provider 共用的 options，見[所有 provider 共用的 Options](../guides/options.md)。

## 桌面與主控台應用程式

`LoopbackOAuth2Client` 以公開 client 登入，不帶 client secret，所以 provider 必須接受搭配 PKCE 的公開 client。
Google 拒絕不送 client secret 的桌面 client；登入 Google 請用 [`GoogleOAuth2Options`](google.md)。

## .NET MAUI 應用程式

`AppOAuth2Client` 以 provider 為該 client 登記的自訂 scheme 登入。見[.NET MAUI 應用程式](../guides/maui.md)。

## 網頁應用程式

在 provider 登記回呼網址，再依 [ASP.NET Core](../guides/aspnetcore.md) 或
[ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。

## Token 與使用者

`UserId` 是 `sub` claim。函式庫會檢查每次登入取得的 ID token：檢查 audience，設定了 `Issuer` 時也檢查 issuer。
見[識別使用者](../guides/identifying-users.md)。

## 前後端分離的應用程式

`GetUserInfoAsync` 擲出 `NotSupportedException`：由前端送出 ID token，後端驗證。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
