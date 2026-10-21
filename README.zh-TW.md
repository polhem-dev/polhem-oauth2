# Polhem.OAuth2

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)

輕量的 .NET 登入套件，支援 OAuth2 與 OpenID Connect（OIDC）。桌面與主控台應用程式透過系統瀏覽器、loopback 回呼與 PKCE 登入，
可在 Windows、macOS、Linux 上使用。Android、iOS 與 Mac Catalyst 上的 .NET MAUI 應用程式透過 `WebAuthenticator` 登入，
可以直連，也可以經由自己的後端中轉。ASP.NET Core 與 ASP.NET（System.Web）應用程式使用搭配 PKCE 的授權碼流程，
每次登入各自保存在一個加密的 cookie。

## 支援的帳號

所有帳號都用 OAuth2 的授權碼流程登入。「OAuth2 + OIDC」表示該帳號另外回傳 OIDC 的 ID token，套件會檢查它或從它讀出使用者；
「OAuth2」表示使用者資料來自 provider 自己的 API。

| 帳號 | 協定 | 桌面與主控台 | .NET MAUI | ASP.NET Core 與 System.Web |
|------|------|--------------|-----------|----------------------------|
| [Google](docs/zh-TW/providers/google.md) | OAuth2 + OIDC | 可以 | iOS 與 Mac Catalyst 直連；Android 經由後端中轉 | 可以 |
| [Facebook](docs/zh-TW/providers/facebook.md) | OAuth2 | 可以 | 直連或經由後端中轉 | 可以 |
| [LINE](docs/zh-TW/providers/line.md) | OAuth2，email 取自 OIDC 的 ID token | 可以 | iOS 與 Mac Catalyst 直連；Android 經由後端中轉 | 可以 |
| [Microsoft Entra ID](docs/zh-TW/providers/microsoft-entra-id.md) | OAuth2 + OIDC | 可以 | 直連或經由後端中轉 | 可以 |
| [Auth0](docs/zh-TW/providers/auth0.md) | OAuth2 + OIDC | 可以 | 直連或經由後端中轉 | 可以 |
| [Okta](docs/zh-TW/providers/okta.md) | OAuth2 + OIDC | 可以 | 直連或經由後端中轉 | 可以 |
| [GitHub](docs/zh-TW/providers/github.md) | OAuth2 | 可以 | 只能經由後端中轉 | 可以 |
| [Apple](docs/zh-TW/providers/apple.md) | OAuth2 + OIDC | 不支援 | 只能經由後端中轉 | 可以 |
| [其他任何 OIDC provider](docs/zh-TW/providers/openid-connect.md)，例如 Keycloak 或 GitLab | OAuth2 + OIDC | provider 接受公開 client 時可以 | 直連或經由後端中轉 | 可以 |

「經由後端中轉」是指應用程式經由自己的 ASP.NET Core 後端登入，做法見 [.NET MAUI 應用程式](docs/zh-TW/guides/maui.md)。
每個帳號的文件說明要在 provider 後台登記什麼，以及實測過的結果。

## 套件

| 套件 | 目標框架 | 用途 |
|------|----------|------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0、net8.0、net10.0 | 各 provider、桌面、主控台與 .NET MAUI 應用程式的登入，以及其他伺服器端框架 |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core 應用程式 |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net472 | System.Web 上的 ASP.NET Web Forms 與 MVC 應用程式 |

```sh
dotnet add package Polhem.OAuth2
```

網頁套件相依於 Polhem.OAuth2，所以網頁應用程式只要加入自己的套件，例如 `dotnet add package Polhem.OAuth2.AspNetCore`。

1.x 的 minor 版本可能含有破壞性變更，會在[變更紀錄](CHANGELOG.zh-TW.md)中標示。參照這些套件時，請使用限制在一個 minor
版本內的範圍，例如 `[1.4,1.5)`，而不是 `1.*`（[ADR-010](docs/adr/adr-010-breaking-changes-in-1-x-minors.zh-TW.md)）。

