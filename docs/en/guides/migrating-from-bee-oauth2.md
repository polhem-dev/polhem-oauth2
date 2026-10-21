# Migrating from Bee.OAuth2

**English** | [繁體中文](../../zh-TW/guides/migrating-from-bee-oauth2.md)

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
- **Redirect URIs** for desktop applications must be registered again as loopback URIs (see
  [Registering a loopback redirect URI](desktop.md#registering-a-loopback-redirect-uri)). URIs that only worked inside an
  embedded browser, such as `https://login.microsoftonline.com/common/oauth2/nativeclient`, no longer work.
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
