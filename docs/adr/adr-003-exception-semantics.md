# ADR-003: Only expected OAuth2 failures become failed results

**English** | [繁體中文](adr-003-exception-semantics.zh-TW.md)

## Status

Accepted (2026-09-13). Revised on 2026-09-14, before the first release, for `OAuth2Client` and the web sign-in cookie, and on
2026-09-21 for the error object of Facebook.

## Context

Completing a sign-in returns an `AuthorizationResult`. Bee.OAuth2 caught every exception and returned it inside a failed
result. Programming and configuration errors, such as an unregistered client or a call outside an HTTP request, therefore
looked the same as a routine sign-in failure, and problems that needed a fix were easy to overlook.

## Decision

- Protocol failures throw `OAuth2Exception`:
  - an error returned by the provider, in the redirect or by the token endpoint, with its code in `Error` and its text in
    `ErrorDescription`. The token endpoint of Facebook reports a Graph API error object instead of the strings of RFC 6749,
    section 5.2: its numeric `code` becomes `Error`, or its `type` when it has no code, and its `message` becomes
    `ErrorDescription`;
  - a state that is missing or does not match, and a missing authorization code or PKCE code verifier;
  - a token response without an access token, and an empty user information response.
- Completing a sign-in turns only the failures a sign-in is expected to produce into a failed result:
  - `OAuth2Client.CompleteAuthorizationAsync`: `OAuth2Exception`, `HttpRequestException`, `TaskCanceledException` when a
    request times out, and `JsonException`.
  - The ASP.NET and ASP.NET Core managers also: `OAuth2Exception` when no sign-in cookie matches the state or the sign-in
    is too old, and `CryptographicException` when the cookie cannot be decrypted.
  - `LoopbackOAuth2Client.SignInAsync` also: the failures described in ADR-004.
- Cancellation by the caller propagates from `OAuth2Client` and the web managers as `OperationCanceledException`, and in
  ASP.NET Core also when the request is aborted. Cancellation means that nobody waits for the result, not that the sign-in
  failed. The loopback client turns it into a failed result instead, as ADR-004 describes.
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
