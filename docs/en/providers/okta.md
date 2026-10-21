# Okta

**English** | [繁體中文](../../zh-TW/providers/okta.md)

`OktaOAuth2Options` signs in with an Okta org through OpenID Connect. The provider name of its results is `Okta`.

## Options

- `Domain`, `ClientId` and `RedirectUri` are required. `Domain` is the host name of the org, with or without `https://`.
  The options shared by every provider are described in [Options](../guides/options.md).
- Okta uses the `default` authorization server unless `AuthorizationServerId` names another one; an empty value selects the
  org authorization server. Okta creates the `default` server only in an org of the Integrator Free Plan or with API Access
  Management, so another org sets the empty value.
- The authorization server needs an access policy with a rule that allows the authorization code grant. Without one, the
  sign-in fails with a policy evaluation error.
- Okta registers a web application for `client_secret_basic` unless told otherwise. Set `ClientAuthentication` to
  `ClientSecretBasic` for an application that refuses the secret in the request body; the web application of an Okta org
  tested on 2026-09-26 accepted both methods.

## Desktop and console applications

| Application type | Redirect URI | Port |
|------------------|--------------|------|
| Native, with client authentication None and PKCE required | `http://localhost:53682/callback` | Must match |

This registration was tested on 2026-09-14, and the code was exchanged without the client secret
([ADR-004](../../adr/adr-004-system-browser-loopback.md)).

## .NET MAUI applications

Register a custom scheme as a sign-in redirect URI of a Native application; it signed in on iOS, Mac Catalyst and Android.
See [.NET MAUI applications](../guides/maui.md) and the tests in [ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL as a sign-in redirect URI of a Web application, and follow the guide for
[ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md).

## Tokens and users

Okta issues a refresh token when `Scopes` includes `offline_access` and the application is allowed the Refresh Token grant.

## Apps with a separate back end

`GetUserInfoAsync` throws `NotSupportedException`: the front end sends its ID token, which the back end validates. See
[Apps with a separate back end](../guides/separate-back-end.md).
