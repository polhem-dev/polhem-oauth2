# ADR-011: When an application has a back end, the back end signs in

**English** | [繁體中文](adr-011-back-end-signs-in.zh-TW.md)

## Status

Accepted (2026-11-05). Supersedes ADR-007. Implemented on 2026-11-05 in pull requests
[#45](https://github.com/polhem-dev/polhem-oauth2/pull/45) (the desktop relay),
[#46](https://github.com/polhem-dev/polhem-oauth2/pull/46) (the removal of the access token check) and
[#47](https://github.com/polhem-dev/polhem-oauth2/pull/47) (the samples and the device tests). "Test results" records the tests.

## Context

ADR-007 gave an application with a back end three ways to tell the back end who signed in, in order of preference: the back
end runs the sign-in itself (ADR-005 for the web, ADR-006 for mobile applications), the front end sends an ID token for the
back end to validate, or the front end sends an access token, whose client the back end checks with the provider through
`OAuth2Client.GetUserInfoAsync(accessToken)`.

The last two exist because a front end may sign in on its own. A desktop application had no other choice: ADR-006 relays
only mobile applications, so a desktop application with a back end signed in with `LoopbackOAuth2Client` and then had to
hand the back end a token. With Facebook and GitHub, which issue no ID token, only the access token check was left.

The check carries costs that the other ways do not. LINE and Facebook document it only with the token in the query string,
so the token can reach HTTP client logs. Every check is a request to the provider, which limits how many it answers. Facebook
and GitHub check only tokens issued to the checking application, so the front end and the back end must share one
application. And the front end, a public client, holds the provider's tokens and, for GitHub, the client secret.

The common practice points the same way. The IETF's best current practice for browser-based applications recommends a
back end for frontend, which keeps the tokens on the server, for business applications and applications that handle personal
data. Microsoft's documentation of `WebAuthenticator` for .NET MAUI recommends a web back end as the middle layer between the
application and the provider, because an application cannot keep a client secret.

## Decision

- An application without a back end of its own signs in directly and uses the result itself: `LoopbackOAuth2Client` on the
  desktop, `AppOAuth2Client` on mobile. Nothing changes for it.
- An application with a back end has the back end sign in, with every provider, OpenID Connect providers included:
  - a web front end through the web sign-in of ADR-005;
  - a mobile application through the relay of ADR-006;
  - a desktop application, and the Windows platform of a .NET MAUI application, through the same relay, which now accepts
    loopback redirect URIs.
- The front end never hands the back end a token, or user information, as proof of who signed in. The library does not
  validate one, and the documentation no longer recommends it.
- `OAuth2Client.GetUserInfoAsync(accessToken)`, `OAuth2Options.AcceptedClientIds` and the provider token checks are removed
  in 1.5.0, without an `[Obsolete]` step: ADR-010 allows it in a 1.x minor version, they shipped in 1.4.0 on 2026-10-21, and
  no repository of the polhem-dev organization called them.

### The desktop relay

- The relay of Polhem.OAuth2.AspNetCore accepts a loopback application redirect URI: `http`, the address `127.0.0.1` or
  `[::1]`, and a path, with no query, user information or fragment. It must be written in the form that `Uri` writes, so the
  URI the relay later redirects to is exactly the one it compared. `localhost` is refused, as in ADR-004, and so is every
  other spelling of the two addresses.
- A loopback URI registered without a port matches the same address and path on any port, as RFC 8252, section 7.3,
  describes. Registered with a port, it matches that port only. `OAuth2Options.IsAppRedirectUri` is unchanged, so
  `AppOAuth2Client` still refuses http.
- A requested loopback URI may end with `?state=` and 43 to 128 base64url characters, and nothing else in its query. The
  relay appends the code after it. Any web page open in the browser can send a request to a loopback address (ADR-004), so
  without a state, a page that found the port could end the sign-in with a code of its own. That code would not redeem, since
  the verifier does not match, but the user's sign-in would be lost.
- `LoopbackRelayClient` in Polhem.OAuth2 creates the PKCE code verifier, the application redirect URI with the bound port and
  a state, asks the application for the back end's sign-in URL, opens it, waits for the redirect that carries the state, and
  returns the code with the verifier. It shares the listener of `LoopbackOAuth2Client`. Expected failures, an error redirect,
  a redirect without a code, a timeout and cancellation, are failed results (ADR-003).
- The application sends the code and the verifier to its back end itself, and the back end redeems them with
  `RedeemAppCodeAsync`. The sign-in URL and the redemption endpoint are a contract between the application and its back end,
  as for mobile applications, and the library does not define them.

## Out of scope

- A relay in Polhem.OAuth2.AspNet (System.Web). A back end on another framework can follow the same steps with
  `OAuth2Client`.
- Native sign-in libraries that hand the device a token, such as the iOS Sign in with Apple button, as ADR-009 already
  excludes.
- Returning the provider's tokens to the application. An application that must call a provider API itself signs in
  directly, or has the back end call the API.

## Consequences

- One rule covers every application with a back end, and the provider's tokens and client secret never reach the front end.
  Sign in with Apple and GitHub become available to desktop applications with a back end, without shipping a client secret.
- Any program on the user's computer can listen on a loopback port. With a registration without a port, such a program can
  start a relayed sign-in to its own port and redeem the code with its own verifier, which RFC 8252, section 8.3, accepts for
  loopback redirects. The documentation of `AppRedirectUris` says so and suggests registering a port when the application can
  always use the same one.
- A back end that called `GetUserInfoAsync(accessToken)` no longer compiles. The release notes of 1.5.0 describe the
  migration: move the sign-in to the back end.
- `tests/Polhem.OAuth2.UnitTests/AspNetCoreAppRelayLoopbackTests.cs` covers the registration and matching rules,
  `LoopbackRelayClientTests.cs` the client, and `AspNetCoreLoopbackRelayTests.cs` a sign-in from the client through a real
  relay to the redeemed user. The fake provider registers the loopback URI and a relay client for its OpenID Connect mode.

## Test results

On 2026-11-05:

- The unit tests passed on .NET 10, including a sign-in from `LoopbackRelayClient` through an `OAuth2Manager` relay.
- The fake provider was run locally, and a relayed sign-in was followed for its Auth0 and OpenID Connect modes to
  `http://127.0.0.1:53999/callback?state=...`. Each returned `?state=...&code=...`, and each code redeemed for the fake user.
  The relay had not been tested with `OidcOAuth2Options` before.
- An adversarial review of the loopback rules tried other spellings of the addresses (`127.1`, `127.0.0.2`,
  `[0:0:0:0:0:0:0:1]`, `[::ffff:127.0.0.1]`, a trailing dot), uppercase schemes, leading zeros in the port, ports 0 and
  65536, backslashes, encoded slashes, and a second `code` in the state query. The relay refused each of them.
- [Run 42029623202](https://github.com/polhem-dev/polhem-oauth2/actions/runs/42029623202) of the device tests passed on
  Windows, iOS, Mac Catalyst and Android. On Windows, `LoopbackRelayClient` signed in to the fake provider through the relay;
  on Mac Catalyst, an application signed in to the OpenID Connect mode through the relay.
- On macOS, the OAuthConsole sample with `--back-end` signed in through the OAuthAspNetCore sample with real accounts, the
  back end registering `http://127.0.0.1/callback` without a port:
  - GitHub, with the back end's web client: the redeemed user had the numeric account ID, the name and the email
    address. The client secret stayed on the back end.
  - Facebook: the redeemed user had the numeric ID, the name and the email address.

  These are the two providers without an ID token, for which a front end with a back end could only use the access token
  check that this decision removes.
