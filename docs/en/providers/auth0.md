# Auth0

**English** | [繁體中文](../../zh-TW/providers/auth0.md)

`Auth0OAuth2Options` signs in with an Auth0 tenant through OpenID Connect. The provider name of its results is `Auth0`.

## Options

- `Domain`, `ClientId` and `RedirectUri` are required. `Domain` is the host name of the tenant, such as
  `your-tenant.auth0.com`, with or without `https://`. The options shared by every provider are described in
  [Options](../guides/options.md).
- An endpoint can carry a parameter of Auth0 in its query, such as `https://tenant.auth0.com/authorize?audience=...`.

## Desktop and console applications

| Application type | Redirect URI | Port |
|------------------|--------------|------|
| Native | `http://127.0.0.1:53682/callback` | Must match |

This registration was tested on 2026-09-14, and the code was exchanged without the client secret
([ADR-004](../../adr/adr-004-system-browser-loopback.md)).

## .NET MAUI applications

Register a custom scheme as an allowed callback URL of a Native application; it signed in on iOS, Mac Catalyst and Android.
Auth0 marks a custom scheme as a non-verifiable callback URI and asks the user to confirm every sign-in. See
[.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL as an allowed callback URL of a Regular Web Application, and follow the guide for
[ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md).

## Tokens and users

- Auth0 issues a refresh token when `Scopes` includes `offline_access` and the application is allowed it.
- When Auth0 returns `name` as JSON null, the user name falls back to `nickname`.

## Apps with a separate back end

`GetUserInfoAsync` throws `NotSupportedException`: the front end sends its ID token, which the back end validates. See
[Apps with a separate back end](../guides/separate-back-end.md).
