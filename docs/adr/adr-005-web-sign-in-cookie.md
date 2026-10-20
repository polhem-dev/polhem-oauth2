# ADR-005: Keep each web sign-in in its own protected cookie

**English** | [繁體中文](adr-005-web-sign-in-cookie.zh-TW.md)

## Status

Accepted (2026-09-14). Revised on 2026-10-20 for providers that return to the callback with a form POST, which Sign in
with Apple does.

## Context

Between the redirect to the provider and the callback, a web application has to keep the state, the PKCE code verifier
and the redirect URI of the sign-in, and remember which registered client it belongs to. The state binds the callback to
the browser that started the sign-in (RFC 6749, section 10.12), and the code verifier must stay secret until the token
request (RFC 7636).

Bee.OAuth2 kept one state per browser in a cookie and the code verifier in session state, with the client name inside the
state, encrypted with a key read from the `OAUTH2_STATE_KEY` environment variable and the package's own AES-CBC-HMAC code
(ADR-001). That design:

- needs session state, and a shared session store once there is more than one server;
- needs a key that is deployed separately from the application;
- keeps one sign-in per browser, so sign-ins started in two tabs replace each other;
- makes the package responsible for encryption code of its own.

## Decision

- `OAuth2Client.CreateAuthorizationRequest` creates a new random state, and a new code verifier when PKCE is used, for
  every sign-in.
- The web managers keep the client name, the state, the code verifier, the redirect URI and the time the sign-in started in
  a cookie of its own, whose name ends with the state.
- The cookie value is protected with the data protection of the platform: ASP.NET Core data protection, or
  `MachineKey.Protect` on System.Web. Both encrypt and authenticate the value.
- Cookie attributes:
  - The name starts with `__Host-`, so browsers accept the cookie only when it is Secure, has the path `/` and names no domain.
  - HTTP-only, and `SameSite=Lax`: most providers redirect back with a top-level GET request, which carries a Lax cookie.
  - `SameSite=None` instead for a client whose provider returns with a form POST from its own site
    (`response_mode=form_post`, reported by `OAuth2Client.UsesFormPostResponse`), because a browser leaves a Lax cookie out
    of that cross-site POST. Sign in with Apple requires it whenever the `name` or `email` scope is requested. Every other
    client keeps `SameSite=Lax`, and the attribute is not configurable.
  - A lifetime of 10 minutes. ASP.NET Core also marks the cookie essential, so that a cookie consent policy does not drop it.
- The callback reads the parameters from the query of a GET request and from the form of a POST request, only from that
  one place, and treats a repeated parameter as missing. ASP.NET Core reads only a UTF-8 `application/x-www-form-urlencoded`
  body. System.Web reads the form without request validation, because the `user` parameter of Apple holds JSON and a name
  that the user typed; the library only compares or parses these values.
- The callback finds the cookie by the returned state, removes it on the response, rejects a sign-in that started more than
  10 minutes ago, and passes the values to `OAuth2Client.CompleteAuthorizationAsync`, which compares the state before it
  uses any other value.
- The format of the cookie is defined once, in `src/Shared/PendingAuthorizationCookie.cs`, and both web packages compile
  that file. The web packages do not use internal members of the core package through InternalsVisibleTo, because they
  accept any later version of it.

ASP.NET Core's own OAuth handler follows the same principle: it protects the data of each sign-in with data protection and
binds it to a correlation cookie of its own.

## Consequences

- No session state and no `OAUTH2_STATE_KEY` are needed. The AES-CBC-HMAC code is removed, so the byte-compatible state
  format of ADR-001 no longer has a purpose.
- Every server that can receive a callback must share the data protection key ring in ASP.NET Core, or use the same machine
  key on System.Web.
- A sign-in must complete within 10 minutes. A sign-in started before an upgrade to this design does not complete after
  it, and the user signs in again.
- Each unfinished sign-in leaves a cookie for up to 10 minutes, so a browser that starts many sign-ins without finishing
  them sends larger requests during that time.
- The sign-in must start and end on HTTPS pages.
- A `SameSite=None` cookie is sent with a POST from any site, but it is still found only by the state of its own sign-in.
  An attacker's page cannot name the state of the victim's sign-in, which it does not know, and cannot place a cookie for its
  own sign-in in the victim's browser, because the `__Host-` prefix and the data protection keep it from setting one. The
  state therefore binds the callback to the browser as before.
- The callback of a form POST client must accept POST and be left out of antiforgery validation, such as
  `[IgnoreAntiforgeryToken]` under `AutoValidateAntiforgeryTokenAttribute`, because the provider's page sends no token.
- A form POST client fails where the browser does not send the `SameSite=None` cookie: a cookie policy whose
  `MinimumSameSitePolicy` is stricter than None; System.Web without the November 2019 update of .NET Framework 4.7.2 or later,
  or with the `aspnet:SupressSameSiteNone` setting, which leave the attribute out; and older browsers such as Safari on iOS 12,
  which treat `SameSite=None` as Strict.
- `tests/Polhem.OAuth2.UnitTests/AspNetCoreOAuth2ManagerTests.cs`, `AspNetOAuth2ManagerTests.cs` (run on .NET Framework)
  and `PendingAuthorizationCookieTests.cs` cover the cookie attributes, the `SameSite=None` cookie and the form POST callback, sign-ins in parallel, missing and altered cookies,
  expiry, and the removal of the cookie in the callback.
