# LINE

**English** | [繁體中文](../../zh-TW/providers/line.md)

`LineOAuth2Options` signs in with a LINE account through a LINE Login channel. The provider name of its results is `LINE`.

## Options

`ClientId` and `RedirectUri` are required, and `ClientSecret` is set for desktop and web clients. The options shared by every
provider are described in [Options](../guides/options.md).

## Desktop and console applications

| Registration | Redirect URI | Port |
|--------------|--------------|------|
| LINE Login channel, Callback URL | `http://localhost:53682/callback` | Must match |

- This registration was tested on 2026-09-14 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- Include the Mobile app type in the channel. In the test of 2026-09-26, a channel of the Web app type alone exchanged the
  code without the client secret but refused a refresh without it, so `LoopbackOAuth2Client` sends the secret to LINE when it
  is set.

## .NET MAUI applications

- iOS and Mac Catalyst sign in directly with the redirect URI `line3rdp.<bundle id>://auth`, registered as the iOS bundle ID of
  the channel.
- Android signs in through the back end. LINE refused a redirect to an Android application ("Invalid redirect custom scheme").
- LINE refused a plain custom scheme on every platform tested.

See [.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL as a Callback URL of the channel, and follow the guide for [ASP.NET Core](../guides/aspnetcore.md)
or [ASP.NET (System.Web)](../guides/aspnet-system-web.md). One channel can serve the desktop and the web clients with two
callback URLs.

## Tokens and users

- LINE issued a refresh token in the test of 2026-09-26 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- `UserId` is the same in every channel of one provider.
- LINE returns the email address only in the ID token, and only when the channel may read it and the user agreed. The
  library reads it from the ID token that the token endpoint returned, and checks that the token was issued to the client,
  but does not check its signature.

## Apps with a separate back end

The front end sends its ID token, which the back end validates. `GetUserInfoAsync` also accepts a LINE access token; the check
sends the token in the query of the request. See [Apps with a separate back end](../guides/separate-back-end.md).
