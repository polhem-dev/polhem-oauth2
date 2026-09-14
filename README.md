# Polhem.OAuth2

**English** | [繁體中文](https://github.com/polhem-dev/polhem-oauth2/blob/main/README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)

Lightweight OAuth2 sign-in for .NET. Desktop and console applications sign in through the system browser with a loopback
redirect and PKCE, on Windows, macOS and Linux. ASP.NET Core and ASP.NET (System.Web) applications use the authorization
code flow with the state kept in a cookie.

Supported providers: Google, Facebook, LINE, Microsoft Entra ID, Auth0 and Okta.

## Packages

| Package | Target frameworks | Use it for |
|---------|-------------------|------------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0, net10.0 | The providers, and sign-in from desktop and console applications |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core applications |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net48 | ASP.NET Web Forms and MVC applications on System.Web |

```sh
dotnet add package Polhem.OAuth2
```

Each provider has its own options type: `GoogleOAuth2Options`, `FacebookOAuth2Options`, `LineOAuth2Options`,
`AzureOAuth2Options` (Microsoft Entra ID), `Auth0OAuth2Options` and `OktaOAuth2Options`. Auth0 and Okta also need
`Domain`, and Okta takes an optional `AuthorizationServerId`.

## Desktop and console applications

`LoopbackOAuth2Client` listens on the redirect URI, opens the authorization URL in the default browser, waits for the
provider to redirect back, and exchanges the authorization code.

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

if (result.IsSuccess && result.UserInfo is { } user)
    Console.WriteLine($"{user.UserId} {user.UserName} {user.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception?.Message}");
```

- The redirect URI must be an `http` URI on `localhost` or a loopback address, and it must be registered with the provider.
  Port 0 picks a free port for each sign-in, which only works with providers that accept any loopback port.
- The client always uses PKCE. A client secret shipped with a desktop application can be extracted, so it is only sent to
  Google, which requires it.
- A timeout (`Timeout`, 5 minutes by default), cancellation, or an error from the provider becomes a failed result. A port
  that cannot be listened on throws `SocketException`.
- Set `OpenBrowser` to open the URL another way, for example through the launcher of a UI framework.

The reasons behind this design, and how each provider handled loopback redirects, are recorded in
[ADR-004](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-004-system-browser-loopback.md).

### Registering a loopback redirect URI

These registrations were tested with each provider on 2026-09-14.

| Provider | Application type | Redirect URI | Port |
|----------|------------------|--------------|------|
| Google | Desktop app | `http://127.0.0.1:0/callback` | Any port was accepted |
| Microsoft Entra ID | Mobile and desktop applications, registered as `http://localhost` | `http://localhost:0` | Ignored |
| Auth0 | Native | `http://127.0.0.1:53682/callback` | Must match |
| Okta | Native, with client authentication None and PKCE required | `http://localhost:53682/callback` | Must match |
| LINE | LINE Login channel, Callback URL | `http://localhost:53682/callback` | Must match |
| Facebook | Facebook Login, Valid OAuth Redirect URIs | `http://localhost:53682/callback` | Register the port you use |

- Okta: the authorization server needs an access policy with a rule that allows the authorization code grant. Without
  one, the sign-in fails with a policy evaluation error.
- Facebook refused `127.0.0.1`; use `localhost`.
- A redirect URI without a port means port 80, which usually needs administrator rights to listen on. Name the port.

## ASP.NET Core

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();
builder.Services.AddSingleton(provider =>
{
    var accessor = provider.GetRequiredService<IHttpContextAccessor>();
    var options = new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:7032/auth/callback",
        UsePkce = true
    };

    var manager = new OAuth2Manager(accessor);
    manager.RegisterClient("Google", new OAuth2Client(options, accessor));
    return manager;
});

var app = builder.Build();
app.UseSession();
```

```csharp
public class AuthController(OAuth2Manager oauth2Manager) : Controller
{
    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        oauth2Manager.RedirectToAuthorization("Google");
        return new EmptyResult();
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback()
    {
        AuthorizationResult result = await oauth2Manager.ValidateAuthorization();
        return result.IsSuccess && result.UserInfo is { } user
            ? Content($"{user.UserId} {user.UserName} {user.Email}")
            : Content($"The sign-in failed: {result.Exception?.Message}");
    }
}
```

## ASP.NET (System.Web)

There is no runnable sample for System.Web, because its projects cannot be built with the `dotnet` CLI. The package is
used the same way, through the static `OAuth2Manager`:

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNet;

// Global.asax.cs
protected void Application_Start()
{
    var options = new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:44300/auth/callback",
        UsePkce = true
    };
    OAuth2Manager.RegisterClient("Google", new OAuth2Client(options));
}
```

