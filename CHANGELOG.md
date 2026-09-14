# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to Polhem.OAuth2, Polhem.OAuth2.AspNet and Polhem.OAuth2.AspNetCore. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-09-14

The first release under the Polhem name. It continues [Bee.OAuth2](https://github.com/jeff377/bee-oauth2); the changes
below are relative to the last Bee.OAuth2 release. How to move an application over is described in
[Migrating from Bee.OAuth2](README.md#migrating-from-beeoauth2).

### Added

- `LoopbackOAuth2Client` signs in from desktop and console applications through the system browser and a loopback
  redirect, always with PKCE, on Windows, macOS and Linux. See [ADR-004](docs/adr/adr-004-system-browser-loopback.md).
- `OAuth2Client` runs the authorization code flow without an HTTP framework, in two steps: `CreateAuthorizationRequest`
  and `CompleteAuthorizationAsync`.
- `TokenResponse` holds the access, refresh and ID tokens with their type, lifetime and scopes. `AuthorizationResult.Token`
  carries it, and `RefreshTokenAsync` on each client obtains new tokens.
- `AddOAuth2Client` registers a client, `OAuth2Manager` and data protection in an ASP.NET Core application.
- `OAuth2Exception`, thrown for protocol failures. Its `Error` and `ErrorDescription` carry the error code and description
  returned by the provider.
- `AzureOAuth2Options.Tenant`, for applications registered in a single Microsoft Entra ID tenant.
- The Okta provider (`OktaOAuth2Options`). An empty `AuthorizationServerId` selects the org authorization server.
- The LINE provider reads the email address from the ID token.
- Every client accepts an `HttpClient`, and asynchronous methods take a `CancellationToken`.
- A net10.0 target for Polhem.OAuth2, which has no package dependencies.
- Nullable reference type annotations on the public API.

### Changed

- Package IDs and namespaces are renamed from `Bee.OAuth2` to `Polhem.OAuth2`.
- The web packages keep each sign-in in a cookie of its own, protected with ASP.NET Core data protection or `MachineKey`,
  and no longer use session state or the `OAUTH2_STATE_KEY` environment variable. See
  [ADR-005](docs/adr/adr-005-web-sign-in-cookie.md).
- `OAuth2Manager` provides `CreateAuthorizationUrl`, `RedirectToAuthorization`, `CompleteAuthorizationAsync` and
  `GetClient`. The ASP.NET Core methods take the `HttpContext`, and the System.Web `RegisterClient` takes the options.
- `UsePkce` is `true` by default.
- A web client sends the client secret whenever one is set, also with PKCE. A loopback client sends it only to Google.
- A client copies and checks its options when it is created. Endpoints must be absolute https URIs, and `Domain` of Auth0
  and Okta accepts only https.
- `AuthorizationResult` and `UserInfo` are sealed and read-only, and `AuthorizationResult.AccessToken` is replaced by `Token`.
- Polhem.OAuth2.AspNet targets .NET Framework 4.7.2 instead of 4.8.
- Polhem.OAuth2.AspNetCore targets net10.0 instead of net8.0, and references the ASP.NET Core shared framework instead
  of the `Microsoft.AspNetCore.Http.Abstractions` package.
- JSON is parsed with System.Text.Json, which the netstandard2.0 build references as a package. The packages no longer
  depend on `Bee.Base` or `Newtonsoft.Json`. See [ADR-001](docs/adr/adr-001-drop-bee-base.md).
- A user information field whose value is JSON null is now `null` instead of an empty string, so fallback fields take effect.
- Facebook uses Graph API v26.0, and Google uses its current authorization and user information endpoints.
- `AuthorizationResult.Exception` holds only the failures an OAuth2 sign-in is expected to produce; other exceptions are
  thrown. An error response from the token endpoint becomes an `OAuth2Exception`. Exception messages include the HTTP
  status code but not the response body. See [ADR-003](docs/adr/adr-003-exception-semantics.md).
- The Microsoft Entra ID user identifier is read from `sub`, which the OpenID Connect user information endpoint returns.

### Removed

- `Bee.OAuth2.WinForms` and `Bee.OAuth2.Desktop`, together with the embedded WebView2 sign-in window. Use
  `LoopbackOAuth2Client` instead.
- The provider classes, `IOAuth2Provider`, `BaseOAuth2Client`, `IStateStorage`, `PkceHelper`, `OAuth2StateCryptor`, and
  the `OAuth2Client` and `StateStorage` types of the web packages. Applications use an options type with a client or a manager.

### Security

- Each sign-in has a new random state and PKCE code verifier, and web applications keep them encrypted and authenticated
  until the callback. The sign-in cookie uses the `__Host-` prefix and is Secure, HTTP-only and SameSite=Lax.
- The loopback listener ends the wait only for a request that names the redirect URI's host and carries the state of the
  sign-in, and it reads connections side by side.
- Provider endpoints must use https.

[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
