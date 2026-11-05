# 有後端的應用程式

[English](../../en/guides/separate-back-end.md) | **繁體中文**

沒有自家後端的應用程式直接登入，並自己使用登入結果：[桌面與主控台應用程式](desktop.md)用 `LoopbackOAuth2Client`，
[.NET MAUI 應用程式](maui.md)用 `AppOAuth2Client`。

有後端的應用程式由後端登入，不分 provider，OpenID Connect provider 也一樣。後端直接從 provider 得知是誰登入，provider 的
token 與 client secret 都留在後端，再由後端發出自己的 session：

| 前端 | 登入方式 |
|------|----------|
| 網頁，包括單頁應用程式 | 後端跑網頁登入：[ASP.NET Core](aspnetcore.md) 或 [ASP.NET（System.Web）](aspnet-system-web.md) |
| Android、iOS 與 Mac Catalyst 上的 .NET MAUI | 以 `WebAuthenticator` 經由後端中轉：[經由後端中轉登入](maui.md#經由後端中轉登入) |
| 桌面與主控台應用程式，以及 Windows 上的 .NET MAUI | 以 `LoopbackRelayClient` 經由後端中轉：[經由後端登入](desktop.md#經由後端登入) |

不要把前端自己取得的任何東西送給後端，當作登入者的身分證明：後端無法分辨使用者資訊是否遭到偽造，而只送 token 也不夠。
使用者登入過的其他應用程式也持有這位使用者的有效 token，provider 的使用者資訊端點一樣會回傳這位使用者。

中轉屬於 Polhem.OAuth2.AspNetCore。其他框架的後端可以用 `OAuth2Client` 依相同步驟實作，網頁登入的做法見
[其他伺服器端框架](other-server-frameworks.md)。

1.5.0 移除了 `OAuth2Client.GetUserInfoAsync(accessToken)` 與 `OAuth2Options.AcceptedClientIds`，它們回傳前端送來的 access
token 所屬的使用者。請依上表把登入移到後端。理由記錄在 [ADR-011](../../adr/adr-011-back-end-signs-in.zh-TW.md)。
