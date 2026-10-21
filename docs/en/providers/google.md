# Google

**English** | [繁體中文](../../zh-TW/providers/google.md)

`GoogleOAuth2Options` signs in with a Google account through OpenID Connect. The provider name of its results is `Google`.
Google registers desktop, web, iOS and Android clients separately, each as an OAuth client of its own type in the Google
Cloud console.

## Options

- `ClientId` and `RedirectUri` are required, and `ClientSecret` is set for desktop and web clients. The options shared by every
  provider are described in [Options](../guides/options.md).
- The library checks the ID token of each sign-in: it must be issued to `ClientId` by `https://accounts.google.com` or
  `accounts.google.com`, and name the same user as the user information ([ADR-008](../../adr/adr-008-generic-openid-connect-provider.md)).

## Desktop and console applications

| Application type | Redirect URI | Port |
|------------------|--------------|------|
| Desktop app | `http://127.0.0.1:0/callback` | Any port was accepted |

- This registration was tested on 2026-09-14 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- The tested client had its redirect URIs registered. Google's documentation differs on whether a desktop app needs
  them, and lists the client secret as optional for installed applications.
- `LoopbackOAuth2Client` sends the client secret to Google, because Google's token endpoint refused the code of a Desktop app
  client without it in the test of 2026-09-26. For the same reason a desktop application uses `GoogleOAuth2Options`, not
  [`OidcOAuth2Options`](openid-connect.md), which sends no secret from a desktop application.

## .NET MAUI applications

- iOS and Mac Catalyst sign in directly with an iOS client that names the bundle ID. The redirect URI is
  `com.googleusercontent.apps.<client id>:/oauthredirect`, or a custom scheme equal to the bundle ID, and no client secret is
  needed: Google decides by the client type.
- Android signs in through the back end. An Android client refuses a custom scheme redirect ("Custom URI scheme is not enabled
  for your Android client").

See [.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register an OAuth client of the Web application type with the callback URL as its redirect URI, and follow the guide for
[ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md). A web application can also
sign in to Google with [`OidcOAuth2Options`](openid-connect.md) read from `https://accounts.google.com`.

## Tokens and users

- Google issues a refresh token to a desktop or mobile application, and to a web application only when `AuthorizationEndpoint`
  carries `?access_type=offline&prompt=consent`.
- `UserId` is the `sub` claim, a number written as a string.

## Apps with a separate back end

The front end sends its ID token, which the back end validates. `GetUserInfoAsync` also accepts a Google access token, and
`AcceptedClientIds` can list the iOS and Android clients of the same Google Cloud project. See
[Apps with a separate back end](../guides/separate-back-end.md).
