# Sign in with Apple

[English](../../en/providers/apple.md) | **繁體中文**

`AppleOAuth2Options` 透過 OpenID Connect 讓網頁應用程式以 Apple 帳號登入，結果的 provider 名稱是 `Apple`。
決策與實測記錄在 [ADR-009](../../adr/adr-009-sign-in-with-apple.zh-TW.md)。

## Options

```csharp
builder.Services.AddOAuth2Client("Apple", new AppleOAuth2Options
{
    ClientId = "com.example.web",          // Services ID
    TeamId = "your-team-id",
    KeyId = "your-key-id",
    PrivateKey = File.ReadAllText(keyPath), // Sign in with Apple key 的 .p8 檔
    RedirectUri = "https://www.example.com/auth/callback"
});
```

- 函式庫以 `PrivateKey` 簽署 client secret，並在到期前重簽。也可以改把預先簽好的 client secret 放在 `ClientSecret`，
  這時由你自己更換。`.p8` 檔要像 secret 一樣，不放進版本控制。
- Apple 拒絕 localhost 與 IP 的回呼網址，所以開發時回呼需要一個公開的 https 主機，例如 tunnel。
- 所有 provider 共用的 options，見[所有 provider 共用的 Options](../guides/options.md)。

## 桌面與主控台應用程式

不支援。`LoopbackOAuth2Client` 會拒絕這組 options，因為 Apple 只接受有網域名稱的 https 回呼網址，
而且每個 token 請求都要 client secret。

## .NET MAUI 應用程式

行動應用程式依[經由後端中轉登入](../guides/maui.md#經由後端中轉登入)的說明由後端登入。`AppOAuth2Client`
基於與 loopback client 相同的理由，拒絕這組 options。

## 網頁應用程式

- 預設的 scope `name email` 會讓 Apple 從它自己的網站以 form POST 回到回呼網址：回呼要同時接受 GET 與 POST，
  並排除在 antiforgery 驗證之外。見[網頁應用程式：登入狀態怎麼保存](../guides/web-sign-in-state.md)。
- 依 [ASP.NET Core](../guides/aspnetcore.md) 或 [ASP.NET（System.Web）](../guides/aspnet-system-web.md) 的說明設定。
  其他伺服器端框架要把 form 的 `user` 欄位傳給 `AuthorizationCallback`；
  見[其他伺服器端框架](../guides/other-server-frameworks.md)。

## Token 與使用者

- `UserId` 與 `Email` 來自 ID token。姓名只在使用者第一次登入時送來，要在那時保存；之後 `UserName` 為 null。
- `Email` 可能是 Apple 私密轉寄服務的地址。
- ADR-009 的實測中，Apple 有發 refresh token；refresh 時回傳新的 access token 與 ID token，但沒有新的 refresh token。

## 前後端分離的應用程式

`GetUserInfoAsync` 擲出 `NotSupportedException`：由前端送出 ID token，後端驗證。
見[前後端分離的應用程式](../guides/separate-back-end.md)。
