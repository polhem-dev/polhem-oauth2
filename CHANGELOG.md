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
- The Okta provider (`OktaOAuth2Options`).
- A net10.0 target for Polhem.OAuth2, which has no package dependencies.
- Nullable reference type annotations on the public API.

### Changed

- Package IDs and namespaces are renamed from `Bee.OAuth2` to `Polhem.OAuth2`.
- Polhem.OAuth2.AspNetCore targets net10.0 instead of net8.0, and references the ASP.NET Core shared framework instead
  of the `Microsoft.AspNetCore.Http.Abstractions` package.
- JSON is parsed with System.Text.Json. The packages no longer depend on `Bee.Base` or `Newtonsoft.Json`. See
  [ADR-001](docs/adr/adr-001-drop-bee-base.md).
- A user information field whose value is JSON null is now `null` instead of an empty string, so fallback fields take effect.
- `AuthorizationResult.Exception` holds only the failures an OAuth2 sign-in is expected to produce; other exceptions are
  thrown. Exception messages include the HTTP status code but no longer the response body. See
  [ADR-003](docs/adr/adr-003-exception-semantics.md).

### Removed

- `Bee.OAuth2.WinForms` and `Bee.OAuth2.Desktop`, together with the embedded WebView2 sign-in window. Use
  `LoopbackOAuth2Client` instead.

### Security

- A state value that is truncated, malformed or altered is rejected with `CryptographicException` before it is
  decrypted. The encrypted state format is unchanged, so an existing `OAUTH2_STATE_KEY` keeps working.
- `ValidateState` fails when the returned state or the stored state is empty. Before, a request without a state passed
  when no state was stored.

[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