## 以 Google 快速上手

### 桌面應用程式的 OAuth2

`LoopbackOAuth2Client` 會在回呼網址上監聽、用預設瀏覽器開啟登入頁，等 Google 導回後再用授權碼換 token。
請在 Google Cloud console 建立 Desktop app 類型的 OAuth client。

```csharp
using Polhem.OAuth2;

var options = new GoogleOAuth2Options
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "http://127.0.0.1:0/callback"
};

var client = new LoopbackOAuth2Client(options);
AuthorizationResult result = await client.SignInAsync();

if (result.IsSuccess)
    Console.WriteLine($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception.Message}");
```

### ASP.NET Core 的 OAuth2

建立 Web application 類型的 OAuth client，並把回呼網址登記為它的 redirect URI。

```csharp
using Polhem.OAuth2;

builder.Services.AddControllers();
builder.Services.AddOAuth2Client("Google", new GoogleOAuth2Options
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "https://localhost:7032/auth/callback"
});

var app = builder.Build();
app.MapControllers();
```

```csharp
using Microsoft.AspNetCore.Mvc;
using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;

public class AuthController(OAuth2Manager oauth2Manager) : ControllerBase
{
    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        return Redirect(oauth2Manager.CreateAuthorizationUrl(HttpContext, "Google"));
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback()
    {
        AuthorizationResult result = await oauth2Manager.CompleteAuthorizationAsync(HttpContext, HttpContext.RequestAborted);
        return result.IsSuccess
            ? Content($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}")
            : Content($"The sign-in failed: {result.Exception.Message}");
    }
}
```

### 網頁應用程式的 OpenID Connect

`OidcOAuth2Options.FromDiscoveryAsync` 從 provider 的 discovery 文件讀出端點。在應用程式啟動時呼叫一次：

```csharp
var keycloak = await OidcOAuth2Options.FromDiscoveryAsync("https://sso.example.com/realms/example");
keycloak.ProviderName = "Keycloak";
keycloak.ClientId = "your-client-id";
keycloak.ClientSecret = "your-client-secret";
keycloak.RedirectUri = "https://localhost:7032/auth/callback";
builder.Services.AddOAuth2Client("Keycloak", keycloak);
```

上面的 controller 不用改。其他 OpenID Connect provider（例如 GitLab 或 AWS Cognito）把網址換成那家 provider 的，並給它一個自己的
`ProviderName`。有專用 options 的 provider（例如 Google）請改用它自己的 options。

## 更多文件

每個帳號的文件連結在「支援的帳號」一表。以下指南涵蓋各種應用程式，以及它們共通的主題：

- [所有 provider 共用的 Options](docs/zh-TW/guides/options.md)
- [桌面與主控台應用程式](docs/zh-TW/guides/desktop.md)
- [.NET MAUI 應用程式](docs/zh-TW/guides/maui.md)
- [ASP.NET Core](docs/zh-TW/guides/aspnetcore.md)
- [ASP.NET（System.Web）](docs/zh-TW/guides/aspnet-system-web.md)
- [網頁應用程式：登入狀態怎麼保存](docs/zh-TW/guides/web-sign-in-state.md)
- [其他伺服器端框架](docs/zh-TW/guides/other-server-frameworks.md)
- [前後端分離的應用程式](docs/zh-TW/guides/separate-back-end.md)
- [結果、token 與錯誤](docs/zh-TW/guides/results-and-errors.md)
- [識別使用者](docs/zh-TW/guides/identifying-users.md)
- [從 Bee.OAuth2 遷移](docs/zh-TW/guides/migrating-from-bee-oauth2.md)
- [Samples](docs/zh-TW/guides/samples.md)

## 設計決策

設計背後的理由記錄在[架構決策紀錄](docs/adr/README.zh-TW.md)。

## 授權

[MIT](LICENSE.txt)。Copyright (c) Polhem contributors。

Polhem.OAuth2 延續自 [Bee.OAuth2](https://github.com/jeff377/bee-oauth2)。
