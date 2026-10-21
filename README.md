# Polhem.OAuth2

**English** | [繁體中文](https://github.com/polhem-dev/polhem-oauth2/blob/main/README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-oauth2&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-oauth2)

Lightweight sign-in for .NET with OAuth2 and OpenID Connect (OIDC). Desktop and console applications sign in through the system
browser with a loopback redirect and PKCE, on Windows, macOS and Linux. .NET MAUI applications on Android, iOS and Mac
Catalyst sign in through `WebAuthenticator`, directly or through their own back end. ASP.NET Core and ASP.NET (System.Web)
applications use the authorization code flow with PKCE, and keep each sign-in in a protected cookie.

## Supported accounts

Every account signs in with the OAuth2 authorization code flow. "OAuth2 + OIDC" means the account also returns an
OIDC ID token, which the library checks or reads the user from; "OAuth2" means the user comes from the provider's own API.

| Account | Protocol | Desktop and console | .NET MAUI | ASP.NET Core and System.Web |
|---------|----------|---------------------|-----------|-----------------------------|
| [Google](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/google.md) | OAuth2 + OIDC | Yes | Directly on iOS and Mac Catalyst; through the back end on Android | Yes |
| [Facebook](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/facebook.md) | OAuth2 | Yes | Directly or through the back end | Yes |
| [LINE](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/line.md) | OAuth2, with the email address from the OIDC ID token | Yes | Directly on iOS and Mac Catalyst; through the back end on Android | Yes |
| [Microsoft Entra ID](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/microsoft-entra-id.md) | OAuth2 + OIDC | Yes | Directly or through the back end | Yes |
| [Auth0](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/auth0.md) | OAuth2 + OIDC | Yes | Directly or through the back end | Yes |
| [Okta](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/okta.md) | OAuth2 + OIDC | Yes | Directly or through the back end | Yes |
| [GitHub](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/github.md) | OAuth2 | Yes | Through the back end only | Yes |
| [Apple](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/apple.md) | OAuth2 + OIDC | No | Through the back end only | Yes |
| [Any other OIDC provider](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/providers/openid-connect.md), such as Keycloak or GitLab | OAuth2 + OIDC | Yes, when the provider accepts a public client | Directly or through the back end | Yes |

"Through the back end" means that the application signs in through its own ASP.NET Core back end, as the
[.NET MAUI guide](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/maui.md) describes. Each account's
document says what to register with the provider and what it was tested with.

## Packages

| Package | Target frameworks | Use it for |
|---------|-------------------|------------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0, net8.0, net10.0 | The providers, sign-in from desktop, console and .NET MAUI applications, and other server frameworks |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core applications |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net472 | ASP.NET Web Forms and MVC applications on System.Web |

```sh
dotnet add package Polhem.OAuth2
```

The web packages depend on Polhem.OAuth2, so a web application adds only its own package, such as
`dotnet add package Polhem.OAuth2.AspNetCore`.

1.x minor versions may contain breaking changes, which the [changelog](https://github.com/polhem-dev/polhem-oauth2/blob/main/CHANGELOG.md)
marks. Reference the packages with a range that stays within one minor version, for example `[1.4,1.5)`, rather than `1.*`
([ADR-010](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-010-breaking-changes-in-1-x-minors.md)).

## Getting started with Google

### OAuth2 in a desktop application

`LoopbackOAuth2Client` listens on the redirect URI, opens the sign-in in the default browser, and exchanges the code
when Google redirects back. Create an OAuth client of the Desktop app type in the Google Cloud console.

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

### OAuth2 in ASP.NET Core

Create an OAuth client of the Web application type, and register the callback URL as its redirect URI.

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

### OpenID Connect in a web application

`OidcOAuth2Options.FromDiscoveryAsync` reads the endpoints from the provider's discovery document. Call it once, when the
application starts:

```csharp
var keycloak = await OidcOAuth2Options.FromDiscoveryAsync("https://sso.example.com/realms/example");
keycloak.ProviderName = "Keycloak";
keycloak.ClientId = "your-client-id";
keycloak.ClientSecret = "your-client-secret";
keycloak.RedirectUri = "https://localhost:7032/auth/callback";
builder.Services.AddOAuth2Client("Keycloak", keycloak);
```

The controller above works unchanged. For another OpenID Connect provider, such as GitLab or AWS Cognito, pass that
provider's address instead, and give it a `ProviderName` of its own. A provider that has options of its own, such as
Google, signs in with those options instead.

## More documentation

Each account's document is linked from the table in "Supported accounts". The guides cover each kind of application and
the topics they share:

- [Options shared by every provider](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/options.md)
- [Desktop and console applications](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/desktop.md)
- [.NET MAUI applications](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/maui.md)
- [ASP.NET Core](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/aspnetcore.md)
- [ASP.NET (System.Web)](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/aspnet-system-web.md)
- [Web applications: how a sign-in is kept](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/web-sign-in-state.md)
- [Other server frameworks](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/other-server-frameworks.md)
- [Apps with a separate back end](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/separate-back-end.md)
- [Results, tokens and errors](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/results-and-errors.md)
- [Identifying users](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/identifying-users.md)
- [Migrating from Bee.OAuth2](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/migrating-from-bee-oauth2.md)
- [Samples](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/en/guides/samples.md)

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/README.md).

## License

[MIT](https://github.com/polhem-dev/polhem-oauth2/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

Polhem.OAuth2 continues [Bee.OAuth2](https://github.com/jeff377/bee-oauth2).
