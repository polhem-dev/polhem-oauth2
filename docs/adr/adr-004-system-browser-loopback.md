# ADR-004: Sign in from desktop applications through the system browser and a loopback redirect

**English** | [繁體中文](adr-004-system-browser-loopback.zh-TW.md)

## Status

Accepted (2026-09-14). Revised on 2026-09-14, before the first release, for `OAuth2Client`, the client secret rule, and the
listener. Revised on 2026-09-19 to name the platforms the flow has been tested on and the exception on iOS, and on 2026-09-21 to
say where accepting `localhost` departs from RFC 8252.

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
  the loopback interface redirection described in RFC 8252, section 7.3. It builds on the same `OAuth2Client` flow as the
  web packages.
- `Polhem.OAuth2.WinForms` and `Polhem.OAuth2.Desktop` are removed. The flow needs only base class library types, so it
  lives in the netstandard2.0 core and does not depend on a UI framework. The number of packages goes from five to three.
  The provider tests below ran in a console application; the device tests of ADR-006 run the listener on Android, iOS,
  Mac Catalyst and Windows, and sign in through the default browser on Windows.
- The listener accepts TCP connections itself instead of using `HttpListener`. On Windows `HttpListener` is built on
  http.sys, which needs a URL reservation for prefixes such as `http://127.0.0.1:53682/`, and it cannot pick a free port.
- Listening rules:
  - Only loopback addresses are bound. The redirect URI must use `http` with `localhost` or a loopback address; any other
    URI is rejected when the client is created. RFC 8252, section 8.3, recommends against `localhost`, which this client
    accepts all the same: Facebook refuses `127.0.0.1`, and Microsoft Entra ID registers only `http://localhost`, as the
    provider test results below record. Comparing the `Host` header of a request answers the concern of that section.
  - For `localhost`, both the IPv4 and the IPv6 loopback addresses are bound, because the browser may resolve the name to
    either. The IPv6 address is skipped only when the machine does not support it. If another program already uses the
    port there, starting fails, because that program could otherwise receive the authorization code.
  - Port 0 picks a free port for each sign-in, for providers that accept any loopback port. Each sign-in sends the redirect
    URI with the bound port; the options are copied when the client is created and are never changed.
  - Connections are read side by side, so a connection that the browser opens in advance and leaves idle does not delay
    the redirect.
- Only a request for the redirect path whose `Host` header names the host and port of the redirect URI, and that carries
  the state of the current sign-in, ends the wait. Any web page open in the browser can send requests to a loopback
  address, and through DNS rebinding such a page can use a host name of its own, so other requests are answered and
  ignored; they can neither end the sign-in nor supply a code. The path is compared after percent-decoding.
- The page the browser shows after the redirect only says that the response was received, because the code is exchanged afterwards.
- `OpenBrowser` lets an application open the URL another way, such as through the launcher of a UI framework. It receives
  the escaped absolute URI and returns a task.
- Failures, in addition to ADR-003: no redirect within `Timeout` becomes a failed result with `TimeoutException`, and
  cancellation, while waiting or during the code exchange, one with `OperationCanceledException`. A port that cannot be
  listened on (`SocketException`), a second sign-in on the same client (`InvalidOperationException`), and a missing default
  browser when `OpenBrowser` is null (`Win32Exception`, or `PlatformNotSupportedException` on iOS, where no process can be
  started) propagate.
- The loopback client is a public client. It always uses PKCE, whatever `OAuth2Options.UsePkce` says, as RFC 8252 requires
  of native applications, and it does not send the client secret, except to Google. Google's documentation lists the client
  secret as optional for installed applications, but a test without it on 2026-09-26 failed: the token endpoint answered
  `invalid_request`, so the secret is required and the exception stays. Every other provider tested below exchanged the code
  without it. Web clients, which can keep a secret, send it whenever it is set.

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

These results were recorded before the revision of 2026-09-14. The LINE provider now reads the email address from the ID
token, so a later test can return one when the channel may read it.

### Retest of 2026-09-26

The same applications, signed in with the probe's `--refresh yes`, and with `--scopes "openid email profile offline_access"`
for Microsoft Entra ID, Auth0 and Okta. The probe reports the token type, lifetime and granted scopes as returned, and which
tokens came back.

| Provider | Result |
|----------|--------|
| Google | Without the client secret (`--secret omit`) the token endpoint refused the code with `invalid_request`. |
| Microsoft Entra ID | A refresh token was issued, and the refresh succeeded. The scope came back as `openid email profile`, separated by spaces. |
| Auth0 | A refresh token was issued, and the refresh succeeded. The response to the refresh carried no new refresh token, so this tenant does not rotate them. |
| Okta | No refresh token was issued, and the granted scopes left out `offline_access`: this application is not allowed the refresh token grant. |
| LINE | The code exchange and the refresh succeeded without the client secret, and a refresh token was issued. The granted scopes left out `email`, which the channel has not been approved for. |
| Facebook | The code exchange succeeded without the client secret, with PKCE. The token type came back as `bearer` in lower case, no scope was returned, and no refresh token was issued. |

## Consequences

- Applications that used the desktop packages move to `LoopbackOAuth2Client` and register a loopback redirect URI with each
  provider. `Caption`, `Width`, `Height`, `AuthorizationForm` and the desktop `OAuth2Manager` have no replacement, because
  the provider's page opens in the user's browser.
- A desktop application cannot keep a client secret confidential: anything distributed with it can be extracted.
- A redirect URI without a port means port 80. On macOS the probe could not listen on that port without administrator
  rights, so a redirect URI should name its port.
- The browser takes the focus during sign-in. The Windows Forms samples bring their window back to the front when the sign-in ends.
- `tests/Polhem.OAuth2.UnitTests/LoopbackListenerTests.cs` and `LoopbackOAuth2ClientTests.cs` cover port selection, the
  IPv6 port conflict, ignored requests without the state or with another `Host` header, an idle connection, the timeout,
  cancellation, and the redirect URI sent with the token request. On Windows the tests also run on .NET Framework, where the
  netstandard2.0 build of the listener is used.