```csharp
// An MVC controller. In Web Forms, call OAuth2Manager.RedirectToAuthorization("Google") from the sign-in page,
// and await OAuth2Manager.ValidateAuthorization() on the callback page, which needs Async="true".
public class AuthController : Controller
{
    public ActionResult Login()
    {
        return Redirect(OAuth2Manager.GetAuthorizationUrl("Google"));
    }

    public async Task<ActionResult> Callback()
    {
        AuthorizationResult result = await OAuth2Manager.ValidateAuthorization();
        if (result.IsSuccess && result.UserInfo != null)
            return Content($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");

        return Content($"The sign-in failed: {result.Exception?.Message}");
    }
}
```

## Web applications: state cookie, session and key

- The state is kept in a cookie marked `Secure`, so the callback must be served over HTTPS.
- With `UsePkce`, the code verifier is kept in session state, so session state must be enabled.
- The state carries the name the client was registered under. Set the `OAUTH2_STATE_KEY` environment variable to encrypt
  and authenticate it with AES-CBC and HMAC-SHA256. Without the key, the name is only base64-encoded, which neither hides
  it nor detects tampering.

`OAUTH2_STATE_KEY` is a base64-encoded 64-byte random key. It is read once per process, so set it before the application
starts, and use the same key on every server that can receive the callback. To generate one:

```sh
openssl rand 64 | openssl base64 -A
```

```csharp
Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)));
```

## Results and errors

`AuthorizationResult.Exception` holds only the failures an OAuth2 sign-in is expected to produce, such as a failed HTTP
request, an invalid state, or an error from the provider. Configuration and programming errors, such as an unregistered
client name, are thrown. See [ADR-003](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-003-exception-semantics.md).

## Migrating from Bee.OAuth2

| Bee.OAuth2 package | Replacement |
|--------------------|-------------|
| `Bee.OAuth2` | `Polhem.OAuth2` |
| `Bee.OAuth2.AspNet` | `Polhem.OAuth2.AspNet` |
| `Bee.OAuth2.AspNetCore` | `Polhem.OAuth2.AspNetCore` |
| `Bee.OAuth2.WinForms`, `Bee.OAuth2.Desktop` | `LoopbackOAuth2Client` in `Polhem.OAuth2` |

- **Namespaces**: `Bee.OAuth2` becomes `Polhem.OAuth2`, and so on for each package.
- **Desktop sign-in** moves from an embedded WebView2 window to the system browser. The synchronous `Authorization()`,
  `Caption`, `Width`, `Height`, `AuthorizationForm`, and the desktop `OAuth2Client`, `OAuth2Manager` and `StateStorage`
  are gone; call `LoopbackOAuth2Client.SignInAsync` instead.
- **Redirect URIs** for desktop applications must be registered again as loopback URIs (see the table above). URIs that
  only worked inside an embedded browser, such as `https://login.microsoftonline.com/common/oauth2/nativeclient`, no longer work.
- **Exceptions**: `AuthorizationResult.Exception` no longer collects unexpected exceptions; they are thrown.
- **JSON null**: a user information field whose value is JSON null is now `null`; Bee.OAuth2 returned an empty string.
  Fallback fields therefore take effect, such as `sub` when Entra ID returns `oid` as JSON null.
- **Target frameworks**: `Polhem.OAuth2.AspNetCore` targets net10.0 (Bee.OAuth2.AspNetCore targeted net8.0).
- **Dependencies**: no `Bee.Base` or `Newtonsoft.Json`. JSON is parsed with System.Text.Json.
- **State key**: the encrypted state format is unchanged, so an existing `OAUTH2_STATE_KEY` keeps working. See
  [ADR-001](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-001-drop-bee-base.md).

## Samples

Each sample reads its provider settings from `OAuthConfig.json`.

| Sample | Shows |
|--------|-------|
| [OAuthConsole](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthConsole) | Desktop sign-in from a console application, on any operating system |
| [OAuthDesktop](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthDesktop) | Desktop sign-in from Windows Forms on .NET |
| [OAuthWinForms](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthWinForms) | Desktop sign-in from Windows Forms on .NET Framework 4.8 |
| [OAuthAspNetCore](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthAspNetCore) | ASP.NET Core |

[LoopbackRedirectProbe](https://github.com/polhem-dev/polhem-oauth2/tree/main/tools/LoopbackRedirectProbe) checks whether
a provider accepts a loopback redirect URI before you build on it.

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/README.md).

## License

[MIT](https://github.com/polhem-dev/polhem-oauth2/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

Polhem.OAuth2 continues [Bee.OAuth2](https://github.com/jeff377/bee-oauth2).
