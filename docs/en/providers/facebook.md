# Facebook

**English** | [繁體中文](../../zh-TW/providers/facebook.md)

`FacebookOAuth2Options` signs in with a Facebook account through OAuth2 and Facebook Login. The provider name of its results
is `Facebook`.

## Options

`ClientId` and `RedirectUri` are required, and a web application sets `ClientSecret`. The options shared by every provider
are described in [Options](../guides/options.md).

## Desktop and console applications

| Registration | Redirect URI | Port |
|--------------|--------------|------|
| Facebook Login, Valid OAuth Redirect URIs | `http://localhost:53682/callback` | Register the port you use |

- This registration was tested on 2026-09-14 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- Facebook refused `127.0.0.1`; use `localhost`. Whether the tested app was in development or live mode was not recorded.
- Facebook accepted PKCE without the client secret in the tests of ADR-004 and ADR-006, but does not document it.

## .NET MAUI applications

- Applications sign in directly on iOS, Mac Catalyst and Android with the redirect URI `fb<app id>://authorize`, registered
  as an iOS platform with the bundle ID; no Android platform was needed in the tests.
- Facebook redirects to that URI with a trailing slash, and the library adds the slash to the token request.
- Facebook refused a plain custom scheme on every platform tested.

See [.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL under Valid OAuth Redirect URIs, and follow the guide for [ASP.NET Core](../guides/aspnetcore.md)
or [ASP.NET (System.Web)](../guides/aspnet-system-web.md).

## Tokens and users

- Facebook issued no refresh token in the test of 2026-09-26 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- `UserId` is different for each app the user signs in to.

## Errors

The token endpoint of Facebook reports a Graph API error instead of an OAuth2 error: `OAuth2Exception.Error` holds its
numeric code, such as `100`, and `ErrorDescription` its message. See [Results, tokens and errors](../guides/results-and-errors.md).

## Apps with a separate back end

Facebook issues no standard ID token, so the front end sends its access token and the back end calls `GetUserInfoAsync`.
The options need `ClientSecret`, and the front end uses the same Facebook app as the back end. The check sends the token in
the query of the request. See [Apps with a separate back end](../guides/separate-back-end.md).
