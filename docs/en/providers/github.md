# GitHub

**English** | [繁體中文](../../zh-TW/providers/github.md)

`GitHubOAuth2Options` signs in with a GitHub account through a GitHub App or an OAuth App. The provider name of its results is
`GitHub`.

## Options

- `ClientId` and `RedirectUri` are required. Set `ClientSecret` as well: GitHub requires it for every code exchange. The
  options shared by every provider are described in [Options](../guides/options.md).
- The options ask for the scopes `read:user user:email`. A GitHub App ignores scopes and takes its permissions from its
  settings: give it the read-only Email addresses permission, or the user information has no email address when the user
  keeps it private.

## Desktop and console applications

| Registration | Redirect URI | Port |
|--------------|--------------|------|
| GitHub App or OAuth App, callback URL, registered as `http://127.0.0.1/callback` | `http://127.0.0.1:0/callback` | Any port was accepted. The host must match: `localhost` did not come back |

- This registration was tested on 2026-10-15 ([ADR-004](../../adr/adr-004-system-browser-loopback.md)).
- `LoopbackOAuth2Client` sends the client secret to GitHub; without it the token endpoint answered
  `incorrect_client_credentials`.

## .NET MAUI applications

Applications sign in to GitHub through the back end only, and `AppOAuth2Client` refuses `GitHubOAuth2Options` with an
`ArgumentException`. GitHub redirected to a custom scheme of the application, but refused
the code exchange without the client secret, which an application cannot keep. See
[.NET MAUI applications](../guides/maui.md#signing-in-through-the-back-end) and the tests in
[ADR-006](../../adr/adr-006-app-sign-in.md).

## Web applications

Register the callback URL in the settings of the GitHub App or OAuth App, and follow the guide for
[ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md).

## Tokens and users

- A GitHub App issues a refresh token when it expires user tokens, which a new GitHub App does unless that is turned off.
  An OAuth App issues no refresh token.
- `UserId` is the numeric account ID, which stays when the user renames the account; the login does not, and another account
  can take it.
- When the user keeps the email address private, the library reads the primary verified address from `/user/emails`; if that
  request fails, `Email` is null and the sign-in still succeeds.

## Errors

GitHub reports some errors of its token endpoint, such as `bad_verification_code`, with status 200; they become an
`OAuth2Exception` all the same. See [Results, tokens and errors](../guides/results-and-errors.md).

## Apps with a separate back end

GitHub issues no standard ID token, so the front end sends its access token and the back end calls `GetUserInfoAsync`. The
options need `ClientSecret`, and the front end uses the same GitHub application as the back end. Client credentials that
GitHub refuses are thrown. See [Apps with a separate back end](../guides/separate-back-end.md).
