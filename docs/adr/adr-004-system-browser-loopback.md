# ADR-004: Sign in from desktop applications through the system browser and a loopback redirect

**English** | [繁體中文](adr-004-system-browser-loopback.zh-TW.md)

## Status

Accepted (2026-09-14)

## Context

Bee.OAuth2 had two desktop packages: `Bee.OAuth2.WinForms` for .NET Framework 4.8 and `Bee.OAuth2.Desktop` for .NET on
Windows. Both showed the provider's sign-in page in an embedded WebView2 control and read the authorization code from the
navigation to the redirect URI, so nothing had to listen on that URI.

That approach has several problems:

- RFC 8252 (OAuth 2.0 for Native Apps) asks native applications not to use embedded user agents. The application can
  observe what the user types on the provider's page, the user cannot check the address bar, and the sign-in session and
  password manager of the user's own browser are not available. Providers are free to refuse embedded browsers; Google's
  policy does.
- WebView2 exists only on Windows, which tied the flow to Windows Forms and required one package for each .NET flavor.
- Some redirect URIs used by the samples, such as `https://login.microsoftonline.com/common/oauth2/nativeclient`, only work
  when an embedded browser intercepts the navigation.

## Decision

- The core package provides `LoopbackOAuth2Client`. Its `SignInAsync` method listens on the loopback address of the
  redirect URI, opens the authorization URL in the default browser, waits for the redirect, and exchanges the code. This is
  the loopback interface redirection described in RFC 8252, section 7.3.
- `Polhem.OAuth2.WinForms` and `Polhem.OAuth2.Desktop` are removed. The flow needs only base class library types, so it
  lives in the netstandard2.0 core and runs on Windows, macOS and Linux, including console and Avalonia applications. The
  number of packages goes from five to three.
- The listener accepts TCP connections itself instead of using `HttpListener`. On Windows `HttpListener` is built on
  http.sys, which needs a URL reservation for prefixes such as `http://127.0.0.1:53682/`, and it cannot pick a free port.
- Listening rules:
  - Only loopback addresses are bound. The redirect URI must use `http` with `localhost` or a loopback address; any other
    URI is rejected when the client is created.
  - For `localhost`, both the IPv4 and the IPv6 loopback addresses are bound, because the browser may resolve the name to
    either. The IPv6 address is skipped only when the machine does not support it. If another program already uses the
    port there, starting fails, because that program could otherwise receive the authorization code.
  - Port 0 picks a free port for each sign-in, for providers that accept any loopback port.
- Only a request that carries the state of the current sign-in ends the wait. Any web page open in the browser can send
  requests to a loopback address, so other requests are answered and ignored; they can neither end the sign-in nor supply a code.
- The page the browser shows after the redirect only says that the response was received, because the code is exchanged afterwards.
- Failures, in addition to ADR-003: no redirect within `Timeout` becomes a failed result with `TimeoutException`,
  cancellation one with `OperationCanceledException`, and a redirect that carries an error one with `OAuth2Exception`.
  A port that cannot be listened on (`SocketException`), a second sign-in on the same client, and an authorization URL
  that is not an http or https URL (`InvalidOperationException`) propagate.
- The loopback client always uses PKCE, whatever `OAuth2Options.UsePkce` says, as RFC 8252 requires of native applications.
  With PKCE the client secret is not sent, except to Google, which requires it. Every provider tested below exchanged the
  code this way.

## Provider test results

Each provider is tested with `tools/LoopbackRedirectProbe`, which signs in through `LoopbackOAuth2Client`.

| Provider | Application type | Redirect URIs tested | Result |
|----------|------------------|----------------------|--------|
| Google | Desktop app | `http://127.0.0.1:<free port>/callback`, `http://localhost:53682/callback` | Accepted with PKCE, and the code exchange succeeded (2026-09-14). The redirect with a free port was accepted although the registered URI names port 0. |
| Microsoft Entra ID | Mobile and desktop applications | `http://localhost:<free port>/`, registered as `http://localhost` | Accepted with PKCE, and the code exchange succeeded without the client secret (2026-09-14). The port was ignored, and the trailing slash did not affect the match. Whether a path such as `/callback` must match, and `127.0.0.1`, have not been tested. |
| Auth0 | Native | `http://127.0.0.1:53682/callback`, `http://localhost:53682/callback` | Both accepted with PKCE, and the code exchange succeeded without the client secret (2026-09-14). The port must match: a redirect to `127.0.0.1` with a free port was refused with an error page. |
| Okta | Native (client authentication None, PKCE required) | `http://localhost:53682/callback`, `http://127.0.0.1:53682/callback` | Both accepted, and the code exchange succeeded without the client secret (2026-09-14). The default authorization server first refused the request because it had no access policy; a policy for the app with a rule that allows the authorization code grant fixed that. A redirect to `localhost` with a free port did not come back, so the port must match. |
| LINE | — | `http://localhost:53682/callback` | Accepted with PKCE, and the code exchange succeeded without the client secret (2026-09-14). The port must match: while only `http://localhost/callback` was registered, a redirect to port 53682 was refused as an invalid `redirect_uri`. The user information did not include an email address. `127.0.0.1` has not been tested. |
| Facebook | — | `http://localhost:53682/callback`, `http://127.0.0.1:53682/callback` | `localhost:53682` was accepted with PKCE, and the code exchange succeeded without the client secret (2026-09-14). `127.0.0.1:53682` was refused: the sign-in page reported that the application's connection is not secure. `localhost` with a free port was also accepted, although only port 53682 was registered. The app's mode (development or live) was not recorded, and a live app has not been tested. |

## Consequences

- Applications that used the desktop packages move to `LoopbackOAuth2Client` and register a loopback redirect URI with each
  provider. `Caption`, `Width`, `Height`, `AuthorizationForm` and the desktop `OAuth2Manager` have no replacement, because
  the provider's page opens in the user's browser.
- A desktop application cannot keep a client secret confidential: anything distributed with it can be extracted.
- A redirect URI without a port means port 80. On macOS the probe could not listen on that port without administrator
  rights, so a redirect URI should name its port.
- The browser takes the focus during sign-in. The Windows Forms samples bring their window back to the front when the sign-in ends.
- `tests/Polhem.OAuth2.UnitTests/LoopbackListenerTests.cs` and `LoopbackOAuth2ClientTests.cs` cover port selection, the
  IPv6 port conflict, ignored requests without the state, the timeout, cancellation, and the redirect URI sent with the
  token request. The tests target net10.0, so the netstandard2.0 build of the listener is not run by them.
