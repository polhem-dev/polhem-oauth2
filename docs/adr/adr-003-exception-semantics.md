# ADR-003: Only expected OAuth2 failures become failed results

**English** | [繁體中文](adr-003-exception-semantics.zh-TW.md)

## Status

Accepted (2026-09-13). Revised on 2026-09-14, before the first release, for `OAuth2Client` and the web sign-in cookie, and on
2026-09-21 for the error object of Facebook and for `AppOAuth2Client`, which ADR-006 added, and on 2026-10-15 for token
errors that GitHub reports with status 200. Revised again on 2026-10-15 for `OAuth2Client.GetUserInfoAsync`, which
ADR-007 added, and on 2026-10-20 for the ID token checks and the discovery of ADR-008, and for Sign in with Apple (ADR-009). Revised on 2026-10-21: the ID token failures apply to every OpenID Connect
provider, as they have since the dedicated providers began to check the ID token. Revised the same day for a POST
callback whose body cannot be read in ASP.NET Core.

## Context

Completing a sign-in returns an `AuthorizationResult`. Bee.OAuth2 caught every exception and returned it inside a failed
result. Programming and configuration errors, such as an unregistered client or a call outside an HTTP request, therefore
looked the same as a routine sign-in failure, and problems that needed a fix were easy to overlook.

## Decision

- Protocol failures throw `OAuth2Exception`:
  - an error returned by the provider, in the redirect or by the token endpoint, with its code in `Error` and its text in
    `ErrorDescription`. The token endpoint of Facebook reports a Graph API error object instead of the strings of RFC 6749,
    section 5.2: its numeric `code` becomes `Error`, or its `type` when it has no code, and its `message` becomes
    `ErrorDescription`. A token response with status 200 that names an error is read the same way, because GitHub reports
    failures of its token endpoint like that, and RFC 6749 gives a successful response no error field;
  - a state that is missing or does not match, and a missing authorization code or PKCE code verifier;
  - a token response without an access token, and an empty user information response;
  - an ID token of the sign-in that an OpenID Connect provider (Google, Auth0, Okta, Microsoft Entra ID, Sign in with Apple
    or `OidcOAuth2Options`) cannot read, that was issued to another client or by another issuer, or whose subject differs from
    the user information, and a discovery document that does not describe a usable provider (ADR-008);
  - for `AppleOAuth2Options`, a token response without an ID token, and a client secret that the platform cannot sign
    with a private key that was accepted when the client was created (ADR-009).
- Completing a sign-in turns only the failures a sign-in is expected to produce into a failed result:
  - `OAuth2Client.CompleteAuthorizationAsync`: `OAuth2Exception`, `HttpRequestException`, `TaskCanceledException` when a
    request times out, and `JsonException`.
  - The ASP.NET and ASP.NET Core managers also: `OAuth2Exception` when no sign-in cookie matches the state or the sign-in
    is too old, or, in ASP.NET Core, when the form of a POST callback exceeds the form limits or its body cannot be read,
    such as one over the request size limit or cut short, and `CryptographicException` when the cookie cannot be decrypted.
    A request aborted while its body is read throws `OperationCanceledException`, as an aborted request does elsewhere.
  - `LoopbackOAuth2Client.SignInAsync` also: the failures described in ADR-004.
  - `AppOAuth2Client.SignInAsync` also: the failures described in ADR-006.
  - `OAuth2Client.GetUserInfoAsync`, which returns the user of an access token from a front end: the same exceptions as
    `CompleteAuthorizationAsync`, among them an `OAuth2Exception` when the provider refuses the token or names a client that is
    not accepted. The checks that throw instead are described in ADR-007.
- Cancellation by the caller propagates from `OAuth2Client` and the web managers as `OperationCanceledException`, and in
  ASP.NET Core also when the request is aborted. Cancellation means that nobody waits for the result, not that the sign-in
  failed. The loopback client and the application client turn it into a failed result instead, as ADR-004 and ADR-006 describe.
- Every other exception propagates. Configuration and programming errors throw `InvalidOperationException`: an
  unregistered client name, a sign-in cookie that names a client that is no longer registered, and a missing current HTTP
  context. Invalid options throw `ArgumentException` when the client is created.
- `RefreshTokenAsync` has no result type: it returns the new tokens or throws.
- Exception messages include the HTTP status code and the provider's error code, but not the response body or the error
  description, which can contain details an application should not show to its users.

## Consequences

- `AuthorizationResult.Exception` only holds expected failures, so an application can report it to the user or log it as a
  routine event.
- Callers must be ready for other exceptions, as with any library call.
- The values of an error in a redirect come from the query string, which anyone who sends the user a link can set, so an
  application encodes them before showing them.
- `tests/Polhem.OAuth2.UnitTests/OAuth2ClientTests.cs` covers the failed results for a state that does not match, an error
  redirect, a missing code or code verifier, a network failure, an error from the token endpoint and a timeout, and the
  propagation of cancellation. `OAuth2ProviderTests.cs` covers how token and error responses are read, and
  `AspNetCoreOAuth2ManagerTests.cs` and `AspNetOAuth2ManagerTests.cs` cover the cookie failures and configuration errors.
