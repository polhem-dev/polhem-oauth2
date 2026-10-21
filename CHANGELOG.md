# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to Polhem.OAuth2, Polhem.OAuth2.AspNet and Polhem.OAuth2.AspNetCore, a few lines per version. Each
version links to its release notes under [`docs/en/changelogs/`](docs/en/changelogs/), which describe every change in
full and, for each breaking change, who it affects and how to migrate. Breaking changes are marked **Breaking:** here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/). 1.2.0 and 1.3.0 were released as minor versions and contain breaking changes,
which were identified when this changelog was rewritten; from 1.4.0 on, a breaking change raises the major version.

## [Unreleased]

Details and migration steps: [docs/en/changelogs/1.4.0.md](docs/en/changelogs/1.4.0.md)

- `GitHubOAuth2Options` signs in with GitHub, through a GitHub App or an OAuth App.
- `OAuth2Client.GetUserInfoAsync(accessToken)` returns the user of an access token from a front end, after the provider confirms which client it was issued to.
- `OidcOAuth2Options` signs in with any other OpenID Connect provider, with endpoints set by hand or read from its discovery document.
- `AppleOAuth2Options` signs a web application in with Apple, and the web managers accept the form POST callback it returns with.
- Google, Auth0, Okta and Microsoft Entra ID check the ID token of a sign-in against the client and the user information.
- Requests to providers name the library in `User-Agent` and ask the token endpoint for JSON, and a token error sent with status 200 carries the provider's error code.

## [1.3.0] - 2026-09-26

Details and migration steps: [docs/en/changelogs/1.3.0.md](docs/en/changelogs/1.3.0.md)

- **Breaking:** A token response that names a token type other than `Bearer` fails the sign-in or the refresh.
- **Breaking:** The shared `HttpClient` that a client uses when none is passed in no longer follows redirects.
- Polhem.OAuth2 has a net8.0 build, whose default `HttpClient` replaces pooled connections and follows DNS changes, as on .NET 10.
- `OAuth2Options.ClientAuthentication` can send the client secret in an HTTP Basic header.
- `AddOAuth2AppRelay` reads its settings from a configuration section, and the ASP.NET Core `OAuth2Manager` can be derived from.
- Reading a response body now honors cancellation, and several sign-in failures of the web packages and the loopback client are fixed.

## [1.2.0] - 2026-09-25

Details and migration steps: [docs/en/changelogs/1.2.0.md](docs/en/changelogs/1.2.0.md)

- **Breaking:** The back-end relay protects its cache entries with data protection, so a code is not redeemed by a server of another version.
- **Breaking:** The relay refuses an application redirect URI that is no longer listed, and a `CodeLifetime` longer than 10 minutes.
- **Breaking:** A Facebook token endpoint error becomes an `OAuth2Exception` instead of an `HttpRequestException`.
- `OAuth2Client.Create(options, httpClientFactory)` and `AddOAuth2ClientWithHttpClientFactory` take each `HttpClient` from a factory, such as `IHttpClientFactory`.
- The relay can be started without exceptions and can return its URLs instead of redirecting, through `TryRedirectToAppAuthorization` and the `Create…Url` methods.
- The query of an authorization endpoint is kept, and `UserInfo.UserName` falls back to the given and family names for every OpenID Connect provider.

## [1.1.0] - 2026-09-19

Details and migration steps: [docs/en/changelogs/1.1.0.md](docs/en/changelogs/1.1.0.md)

- `AppOAuth2Client` signs in from .NET MAUI applications on Android, iOS and Mac Catalyst.
- The back-end relay of Polhem.OAuth2.AspNetCore lets an application sign in through its own back end, which keeps the provider's tokens.
- The net10.0 build of Polhem.OAuth2 is marked AOT compatible.
- The Microsoft Entra ID provider fills `UserName` for a personal Microsoft account.

## [1.0.0] - 2026-09-14

Details and migration steps: [docs/en/changelogs/1.0.0.md](docs/en/changelogs/1.0.0.md)

- **Breaking:** Bee.OAuth2 is renamed Polhem.OAuth2: package IDs, namespaces and target frameworks change, and the desktop packages are replaced by `LoopbackOAuth2Client`.
- **Breaking:** The provider classes and the state storage types are no longer public, and `AuthorizationResult` and `UserInfo` are sealed and read-only.
- **Breaking:** The web packages keep each sign-in in a protected cookie instead of session state, and their methods are renamed.
- **Breaking:** PKCE is on by default, options are checked when a client is created, endpoints must use https, and unexpected exceptions are thrown.
- **Breaking:** A JSON null field of the user information is `null`, and the Microsoft Entra ID user identifier is read from `sub`.
- `LoopbackOAuth2Client` signs in from desktop and console applications through the system browser, and every client returns its tokens and can refresh them.

[Unreleased]: https://github.com/polhem-dev/polhem-oauth2/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.3.0
[1.2.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.2.0
[1.1.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.1.0
[1.0.0]: https://github.com/polhem-dev/polhem-oauth2/releases/tag/v1.0.0
