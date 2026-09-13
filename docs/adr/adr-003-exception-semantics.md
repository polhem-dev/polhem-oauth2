# ADR-003: Only expected OAuth2 failures become failed results

**English** | [繁體中文](adr-003-exception-semantics.zh-TW.md)

## Status

Accepted (2026-09-13)

## Context

`ValidateAuthorization` returns an `AuthorizationResult`. Bee.OAuth2 caught every exception and returned it inside a
failed result. Programming and configuration errors, such as an unregistered client or a call outside an HTTP request,
therefore looked the same as a routine sign-in failure, and problems that needed a fix were easy to overlook.

## Decision

- Protocol failures throw `OAuth2Exception`: an empty authorization code, a token response without an access token,
  an empty user information response, and a state that is missing, names no registered client or does not match.
- `ValidateAuthorization` turns only the failures an OAuth2 exchange is expected to produce into a failed result:
  - In `BaseOAuth2Client`: `OAuth2Exception`, `HttpRequestException`, `TaskCanceledException` (a request timeout),
    and JSON parse errors.
  - The ASP.NET and ASP.NET Core managers additionally turn `CryptographicException` into a failed result. It is raised
    when the state is not valid base64, is malformed, or fails authentication.
- Every other exception propagates. Configuration and programming errors, such as an unregistered client name or a
  missing HTTP context, throw `InvalidOperationException`.
- Exception messages include the HTTP status code but not the response body, which can contain details an application
  should not show to its users.

## Consequences

- `AuthorizationResult.Exception` only holds expected failures, so an application can report it to the user or log it as a
  routine event.
- Callers must be ready for other exceptions, as with any library call.
- `tests/Polhem.OAuth2.UnitTests/BaseOAuth2ClientTests.cs` covers both paths in `BaseOAuth2Client`: failed results for an
  empty authorization code and an unreachable token endpoint, and propagation of an unexpected storage failure.
- `tests/Polhem.OAuth2.UnitTests/AesCbcHmacCryptorTests.cs` and `OAuth2StateCryptorTests.cs` cover the ways a state
  value can be invalid: truncated data, altered length fields, altered content, and text that is not base64. Each one
  is reported as `CryptographicException`.
