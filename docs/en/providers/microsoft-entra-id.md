# Microsoft Entra ID

**English** | [繁體中文](../../zh-TW/providers/microsoft-entra-id.md)

`AzureOAuth2Options` signs in with a Microsoft work, school or personal account through Microsoft Entra ID and OpenID
Connect. The provider name of its results is `Azure`.

## Options

- `ClientId` and `RedirectUri` are required. The options shared by every provider are described in
  [Options](../guides/options.md).
- Microsoft Entra ID uses the `common` tenant. An application registered for a single tenant sets `Tenant` to the tenant
  ID or domain name.

## Desktop and console applications

| Platform | Redirect URI | Port |
|----------|--------------|------|
| Mobile and desktop applications, registered as `http://localhost` | `http://localhost:0` | Ignored |

- This registration was tested on 2026-09-14, and the code was exchanged without the client secret
  ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- URIs that only worked inside an embedded browser, such as `https://login.microsoftonline.com/common/oauth2/nativeclient`,
  do not work with the system browser.

## .NET MAUI applications

- iOS and Mac Catalyst register the iOS/macOS platform with the redirect URI `msauth.<bundle id>://auth`, or a custom URI
  under mobile and desktop applications.
- Android registers the Android platform with the package name and signature hash, and the redirect URI
  `msauth://<package>/<signature hash>`, or a custom URI.
- Microsoft Entra ID registers a custom scheme only with two slashes, as `<scheme>://<path>`.
- Microsoft asks a personal account to confirm that the application comes from a trusted source.

See [.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL as a redirect URI of the web platform, and follow the guide for
[ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md).

## Tokens and users

- Microsoft Entra ID issues a refresh token when `Scopes` includes `offline_access`.
- `UserId` is the `sub` claim, which is different for each application the user signs in to. The `oid` claim of
  `Token.IdToken` is the same in every application, for an application that needs that; validate the token first.

## Apps with a separate back end

`GetUserInfoAsync` throws `NotSupportedException`: the front end sends its ID token, which the back end validates. See
[Apps with a separate back end](../guides/separate-back-end.md).
