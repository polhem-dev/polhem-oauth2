# ADR-007: Return the user of an access token from a front end only after the provider names its client

**English** | [繁體中文](adr-007-access-token-from-front-end.zh-TW.md)

## Status

Accepted (2026-10-15). Implemented on 2026-10-15 in pull requests
[#9](https://github.com/polhem-dev/polhem-oauth2/pull/9), [#10](https://github.com/polhem-dev/polhem-oauth2/pull/10),
[#11](https://github.com/polhem-dev/polhem-oauth2/pull/11) and [#18](https://github.com/polhem-dev/polhem-oauth2/pull/18). The checks were tested with
tokens that are not valid on 2026-10-15, and with real accounts and the tokens of other clients on 2026-10-16, as "Test results"
records. ADR-008 added `OidcOAuth2Options` to the providers that are not supported on 2026-10-20.

## Context

A back end with its own user accounts may receive an access token from a front end, such as a .NET MAUI, desktop or
single-page application, that signed in to the provider itself. The back end needs the user behind the token, to link the
sign-in to its own account and issue its own session.

Calling the user information endpoint with the token is not enough. When a user signs in to another application of the same
provider, that application holds a valid token for the user too. It can send the token to the back end, and the user
information endpoint answers with the same user, so the application signs in to the back end as the user. This is token
substitution. Only the provider knows which client a token was issued to.

Two other ways to learn the user avoid the question:

- The back end runs the sign-in itself: ADR-005 for web applications and the back-end relay of ADR-006 for mobile
  applications. The tokens never leave the back end.
- The front end sends the ID token of an OpenID Connect sign-in. Its audience is the client ID of the front end, and the back
  end validates its signature, issuer, expiry and audience locally.

Not every provider issues an ID token. Facebook Login issues none outside the Limited Login of iOS, and GitHub has no OpenID
Connect sign-in for users.

## Decision

- The ways to learn the user, in order of preference:
  1. The back end runs the sign-in (ADR-005, ADR-006), when the back end only needs to know who signed in.
  2. The front end sends the ID token, and the back end validates it, when the front end signs in itself and the provider is
     an OpenID Connect provider.
  3. The front end sends the access token, and the back end calls `OAuth2Client.GetUserInfoAsync(accessToken)`, for a provider
     without a standard ID token.
- `GetUserInfoAsync` asks the provider which client the token was issued to before it requests the user information. Every
  client ID the provider reports must be `OAuth2Options.ClientId` or one of `OAuth2Options.AcceptedClientIds`, compared
  ordinally. An empty list accepts only `ClientId`. The check has no option to skip it, because a back end that skipped it would
  accept any application's token.
- The providers are checked as follows:

  | Provider | Check | Accepted when |
  |----------|-------|---------------|
  | Google | `POST https://oauth2.googleapis.com/tokeninfo`, token in a form body | `aud` is accepted and, when present, `azp` too. Clients of one Google Cloud project can obtain tokens for each other, so the two can differ |
  | LINE | `GET https://api.line.me/oauth2/v2.1/verify`, token in the query | `client_id` is accepted |
  | Facebook | `GET https://graph.facebook.com/<version>/debug_token`, token in the query, app access token in the `Authorization` header | `data.is_valid` is `true`, `data.type` is `USER` and `data.app_id` is accepted |
  | GitHub | `POST <API>/applications/<client ID>/token`, client ID and secret in a Basic header, token in a JSON body. `<API>` is the user information endpoint without its trailing `/user` | GitHub finds the token for `ClientId`, and the response names `ClientId` |

- Microsoft Entra ID, Auth0 and Okta are not supported. `GetUserInfoAsync` throws `NotSupportedException`, and the message
  points to the ID token, or to an access token for an API of the back end, validated as a JSON Web Token:
  - Entra ID: Microsoft asks applications to treat an access token for Microsoft Graph as opaque and offers no endpoint that
    names its client.
  - Auth0: without an audience, which `Auth0OAuth2Options` does not send, Auth0 issues an opaque token that only `/userinfo`
    accepts, and it has no introspection endpoint.
  - Okta: introspection (RFC 7662) could name the client with the back end's credentials, but whether one client may
    introspect the token of another is documented ambiguously, and the ID token answers the question without a request for
    each token.
- `OidcOAuth2Options`, which ADR-008 added for any other OpenID Connect provider, is not supported either, for the reason of
  Okta.
- Facebook and GitHub need `ClientSecret`; without one, `GetUserInfoAsync` throws `InvalidOperationException`. Both check
  only tokens issued to the application whose credentials authenticate the check, so `AcceptedClientIds` accepts nothing more
  there, and the front end and the back end use the same Facebook app or GitHub application. Facebook refuses the token of
  another app with error 100, and GitHub does not find it.
- Failures follow ADR-003. A failed result holds `OAuth2Exception` when the provider refuses the token, names no client, or names
  one that is not accepted, and when the token is longer than the library accepts; `HttpRequestException`; `TaskCanceledException`
  when a request times out; and `JsonException`. These throw: `ArgumentException` for an empty token, `NotSupportedException`
  for a provider without a check, and `InvalidOperationException` for a missing secret, refused GitHub credentials, or a GitHub
  user information endpoint whose path does not end with `/user`.
- No exception message that the library creates includes the token or the request URI, which the tests check. LINE and Facebook document only GET for their checks, so the token
  travels in the query, and the remarks of `GetUserInfoAsync` say how to keep it out of the logs of an HTTP client.
- Encrypting the calls between the front end and the back end, as the payload packages of Polhem.JsonRpc do, does not replace
  the check. Encryption proves that the caller holds the key, and a front end is a public client: its key can be taken from the
  application, or negotiated by anyone. The attacker of token substitution is exactly someone who can run a front end.

## Out of scope

- Validating ID tokens or JSON Web Token access tokens. Libraries such as JwtBearer authentication do that.
- Replay of a token within its lifetime. A back end that issues its own session after the first call can stop accepting the same
  token, and the payload packages of Polhem.JsonRpc can refuse a replayed call when a method asks for it.
- Caching the result of a check.

## Consequences

- A back end can accept an access token from Google, LINE, Facebook and GitHub without trusting the front end.
- Checking a token sends a request to the provider whether or not the token turns out to be valid, and the providers limit how
  many they answer. The remarks ask the application to limit how often its endpoint can be called.
- `tests/Polhem.OAuth2.UnitTests/OAuth2ClientAccessTokenTests.cs` covers the accepted and refused tokens of each provider, the
  failure classification, the unsupported providers, the copying and validation of `AcceptedClientIds`, and that the token
  stays out of exception messages.

## Test results

On 2026-10-15 each check was sent tokens that are not valid, with the credentials of the test applications where needed:

- Google: a POST with the token in a form body returned `invalid_token`; without the token, "Either access_token, id_token, or
  token_handle required". The endpoint reads the body.
- LINE: a GET returned `invalid_request`. A POST to the same path verifies an ID token instead and asked for `id_token`, so the
  check cannot use POST.
- Facebook: with the app access token in the header, the app's own token returned `data.app_id`, `data.type` `APP` and
  `data.is_valid` `true`; a token that is not valid returned status 200 with `data.is_valid` `false` and `data.error`; a wrong
  app access token returned 401.
- GitHub: a token that is not valid returned 404, a wrong secret 401, and a body without `access_token` 422.
- Okta, before it was decided not to support it: introspection accepted the Basic credentials of a web application and returned
  `{"active":false}` for a token that is not valid.

On 2026-10-16 a desktop front end signed in to each provider with a real account through `LoopbackOAuth2Client`, and
`GetUserInfoAsync` received its access token:

- Google: the front end used the desktop client. tokeninfo returned `aud` and `azp`, both the desktop client ID. A back end
  with that `ClientId` returned the user. A back end with the web client of the same Google Cloud project as its `ClientId`
  refused the token as issued to a client that is not accepted, and accepted it once `AcceptedClientIds` listed the desktop
  client; listing only the iOS client refused it again.
- LINE: verify returned `client_id`, the channel ID, and the back end of the same channel returned the user. Only one channel
  was available, so another client was simulated: a back end with a different `ClientId` refused the token, and accepted it
  once `AcceptedClientIds` listed the channel.
- Facebook: the back end had `ClientSecret`. debug_token returned `data.app_id`, `data.type` `USER` and `data.is_valid`
  `true`, and the back end returned the user. A user token of another app of the same developer, generated with the Graph API
  Explorer, was refused: debug_token returned status 400 with error 100, "The App_id in the input_token did not match the
  Viewing App". The back end refused it as well when `AcceptedClientIds` listed that app, which is why Facebook, like GitHub,
  accepts only tokens of `ClientId`.
- GitHub: the front end and the back end used the same GitHub App. The check returned `app.client_id`, the App's client ID,
  and the back end returned the user. A valid token of the GitHub CLI, another OAuth application, returned 404 and was
  refused, also when `AcceptedClientIds` listed the client ID of the GitHub CLI.

No message of a failed result included the token.
