# Polhem.OAuth2

**English** | [繁體中文](https://github.com/polhem-dev/polhem-oauth2/blob/main/README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-oauth2/actions/workflows/build-ci.yml)

Lightweight OAuth2 sign-in for .NET. Desktop and console applications sign in through the system browser with a loopback
redirect and PKCE, on Windows, macOS and Linux. .NET MAUI applications on Android, iOS and Mac Catalyst sign in through
`WebAuthenticator`, directly or through their own back end. ASP.NET Core and ASP.NET (System.Web) applications use the
authorization code flow with PKCE, and keep each sign-in in a protected cookie.

Supported providers: Google, Facebook, LINE, Microsoft Entra ID, Auth0 and Okta.

## Packages

| Package | Target frameworks | Use it for |
|---------|-------------------|------------|
| [Polhem.OAuth2](https://www.nuget.org/packages/Polhem.OAuth2) | netstandard2.0, net10.0 | The providers, sign-in from desktop, console and .NET MAUI applications, and other server frameworks |
| [Polhem.OAuth2.AspNetCore](https://www.nuget.org/packages/Polhem.OAuth2.AspNetCore) | net10.0 | ASP.NET Core applications |
| [Polhem.OAuth2.AspNet](https://www.nuget.org/packages/Polhem.OAuth2.AspNet) | net472 | ASP.NET Web Forms and MVC applications on System.Web |

```sh
dotnet add package Polhem.OAuth2
```

## Options

Each provider has its own options type: `GoogleOAuth2Options`, `FacebookOAuth2Options`, `LineOAuth2Options`,
`AzureOAuth2Options` (Microsoft Entra ID), `Auth0OAuth2Options` and `OktaOAuth2Options`.

- `ClientId` and `RedirectUri` are required. A client copies and checks the options when it is created, so later changes
  to them have no effect, and invalid options throw `ArgumentException` right away.
- Auth0 and Okta need `Domain`, such as `your-tenant.auth0.com`. Okta uses the `default` authorization server unless
  `AuthorizationServerId` names another one; an empty value selects the org authorization server. Okta creates the `default`
  server only in an org of the Integrator Free Plan or with API Access Management, so another org sets the empty value.
- Microsoft Entra ID uses the `common` tenant. An application registered for a single tenant sets `Tenant` to the tenant
  ID or domain name.
- Every endpoint must be an absolute `https` URI without a fragment. A query is kept, so an endpoint can carry a parameter of the
  provider, such as `https://tenant.auth0.com/authorize?audience=...`.
- `UsePkce` is `true` by default.

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

if (result.IsSuccess)
    Console.WriteLine($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception.Message}");
```

- The redirect URI must be an `http` URI on `localhost` or a loopback address, and it must be registered with the provider.
  Port 0 picks a free port for each sign-in, which only works with providers that accept any loopback port.
- The client always uses PKCE. A client secret shipped with a desktop application can be extracted, so it is not sent,
  except to Google.
- A timeout (`Timeout`, 5 minutes by default), cancellation, or an error from the provider becomes a failed result. A port
  that cannot be listened on throws `SocketException`, and a missing default browser throws `Win32Exception`
  (`PlatformNotSupportedException` on iOS, which cannot start a process).
- Set `OpenBrowser` to open the URL another way, for example `uri => launcher.LaunchUriAsync(uri)` with the `ILauncher` of
  Avalonia, or `url => Launcher.Default.OpenAsync(url)` in .NET MAUI.
- The snippet uses top-level statements. The [OAuthWinForms](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthWinForms)
  sample shows the same sign-in in a Windows Forms application on .NET Framework.
- An application that targets .NET Framework 4.7.2 and runs on a machine with FIPS mode enabled needs the setting described
  under "Before deploying" in the ASP.NET (System.Web) section.
- To sign in to a separate back end as well, see the "Apps with a separate back end" section.

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
| LINE | LINE Login channel, Callback URL | `http://localhost:53682/callback` | Must match. The channel must include the Mobile app type: a channel of the Web app type alone requires the client secret, which a public client does not send |
| Facebook | Facebook Login, Valid OAuth Redirect URIs | `http://localhost:53682/callback` | Register the port you use. Facebook accepted PKCE without the client secret in the tests of ADR-004 and ADR-006, but does not document it |

- Google: the tested client had its redirect URIs registered. Google's documentation differs on whether a desktop app needs
  them, and lists the client secret as optional for installed applications.
- Okta: the authorization server needs an access policy with a rule that allows the authorization code grant. Without
  one, the sign-in fails with a policy evaluation error.
- Facebook refused `127.0.0.1`; use `localhost`. Whether the tested app was in development or live mode was not recorded.
- A redirect URI without a port means port 80, which usually needs administrator rights to listen on. Name the port.

## .NET MAUI applications

An application on Android, iOS or Mac Catalyst signs in in one of two ways
([ADR-006](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-006-app-sign-in.md)):

- **Directly**, for an application without a back end of its own. `AppOAuth2Client` opens the sign-in with
  `WebAuthenticator`, the provider redirects to a URI scheme of the application, and the client exchanges the code with
  PKCE and without a client secret.
- **Through the back end**, for an application that signs in to its own ASP.NET Core back end. The back end signs in as a
  web client and gives the application a single-use code, which the application redeems for the user information. Google
  and LINE accept no direct redirect to an Android application, so on Android they need this way.

`WebAuthenticator` does not work on Windows, so the Windows platform of a MAUI application signs in with
`LoopbackOAuth2Client`, as a desktop application does.

### Signing in directly

```csharp
using Polhem.OAuth2;

var options = new Auth0OAuth2Options
{
    Domain = "your-tenant.auth0.com",
    ClientId = "your-native-client-id",
    RedirectUri = "com.example.app:/oauth2redirect"
};

var client = new AppOAuth2Client(options, async (url, redirectUri, cancellationToken) =>
    (await WebAuthenticator.Default.AuthenticateAsync(url, redirectUri)).CallbackUri);

AuthorizationResult result = await client.SignInAsync();
```

- The redirect URI is a custom scheme or an `https` URI. `http`, `javascript`, `data` and `file` URIs, relative URIs, and
  URIs with a fragment are rejected.
- Each provider needs its own redirect form and registration, such as `fb<app id>://authorize` for Facebook and
  `line3rdp.<bundle id>://auth` for LINE on iOS. Microsoft Entra ID registers a custom scheme only with two slashes, as
  `<scheme>://<path>`. The table in [ADR-006](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-006-app-sign-in.md) lists them with the platforms tested.
- Set no client secret: anything shipped with an application can be extracted.
- Closing the sign-in becomes a failed result with `OperationCanceledException`, and an error from the provider, such as
  `access_denied`, one with `OAuth2Exception`.
- The user information stays in the application. It is no proof of identity for a server, so an application that signs in
  to its own back end signs in through the back end.

### Platform settings

The application must receive the redirect to its scheme:

- iOS and Mac Catalyst: list the scheme under `CFBundleURLTypes` in `Info.plist`. A Mac Catalyst application in the App
  Sandbox also needs the `com.apple.security.network.client` entitlement.
- Android: add an activity that derives from `WebAuthenticatorCallbackActivity`, with an intent filter for the scheme, and
  declare the Custom Tabs service under `<queries>` in `AndroidManifest.xml`, which Android 11 and later require to open it.

```csharp
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "com.example.app")]
public class CallbackActivity : WebAuthenticatorCallbackActivity
{
}
```

```xml
<queries>
  <intent>
    <action android:name="android.support.customtabs.action.CustomTabsService" />
  </intent>
</queries>
```

### Signing in through the back end

The back end registers its web clients as in the ASP.NET Core section, and the redirect URIs of its applications:

```csharp
builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("com.example.app:/relay"));
```

- The application creates a PKCE code verifier and opens a URL of the back end with its redirect URI and the S256 code
  challenge. That endpoint calls `oauth2Manager.TryRedirectToAppAuthorization(HttpContext, "Google", redirectUri, codeChallenge)`
  and answers with status 400 when it returns false: anyone can open the endpoint with any values, so a redirect URI that is
  not registered is an ordinary request, not an exception.
- The provider returns to the back end's usual callback. After `CompleteAuthorizationAsync`,
  `oauth2Manager.RedirectToAppAsync(HttpContext, result, cancellationToken)` sends a sign-in that an application started
  back to the application with a single-use code and returns true. For a sign-in in the browser it returns false. When it
  returns true, return without signing the user in to the web application: the browser session of the sign-in can share its
  cookies with the browser of the device.
- The application posts the code and its verifier to the back end, where `oauth2Manager.RedeemAppCodeAsync` returns the
  user information, or null. The back end then issues the application's own session; the provider's tokens stay on the
  back end.
- The codes are kept in `IDistributedCache`. With several servers, use a distributed cache and share the data protection
  keys, as for web sign-ins. The keys also protect the user information that the cache holds until the code is redeemed.
- The [OAuthAspNetCore](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthAspNetCore) and
  [OAuthMaui](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthMaui) samples show both sides.

## ASP.NET Core

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

- `AddOAuth2Client` registers the client, `OAuth2Manager` and ASP.NET Core data protection. The client is created by the
  call, so invalid options stop the application at startup. It takes an optional `HttpClient`, which the client keeps for
  the life of the application, so one from `IHttpClientFactory` would outlive its handler. Pass none unless the application
  needs its own: the default replaces its pooled connections regularly to follow DNS changes, which `new HttpClient()` does not.
- `oauth2Manager.GetClient("Google")` returns the client, for example to call `RefreshTokenAsync`.
- ASP.NET Core has an `AuthorizationResult` of its own, in `Microsoft.AspNetCore.Authorization`, the namespace of `[Authorize]`.
  A file that imports both namespaces and names the type gets error CS0104. Declare the variable with `var`, or add
  `using AuthorizationResult = Polhem.OAuth2.AuthorizationResult;`.

## ASP.NET (System.Web)

There is no runnable sample for System.Web, because its projects cannot be built with the `dotnet` CLI. The package is
used the same way, through the static `OAuth2Manager`:

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.AspNet;

// Global.asax.cs
protected void Application_Start()
{
    OAuth2Manager.RegisterClient("Google", new GoogleOAuth2Options
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "https://localhost:44300/auth/callback"
    });
}
```

```csharp
// An MVC controller. In Web Forms, call OAuth2Manager.RedirectToAuthorization("Google") from the sign-in page and return,
// and await OAuth2Manager.CompleteAuthorizationAsync() on the callback page, which needs Async="true".
public class AuthController : Controller
{
    public ActionResult Login()
    {
        return Redirect(OAuth2Manager.CreateAuthorizationUrl(HttpContext, "Google"));
    }

    public async Task<ActionResult> Callback()
    {
        AuthorizationResult result = await OAuth2Manager.CompleteAuthorizationAsync(HttpContext);
        if (result.IsSuccess)
            return Content(result.UserInfo.UserId + " " + result.UserInfo.UserName + " " + result.UserInfo.Email);

        return Content("The sign-in failed: " + result.Exception.Message);
    }
}
```

Before deploying:

- Target .NET Framework 4.7.2 or later, and set `<httpRuntime targetFramework="4.7.2" />`, or your later version, in
  `web.config`. Asynchronous pages and the operating system's TLS defaults depend on it.
- On .NET Framework the core package depends on System.Text.Json. Keep the binding redirects that NuGet adds for it and its
  dependencies in `web.config`.
- When more than one server can receive the callback, set the same `<machineKey>` in `web.config` on each of them.
- On .NET Framework the default `HttpClient` keeps a connection for as long as the provider allows, and does not follow a DNS
  change until then. An application that runs for a long time can set `ConnectionLeaseTimeout` on the `ServicePoint` of the token
  endpoint, or pass its own `HttpClient`.
- On a machine with FIPS mode enabled, an application that targets .NET Framework 4.7.2 can get a `CryptographicException`
  when a sign-in starts: for such applications .NET Framework blocks the managed SHA-256 implementation that PKCE uses.
  Target .NET Framework 4.8 or later, or set the `Switch.System.Security.Cryptography.UseLegacyFipsThrow` switch to `false`.
  See [Managed cryptography classes do not throw a CryptographyException in FIPS mode](https://learn.microsoft.com/dotnet/framework/migration-guide/retargeting/4.8.x#managed-cryptography-classes-do-not-throw-a-cryptographyexception-in-fips-mode).

## Web applications: how a sign-in is kept

- Each sign-in keeps its state, PKCE code verifier, redirect URI and client name in a cookie of its own, encrypted and
  authenticated with ASP.NET Core data protection or `MachineKey`. No session state is needed, and sign-ins started in
  several tabs do not replace each other. See [ADR-005](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-005-web-sign-in-cookie.md).
- The cookie name starts with `__Host-`, and the cookie is `Secure`, HTTP-only and `SameSite=Lax`, so the sign-in must
  start and end on HTTPS pages.
- A sign-in must complete within 10 minutes. The callback removes the cookie before it exchanges the code.
- Every server that can receive the callback must be able to decrypt the cookie: share the data protection key ring in
  ASP.NET Core, or use the same machine key on System.Web.

## Other server frameworks

`OAuth2Client` in the core package runs the same flow without an HTTP framework. Keep the pending values where only the
browser that started the sign-in can present them, such as an encrypted, HTTP-only cookie, and remove them when the
callback is handled.

```csharp
var client = new OAuth2Client(options);

// Start the sign-in. Keep and Redirect stand for code of your framework.
AuthorizationRequest request = client.CreateAuthorizationRequest();
Keep(request.Pending.State, request.Pending.CodeVerifier, request.Pending.RedirectUri);
Redirect(request.Url);

// Complete it in the callback.
var pending = new PendingAuthorization(keptState, keptCodeVerifier, keptRedirectUri);
var callback = new AuthorizationCallback(query["code"], query["state"], query["error"], query["error_description"]);
AuthorizationResult result = await client.CompleteAuthorizationAsync(callback, pending, cancellationToken);
```

## Results, tokens and errors

- A successful result has `ProviderName`, `UserInfo` and `Token`; a failed result has `Exception`.
- `Token` holds the access token, and the refresh token, ID token, lifetime and scopes when the provider returns them.
  `RefreshTokenAsync` on a client obtains new tokens. Keep the refresh token of the latest response, because some providers
  issue a new one each time. Whether a provider issues one depends on the request: Microsoft Entra ID, Auth0 and Okta issue
  one when `Scopes` includes `offline_access` (Auth0 and Okta also need it allowed for the application); Google issues one
  to a desktop or mobile application, and to a web application only when `AuthorizationEndpoint` carries
  `?access_type=offline&prompt=consent`; LINE issues one always; Facebook never.
- `Exception` holds only the failures a sign-in is expected to produce, such as a failed HTTP request, a state that does
  not match, or an error from the provider. Configuration and programming errors, such as an unregistered client name, are
  thrown. See [ADR-003](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-003-exception-semantics.md).
- An error from the provider is an `OAuth2Exception`: `Error` holds the error code, such as `access_denied`, and
  `ErrorDescription` the provider's text. Anyone who sends the user a link can set the values of an error in a redirect,
  so encode them before showing them. The token endpoint of Facebook reports a Graph API error instead: `Error` holds its
  numeric code, such as `100`, and `ErrorDescription` its message.

## Identifying users

- Identify a user by the provider name together with `UserInfo.UserId`, not by `Email`: an address can change, and
  providers differ in whether they verify it.
- The provider name is `AuthorizationResult.ProviderName`: `Google`, `Facebook`, `LINE`, `Azure` (Microsoft Entra ID),
  `Auth0` or `Okta`. It does not depend on the name a client is registered under.
- For Microsoft Entra ID, `UserId` is the `sub` claim, which is different for each application the user signs in to. The `oid`
  claim of `Token.IdToken` is the same in every application, for an application that needs that; validate the token first.
- For Facebook, `UserId` is also different for each app. For LINE it is the same in every channel of one provider.
- LINE returns the email address only in the ID token, and only when the channel may read it and the user agreed. The
  library reads it from the ID token that the token endpoint returned, and checks that the token was issued to the client,
  but does not check its signature.
- The library does not validate ID tokens. `Token.IdToken` is returned as the provider sent it; validate it before relying
  on its claims.

## Apps with a separate back end

For brevity, the desktop example signs in and reads the user information in one call, which suits an application that
uses the result itself. When the front end signs in and then signs in to a separate back end, do not send the user
information from the front end to the back end as proof of identity: the back end cannot tell whether it was forged.

- After signing in, the front end passes the token to the back end over HTTPS, and the back end obtains the user
  information from the provider with that token.
- Before the back end trusts the token, it confirms that the token was issued to its own client ID, for example through
  the provider's token verification endpoint or by validating the signature and audience of the ID token. A request to
  the user information endpoint alone does not confirm this: a token that another application obtained for the same
  user returns the same user.
- The library does not provide these back-end steps. A .NET MAUI application can sign in through its ASP.NET Core back
  end instead; see the .NET MAUI section.

## Migrating from Bee.OAuth2

| Bee.OAuth2 package | Replacement |
|--------------------|-------------|
| `Bee.OAuth2` | `Polhem.OAuth2` |
| `Bee.OAuth2.AspNet` | `Polhem.OAuth2.AspNet` |
| `Bee.OAuth2.AspNetCore` | `Polhem.OAuth2.AspNetCore` |
| `Bee.OAuth2.WinForms`, `Bee.OAuth2.Desktop` | `LoopbackOAuth2Client` in `Polhem.OAuth2` |

- **Namespaces**: `Bee.OAuth2` becomes `Polhem.OAuth2`, and so on for each package.
- **Web registration**: ASP.NET Core registers each client with `AddOAuth2Client`, and System.Web with
  `OAuth2Manager.RegisterClient(name, options)`. Session state and `OAUTH2_STATE_KEY` are no longer used.
- **Web methods**: `GetAuthorizationUrl` becomes `CreateAuthorizationUrl`, and `ValidateAuthorization` becomes
  `CompleteAuthorizationAsync`. In ASP.NET Core they take the `HttpContext`. A sign-in started before the upgrade does
  not complete after it; the user signs in again.
- **Desktop sign-in** moves from an embedded WebView2 window to the system browser. The synchronous `Authorization()`,
  `Caption`, `Width`, `Height`, `AuthorizationForm`, and the desktop `OAuth2Client`, `OAuth2Manager` and `StateStorage`
  are gone; call `LoopbackOAuth2Client.SignInAsync` instead.
- **Redirect URIs** for desktop applications must be registered again as loopback URIs (see the table above). URIs that
  only worked inside an embedded browser, such as `https://login.microsoftonline.com/common/oauth2/nativeclient`, no longer work.
- **Results and tokens**: `AuthorizationResult` is read-only. `AccessToken` becomes `Token.AccessToken`, and refreshing
  moves to `RefreshTokenAsync` on the client, which returns a `TokenResponse`.
- **Types that are no longer public**: the provider classes, `IOAuth2Provider`, `BaseOAuth2Client`, `IStateStorage`,
  `PkceHelper` and `OAuth2StateCryptor`. Applications use an options type with a client or a manager.
- **PKCE and the client secret**: `UsePkce` is `true` by default. A web client sends the client secret whenever it is
  set, also with PKCE.
- **Endpoints** must be `https`. `Domain` of Auth0 and Okta takes a host name, with or without `https://`.
- **Exceptions**: `AuthorizationResult.Exception` no longer collects unexpected exceptions; they are thrown.
- **JSON null**: a user information field whose value is JSON null is now `null`; Bee.OAuth2 returned an empty string.
  Fallback fields therefore take effect, such as `nickname` when Auth0 returns `name` as JSON null.
- **Target frameworks**: `Polhem.OAuth2.AspNet` targets .NET Framework 4.7.2, and `Polhem.OAuth2.AspNetCore` targets net10.0.
- **Dependencies**: no `Bee.Base` or `Newtonsoft.Json`. JSON is parsed with System.Text.Json, which the netstandard2.0
  build references as a package.

## Samples

The samples and the loopback redirect probe read their provider settings from one `OAuthConfig.json` in the repository
root. Copy `OAuthConfig.example.json` to `OAuthConfig.json` and fill it in; the build copies the file to the output
folder of each project. `OAuthConfig.json` is ignored by git; keep credentials out of `OAuthConfig.example.json`.

Every provider holds one client per client type, because a provider registers desktop, web and mobile clients
separately:

```json
{
  "Providers": {
    "Okta": {
      "Domain": "",
      "Desktop": { "ClientId": "", "RedirectUri": "http://localhost:53682/callback" },
      "Web": { "ClientId": "", "ClientSecret": "", "RedirectUri": "https://localhost:7032/auth/callback" }
    }
  }
}
```

- The console and Windows Forms samples and the probe read `Desktop`, and the ASP.NET Core sample reads `Web`. A client
  section takes the properties of that provider's options type, such as `Scopes` and `UsePkce`, and what it leaves out
  keeps the default of the type.
- The fields both clients share, none of which are credentials, sit in the provider section: `Domain` for Auth0 and
  Okta, `AuthorizationServerId` for Okta, and `Tenant` for Azure. Anything else there is an error, so credentials cannot
  end up shared by accident.
- A provider whose back end registers one client for both, such as a LINE channel with two callback URLs, gets the same
  `ClientId` and `ClientSecret` in both sections.
- A section whose `ClientId` is still empty is skipped, so the samples offer only the providers you filled in.
- The OAuthMaui sample reads `iOS` on iOS and Mac Catalyst, `Android` on Android, and `Desktop` on Windows. An `iOS` or
  `Android` section cannot hold a `ClientSecret`, because an application cannot keep one: loading such a file fails. The
  build of OAuthMaui packages only `ClientId`, `RedirectUri`, `Scopes`, `UsePkce` and the shared fields, never a secret.
  Google and LINE refuse a direct redirect to an Android application
  ([ADR-006](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/adr-006-app-sign-in.md)), so they have no
  `Android` section, and the sample signs in to them through the back end there.
- The top-level `AppRelay` section configures the back-end relay: `BackendUrl` is where the ASP.NET Core sample runs, and
  `RedirectUri` is the relay callback of the application, which the ASP.NET Core sample registers with
  `AddOAuth2AppRelay`. The relay runs over HTTPS, so the simulator or emulator must trust the ASP.NET Core development
  certificate. On the Android emulator, run `adb reverse tcp:7032 tcp:7032` so that `localhost` reaches the host.

| Sample | Shows |
|--------|-------|
| [OAuthConsole](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthConsole) | Desktop sign-in from a console application, on any operating system |
| [OAuthDesktop](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthDesktop) | Desktop sign-in from Windows Forms on .NET |
| [OAuthWinForms](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthWinForms) | Desktop sign-in from Windows Forms on .NET Framework 4.8 |
| [OAuthAspNetCore](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthAspNetCore) | ASP.NET Core, including the relay endpoints for OAuthMaui |
| [OAuthMaui](https://github.com/polhem-dev/polhem-oauth2/tree/main/samples/OAuthMaui) | .NET MAUI: direct and back-end relay sign-in on Android, iOS and Mac Catalyst, loopback sign-in on Windows. It needs the MAUI workload and is not part of the solution. |

[LoopbackRedirectProbe](https://github.com/polhem-dev/polhem-oauth2/tree/main/tools/LoopbackRedirectProbe) checks whether
a provider accepts a loopback redirect URI before you build on it.

## Design decisions

The reasons behind the design are recorded in the
[architecture decision records](https://github.com/polhem-dev/polhem-oauth2/blob/main/docs/adr/README.md).

## License

[MIT](https://github.com/polhem-dev/polhem-oauth2/blob/main/LICENSE.txt). Copyright (c) Polhem contributors.

Polhem.OAuth2 continues [Bee.OAuth2](https://github.com/jeff377/bee-oauth2).
