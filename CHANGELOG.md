# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to Polhem.OAuth2, Polhem.OAuth2.AspNet and Polhem.OAuth2.AspNetCore. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `OAuth2Options.IsAppRedirectUri` checks whether a redirect URI can return a sign-in to an application. It is the rule that
  `AppOAuth2Client` already applied, and the back-end relay of Polhem.OAuth2.AspNetCore now calls it for
  `OAuth2AppRelayOptions.AppRedirectUris` instead of repeating it.
- `OAuth2Manager.TryRedirectToAppAuthorization` starts a relayed sign-in with values that come from the request, and returns false
  for a client name, a redirect URI or a code challenge that is not valid, instead of throwing. An endpoint that an application
  opens no longer has to catch exceptions to answer with status 400.
- `OAuth2Client.Create(options, httpClientFactory)` creates a client that asks for the `HttpClient` of each request to the
  provider, and `AddOAuth2ClientWithHttpClientFactory` registers one in ASP.NET Core with an `HttpClient` from the service
  provider, such as one from `IHttpClientFactory`, whose handler rotation is honored that way.
- `OAuth2Manager.CreateAppAuthorizationUrl`, `TryCreateAppAuthorizationUrl` and `CreateAppRedirectUrlAsync` return the URL
  of a relayed sign-in instead of redirecting the response, as `CreateAuthorizationUrl` does for a web sign-in, for a caller
  that redirects in its own way, such as a minimal API. The methods that redirect remain.

### Changed

- `OAuth2Manager.RedirectToAppAsync` throws `InvalidOperationException` when the sign-in names an application redirect URI
  that is no longer in `OAuth2AppRelayOptions.AppRedirectUris`, instead of redirecting to it. It also stops when the callback
  request is aborted, as `CompleteAuthorizationAsync` does, so passing `HttpContext.RequestAborted` is no longer needed.
- `OAuth2AppRelayOptions.CodeLifetime` can be at most 10 minutes, which RFC 6749, section 4.1.2, recommends for an authorization
  code. A longer value is rejected by `AddOAuth2AppRelay`.
- An endpoint with a fragment is rejected when the client is created, as RFC 6749, section 3.1, requires. Such an endpoint never
  worked, because the fragment swallowed the parameters added after it.
- `UserInfo.UserName` falls back to the given and family names joined for every OpenID Connect provider when the response
  has no name, as it already did for Microsoft Entra ID; Auth0 falls back to the nickname and Okta to the preferred user
  name only after that. Facebook asks the Graph API for `first_name` and `last_name` as well, and joins them when `name`
  is missing, so `UserInfo.RawJson` from Facebook now includes those fields.

### Fixed

- The query of `AuthorizationEndpoint` is kept, and the parameters of the authorization request are added after it, as RFC 6749,
  section 3.1, requires. An endpoint such as `https://tenant.auth0.com/authorize?audience=...` produced a URL with two question
  marks, which the provider refused. The same applies to the `UserInfoEndpoint` of Facebook.
- An error from the token endpoint of Facebook becomes an `OAuth2Exception`, as for every other provider. Facebook reports a
  Graph API error object, which was read as a response without an error code and thrown as an `HttpRequestException`. `Error`
  holds the numeric code of the Graph API error, and `ErrorDescription` its message.
- A relay code is no longer refused early on a server whose clock runs up to a minute ahead of the server that issued it. The
  cache still ends the lifetime of the code at `CodeLifetime`.

### Security

- The back-end relay protects its entries in `IDistributedCache` with ASP.NET Core data protection. Version 1.1.0 stored the
  user information of a relayed sign-in unprotected until the code was redeemed, and trusted whatever the cache returned, so
  the cache had to be as trusted as the application. While servers of both versions share a cache, a code issued by one version
  is not redeemed by the other.

## [1.1.0] - 2026-09-19

### Added

- `AppOAuth2Client` signs in from .NET MAUI applications on Android, iOS and Mac Catalyst: it opens the sign-in through a
  delegate such as `WebAuthenticator`, accepts custom-scheme and `https` redirect URIs, and always uses PKCE, so it needs no
  client secret. See [ADR-006](docs/adr/adr-006-app-sign-in.md).
- The back-end relay in Polhem.OAuth2.AspNetCore: `AddOAuth2AppRelay`, `OAuth2AppRelayOptions`, and
  `OAuth2Manager.RedirectToAppAuthorization`, `RedirectToAppAsync` and `RedeemAppCodeAsync` let an application sign in
  through its own back end, which keeps the provider's tokens and gives the application a single-use code.
- The net10.0 build of Polhem.OAuth2 is marked AOT compatible, so the trim and AOT analyzers check it.
- The OAuthMaui sample, and relay endpoints in the OAuthAspNetCore sample.

### Changed

- When an `AppOAuth2Client` signs in to Facebook with an `fb<app id>` redirect URI, the token request adds the trailing
  slash that Facebook requires.
- The documentation of `LoopbackOAuth2Client.SignInAsync` names `PlatformNotSupportedException`, which the default browser
  throws on iOS.

### Fixed

- The Microsoft Entra ID provider fills `UserName` for a personal Microsoft account, which sends no `name` claim: it joins the
  given and family names instead ([#2](https://github.com/polhem-dev/polhem-oauth2/issues/2)).

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

[Unreleased]: https://github.com/polhem-dev/polhem-oauth2/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
