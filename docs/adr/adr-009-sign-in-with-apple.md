# ADR-009: Sign in with Apple through the web flow, with a client secret the library signs

**English** | [繁體中文](adr-009-sign-in-with-apple.zh-TW.md)

## Status

Accepted (2026-10-20). Implemented on 2026-10-20 in pull requests [#27](https://github.com/polhem-dev/polhem-oauth2/pull/27)
and [#28](https://github.com/polhem-dev/polhem-oauth2/pull/28), and tested with a real Apple Account on 2026-10-20, as "Test
results" records.

## Context

The library signs in .NET MAUI applications on iOS and Mac Catalyst (ADR-006). Guideline 4.8 of the App Store Review Guidelines
asks an application that offers a third-party sign-in to offer an equivalent option that meets its privacy requirements as
well, and in practice that is Sign in with Apple.

Apple differs from the providers before it exactly where the library made assumptions:

- The client secret is not a fixed string but a JSON Web Token signed with ES256 by the private key of a Sign in with Apple
  key, the `.p8` file that Apple issues once. Apple refuses one that expires more than 15777000 seconds, about six months,
  after it is created.
- Apple has no user information endpoint. The user ID and the email address are only in the ID token, and the name only in the
  `user` parameter that the browser posts with the first authorization.
- When the `name` or `email` scope is requested, Apple returns to the redirect URI with a form POST from its own site
  (`response_mode=form_post`), not a GET redirect.
- The return URL must be https on a domain name; localhost and IP addresses are refused (see "Test results"). A custom scheme
  cannot be registered for the web flow.

## Decision

### Options and client secret

- `AppleOAuth2Options` has the Apple endpoints of `https://appleid.apple.com/.well-known/openid-configuration` and the default
  scopes `name email`. `ClientId` is the Services ID.
- `TeamId`, `KeyId` and `PrivateKey`, the text of the `.p8` file, let the library sign the client secret. A client secret signed
  in advance is accepted in `ClientSecret` too, and the caller then replaces it before it expires. `PrivateKey` takes
  precedence when both are set.
- The library signs a secret with `iss` the Team ID, `sub` the client ID, `aud` `https://appleid.apple.com` and the Key ID as
  `kid`, valid for one hour, and replaces it ten minutes before it expires. A short lifetime keeps the damage small if a secret
  ends up in a log, and signing costs far less than the request it is sent with. The signature is the IEEE P1363 form that
  RFC 7518 requires.
- The key file is read by the library on every target, because netstandard2.0 has no `ECDsa.ImportFromPem`. The options are
  refused when the client is created if the key is not a PKCS#8 P-256 key with its public key, if the private key does not
  match that public key, if the Key ID or Team ID is missing or has white space around it, if `UserInfoEndpoint` is set, or
  if `ClientAuthentication` is not `ClientSecretPost`, the only method in Apple's discovery document. No message includes the
  key.
- A failure of the platform's cryptography while a secret is signed later becomes an `OAuth2Exception`, so a sign-in fails
  as ADR-003 expects instead of throwing `CryptographicException`.

### User information

- The user is the payload of the ID token from the token endpoint, read with the checks of ADR-008: the audience must be the
  client ID and the issuer `https://appleid.apple.com`, and the token needs a `sub`. `UserInfo.RawJson` holds that payload.
- The name is joined from the strings `name.firstName` and `name.lastName` of the `user` parameter. The browser posts the
  parameter, so its email address is ignored in favor of the ID token's, a value that is not such a JSON object or is longer
  than 4096 characters is ignored, and only strings are used. `AuthorizationCallback` gains a `User` property and a
  constructor that takes it.
- Apple sends the name only the first time a user authorizes the application. `UserName` is null afterwards, and the
  application keeps the name from the first sign-in itself.
- `OAuth2Client.GetUserInfoAsync(accessToken)` throws `NotSupportedException`, as for the other OpenID Connect providers: a front
  end proves who signed in with the ID token (ADR-007).

### Callback and cookie

- `response_mode=form_post` is sent when the `name` or `email` scope is requested, and not otherwise; Apple accepts a query
  redirect for the `openid` scope alone or for none. `OAuth2Client.UsesFormPostResponse` reports it.
- Both web managers read a POST callback from its form and keep the pending sign-in of such a client in a `SameSite=None`
  cookie. ADR-005 records the decision, why the state still binds the callback to the browser, and where the cookie is not sent.
- PKCE stays on. Apple does not list it in its discovery document, but it accepts the parameters and refuses a code
  exchange with the wrong verifier.

### Public clients

- `LoopbackOAuth2Client` and `AppOAuth2Client` refuse the options with `ArgumentException` when they are created, through a
  capability of the provider rather than a type check. Apple accepts neither a loopback nor a custom scheme redirect URI for
  the web flow, and requires the client secret for every token request, which an application cannot keep.
- A mobile application signs in through the back-end relay of ADR-006, which needs no change: the relay runs the web flow,
  form POST included.

## Out of scope

- The native Sign in with Apple of iOS (`ASAuthorizationController`), whose authorization code a back end would exchange with
  the bundle ID as the client ID, and the token revocation of `/auth/revoke`, which guideline 5.1.1(v) asks for when an
  application deletes an account. Both need a design of their own.
- A desktop sign-in with Apple, which needs a back end of the application's own.
- Validating an ID token that a front end sends, as in ADR-008.

## Consequences

- A web application signs in with Apple with `PrivateKey`, `KeyId`, `TeamId`, `ClientId` and an https `RedirectUri`, and maps its
  callback for GET and POST without antiforgery validation.
- An application that needs the name stores it at the first sign-in. A user who stops using Sign in with Apple for the
  application and signs in again sends it once more.
- `UserInfo.Email` can be an address of Apple's private email relay, `@privaterelay.appleid.com`, when the user hides their
  address. Apple documents the `is_private_email` claim of the ID token, in `RawJson`, for that case; it was not tested.
- Development against Apple needs a public https host for the return URL, such as a tunnel, because Apple refuses localhost.
- `tests/Polhem.OAuth2.UnitTests/AppleOAuth2ProviderTests.cs` covers the options, the signed secret and its renewal, the key
  checks, the `user` parameter and the ID token; `AspNetCoreOAuth2ManagerTests.cs` and `AspNetOAuth2ManagerTests.cs` cover the
  form POST callback. The fake provider answers like Apple under `/apple`, and the device tests sign in to it through the relay.

## Test results

On 2026-10-20 the Services ID page of the Apple Developer site refused `localhost` as a domain and as a return URL. It accepted
an IP address as a return URL in the form, and refused it when the configuration was saved.

The authorization endpoint, with a registered Services ID:

| Request | Result |
|---------|--------|
| `name email` with `response_mode=query` | `invalid_request`: "response_mode must be form_post when name or email scope is requested." |
| `name email` with `form_post` and PKCE | Sign-in page |
| `openid` alone, or no scope, with `query` | Sign-in page |
| `profile` | `invalid_request`: "Invalid client scope" |

The token endpoint answered `invalid_grant` to an authorization code that did not exist, whether the client secret was valid,
expired or signed by another key: it checks the code first, so the client secret was verified by real sign-ins.

The `samples/OAuthAspNetCore` web application signed in with a real Apple Account through a public tunnel, with the client secret
signed by the library:

- The token endpoint accepted the client secret, and returned an access token valid for one hour, a refresh token and an ID
  token.
- The first sign-in posted the `user` parameter with `name.firstName`, `name.lastName` and `email`, and the name was set. Later
  sign-ins had no `user` parameter and no name.
- The ID token had `iss`, `aud`, `exp`, `iat`, `sub`, `at_hash`, `email`, `email_verified` as a boolean, `auth_time` and
  `nonce_supported`. `sub` has the form `<six digits>.<32 hexadecimal digits>.<four digits>`.
- A refresh succeeded and returned an access token and an ID token, without a new refresh token.
- A code exchange with a code verifier that did not match the challenge was refused with `invalid_grant`.
- With the `openid` scope alone and with no scope, Apple returned with a GET redirect, the sign-in succeeded, and the ID token
  still had the email address of an account that had shared it before.
- The user shared their address, so the private relay address was not tested.

`samples/OAuthMaui` signed in through the back-end relay of the same web application, on the iPhone 17 Pro simulator, on Mac
Catalyst and on an Android emulator. Each returned the user ID and the email address of the ID token, the same on every
platform, and no name, because the account had authorized the application before.

The device tests of [run 39521534642](https://github.com/polhem-dev/polhem-oauth2/actions/runs/39521534642) ran the unit tests,
the signing of the client secret among them, on iOS, Mac Catalyst, Android and Windows, and signed in to the Sign in with Apple
mode of the fake provider through the relay on iOS, Mac Catalyst and Android, with the form POST callback.
