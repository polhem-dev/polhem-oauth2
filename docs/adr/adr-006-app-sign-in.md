# ADR-006: Sign in from mobile applications directly or through the application's own back end

**English** | [繁體中文](adr-006-app-sign-in.zh-TW.md)

## Status

Accepted (2026-09-19). Implemented on 2026-09-19; the results of signing in with the library are under "Test results".
Revised on 2026-09-21: the redirect URI rule became public, and the back-end relay calls it instead of repeating it; the relay
protects its cache entries, checks the application redirect URI again when it returns the sign-in, can start a sign-in
from the values of a request without throwing, and bounds the lifetime of a code.

## Context

Polhem.OAuth2 covers web applications (ASP.NET Core and System.Web) and desktop applications (`LoopbackOAuth2Client`,
ADR-004). .NET MAUI applications on Android, iOS and Mac Catalyst are not covered:

- `OAuth2Options` accepts only `http` and `https` redirect URIs, so every client rejects a custom URI scheme such as
  `com.example.app:/oauth2redirect`.
- `LoopbackOAuth2Client` opens the browser with `Process.Start`, which cannot open a browser on iOS or Android, and a mobile
  application cannot rely on listening on a loopback port.

.NET MAUI provides `WebAuthenticator`, which opens `ASWebAuthenticationSession` on iOS and Mac Catalyst and Custom Tabs on
Android, and returns the URI the provider redirected to. On .NET 10 it does not work on Windows
([dotnet/maui#2702](https://github.com/dotnet/maui/issues/2702)).

A mobile application cannot keep a client secret, so it is a public client (RFC 8252). Whether a provider lets such a
client redirect to the application depends on the provider and on the platform. On 2026-09-19 each provider was tested with
a throwaway MAUI application that built the authorization URL itself, opened it with `WebAuthenticator`, and exchanged the
code with PKCE and without a client secret:

| Provider | Registration | Redirect URI | iOS | Mac Catalyst | Android |
|----------|--------------|--------------|-----|--------------|---------|
| Google | iOS client type, with the bundle ID | `com.googleusercontent.apps.<client id>:/oauthredirect`, or a custom scheme equal to the bundle ID | Accepted | Accepted, with the same iOS client | — |
| Google | Android client type, with the package name and signing certificate | Either form above | — | — | Refused: "Custom URI scheme is not enabled for your Android client" |
| Microsoft Entra ID | iOS/macOS platform, or a custom URI under mobile and desktop applications | `msauth.<bundle id>://auth`, or `<scheme>://<path>` | Accepted | Accepted | — |
| Microsoft Entra ID | Android platform, with the package name and signature hash | `msauth://<package>/<signature hash>`, or `<scheme>://<path>` | — | — | Accepted |
| Auth0 | Native application, allowed callback URL | Custom scheme | Accepted | Accepted | Accepted |
| Okta | Native application, sign-in redirect URI | Custom scheme | Accepted | Accepted | Accepted |
| LINE | LINE Login channel, iOS bundle ID | `line3rdp.<bundle id>://auth` | Accepted | Accepted | — |
| LINE | LINE Login channel, Android package name and signature | `line3rdp.<package>://auth`, or a custom scheme | — | — | Refused: "Invalid redirect custom scheme" |
| Facebook | iOS platform with the bundle ID; no Android platform was needed | `fb<app id>://authorize` | Accepted | Accepted | Accepted |

Notes on the table:

- Every accepted combination exchanged the code without a client secret. For Facebook that is observed behavior: its
  documentation of the manual flow lists the client secret as required and does not mention PKCE, so Facebook could withdraw it.
  A retest on 2026-09-26 on the iOS simulator signed in to Google with the iOS client again without a client secret, while the
  desktop client of ADR-004 was refused without it that day: Google decides by the client type.
- The Entra ID portal refuses a custom URI in the form `<scheme>:/<path>`; it requires `<scheme>://`.
- Facebook redirects to `fb<app id>://authorize/`, with a trailing slash, and the token request succeeds only when its
  `redirect_uri` has that slash.
- Facebook and LINE refused the plain custom scheme `dev.polhem.redirectprobe:/oauth2redirect` on every platform tested.
- Auth0 marks a custom scheme as a non-verifiable callback URI and asks the user to confirm every sign-in. Microsoft asks a
  personal account to confirm that the application comes from a trusted source.
- Redirects to an https URI bound to the application (RFC 8252, section 7.2) were not tested.

## Decision

### Two ways to sign in from an application

- **Direct.** The application is a public client. It opens the authorization URL in the system browser session, the
  provider redirects to the application, and the application exchanges the code with PKCE. This is for applications without
  a back end of their own, such as an application that only calls the provider's API. The user information stays in the
  application and is not proof of identity for a server: an application that signs in to its own back end must let the back
  end obtain the user.
- **Back-end relay.** The application opens a sign-in URL on its own ASP.NET Core back end. The back end runs the existing
  web flow as a confidential client, then redirects to the application with a short-lived, single-use code. The application
  redeems the code at the back end over HTTPS, proving that it holds the verifier it created before the sign-in started, and
  receives the user information. The provider's tokens stay on the back end, which issues the application's own session.
  It uses only the existing web flow, and it is required where the table above shows no direct redirect: Google and LINE on
  Android.

The same `Polhem.OAuth2.AspNetCore` registration serves browser users and application users.

### Core: `AppOAuth2Client`

- A sealed `AppOAuth2Client` sits next to `LoopbackOAuth2Client` and is created from provider options:
  `AppOAuth2Client(OAuth2Options options, Func<Uri, Uri, CancellationToken, Task<Uri>> authenticate, HttpClient? httpClient = null)`.
  It has `SignInAsync` and `RefreshTokenAsync`, like `LoopbackOAuth2Client`.
- `authenticate` receives the authorization URL, the redirect URI and the cancellation token, opens the URL, and returns the
  URI the provider redirected to. The core package therefore does not depend on MAUI: with `WebAuthenticator` the delegate
  returns `WebAuthenticatorResult.CallbackUri`.
- The client parses the returned URI itself: `code`, `state`, `error` and `error_description` from the query or the
  fragment, percent-decoded with `+` read as a space, taking the first value of a repeated parameter. The web managers treat a
  repeated parameter as missing instead. Both are safe, because the state must match before any other value is used; they
  differ because the web frameworks hand over every value of a name, and joining them would match nothing.
- Redirect URIs: the client accepts an absolute URI with the `https` scheme or a custom scheme, and rejects `http`,
  `javascript`, `data` and `file`, relative URIs, and URIs with a fragment. A custom scheme is not required to contain a
  period, because the forms that Facebook (`fb<app id>`) and Entra ID on Android (`msauth`) require have none.
  `OAuth2Client` keeps accepting only `http` and `https`, so web applications gain no new redirect forms. The rule is the
  public `OAuth2Options.IsAppRedirectUri`, which the back-end relay calls for its application redirect URIs, so both apply
  one definition. ADR-005 rules out internal members of the core package, not its public API.
- The client always uses PKCE and sends no client secret unless one is set, in which case it follows
  `OAuth2Provider.RequiresClientSecret`, as the loopback client does.
- Failures, in addition to ADR-003: an `OperationCanceledException` from `authenticate`, such as the `TaskCanceledException`
  with which `WebAuthenticator` reports that the user closed the sign-in, and cancellation of the caller's token become a failed result with
  `OperationCanceledException`. A state mismatch, a provider error and a missing code become a failed result with
  `OAuth2Exception`. Any other exception from `authenticate` propagates, and so does a second sign-in on the same client
  (`InvalidOperationException`).
- `FacebookOAuth2Provider` adds the trailing slash to the redirect URI of the token request when an `AppOAuth2Client`
  signs in with a `fb<app id>` redirect URI that has none.
- The cancellation and failure mapping is shared with `LoopbackOAuth2Client`, not copied.

### ASP.NET Core: back-end relay

- `services.AddOAuth2AppRelay(options => ...)` registers the relay. `OAuth2AppRelayOptions.AppRedirectUris` lists the
  application redirect URIs the back end may redirect to, compared exactly, and `CodeLifetime` sets how long a relay code
  can be redeemed.
- `OAuth2Manager.RedirectToAppAuthorization(context, clientName, appRedirectUri, codeChallenge)` starts a relayed sign-in.
  It rejects an application redirect URI that is not registered, keeps the redirect URI and the S256 code challenge with the
  pending sign-in in the protected cookie of ADR-005, and redirects to the provider with the client's web redirect URI, so
  no additional redirect URI is registered with the provider. The three values come from the request in an endpoint that an
  application opens, so `TryRedirectToAppAuthorization` takes the same values and returns false when the client name, the
  redirect URI or the code challenge is not valid, for the endpoint to answer with status 400. The method that throws stays
  for values that the back end chooses itself, where a value that is not valid is a programming error (ADR-003). Each of the
  three methods that redirect has a counterpart that returns the URL instead, `CreateAppAuthorizationUrl`,
  `TryCreateAppAuthorizationUrl` and `CreateAppRedirectUrlAsync`, as the web sign-in has `CreateAuthorizationUrl`.
- The provider returns to the existing web callback. After `CompleteAuthorizationAsync`,
  `OAuth2Manager.RedirectToAppAsync(context, result)` returns false for a web sign-in. For a relayed sign-in it stores the
  user information under a new random code, redirects to the application redirect URI with that code, or with the error of a
  failed sign-in, and returns true.
- `OAuth2Manager.RedeemAppCodeAsync(clientName, code, codeVerifier, cancellationToken)` returns the user information when
  the code exists, has not expired, belongs to the client, and the verifier matches the stored challenge, and removes the
  code. Otherwise it returns null.
- Relay codes are stored in `IDistributedCache`: the in-memory cache for a single server, a distributed cache when several
  servers receive callbacks. No storage abstraction of the package's own is added. An entry is protected with ASP.NET Core
  data protection, under a purpose of its own, so the cache is not trusted: reading it does not reveal the user information,
  and an entry written without the keys is not redeemed. The key of an entry is a hash of the code.
- The cache ends the lifetime of a code, on its own clock, at `CodeLifetime`, which is at most the 10 minutes that RFC 6749,
  section 4.1.2, recommends for an authorization code. The entry also holds the time it expires, for a cache that returns an
  entry it should have dropped. That second check tolerates one minute, as the pending sign-in cookie does, so that a server
  whose clock runs ahead of the server that issued the code does not end the lifetime early. `OAuth2Manager` reads the time
  from `TimeProvider` when one is registered, which is how the tests move the clock.
- `RedirectToAppAsync` checks again that the application redirect URI of the sign-in is registered, because it may have been
  removed since the sign-in started. If it is not, the method throws `InvalidOperationException`, like a sign-in cookie that
  names a client that is no longer registered (ADR-003), and does not redirect. It stops when the callback request is
  aborted, as `CompleteAuthorizationAsync` does.
- Neither the redirect to the application nor the redeem response carries a provider token.

### Platforms on .NET 10

| Platform | Direct | Back-end relay | Loopback |
|----------|--------|----------------|----------|
| Android | `WebAuthenticator`, except Google and LINE | `WebAuthenticator` to the back end | Not applicable |
| iOS | `WebAuthenticator` | `WebAuthenticator` to the back end | Not applicable |
| Mac Catalyst | `WebAuthenticator` | `WebAuthenticator` to the back end | Possible: the sandbox needs `com.apple.security.network.server`, without which binding a loopback port fails with "Permission denied" |
| Windows | Not available until `WebAuthenticator` works there | Not provided | `LoopbackOAuth2Client` |

The Mac Catalyst loopback result comes from a listener written in the test application, not from `LoopbackOAuth2Client`.

### Scope

- No `Polhem.OAuth2.Maui` package. The core stays independent of UI frameworks, and the MAUI wiring lives in a sample and the
  README. A MAUI package would make every build of the solution require the MAUI workload.
- No back-end relay for System.Web.
- No back-end relay for desktop applications, including the Windows platform of a MAUI application, which keep
  `LoopbackOAuth2Client`. The Windows platform is reconsidered once `WebAuthenticator` works there.
- After a relayed sign-in the application receives only the user information, not the provider's tokens, so no provider
  token has to be stored on the device.

## Test results

### Real providers

Signed in on 2026-09-19 with the library through the `samples/OAuthMaui` application and, for the back-end relay, the
`samples/OAuthAspNetCore` back end. iOS ran on the iPhone 17 Pro simulator (iOS 26.5), Mac Catalyst on macOS 26.6, and
Android on an emulator with Android 15.

| Provider | Direct, iOS | Direct, Mac Catalyst | Direct, Android | Back-end relay |
|----------|-------------|----------------------|-----------------|----------------|
| Google | Accepted | Accepted | Not possible (see the context) | Accepted on Mac Catalyst |
| Microsoft Entra ID | Test application only | Accepted | Accepted | Not tested |
| Auth0 | Test application only | Accepted | Accepted | Not tested |
| Okta | Test application only | Accepted | Accepted | Not tested |
| LINE | Test application only | Accepted; the user information had no email address | Not possible (see the context) | Not tested |
| Facebook | Test application only | Accepted, with the trailing slash added by `FacebookOAuth2Provider` | Accepted | Not tested |

- "Test application only" means that the throwaway application of the context section signed in, but the library was not
  run on iOS with that provider. iOS and Mac Catalyst run the same code path and share the provider registrations.
- The back-end relay was tested with real providers only on Mac Catalyst. On iOS and Android the simulator or emulator
  would have to trust the development certificate of the back end; the relay is covered there by the end-to-end tests below.
- Windows has not been tested with real providers.
- Closing the sign-in, all through the library: on Android, closing Custom Tabs with the back button; on iOS, canceling
  the system prompt before a direct sign-in and before a relayed one; on Mac Catalyst, closing the sign-in window. Each
  became a failed result with `OperationCanceledException`, which the sample shows as canceled. Canceling on Google's own
  page on Mac Catalyst returned `access_denied`, which became a failed result with `OAuth2Exception`, as for any provider
  error.

### Retest of 2026-09-26 on iOS and Android

Signed in with the library of `main` after 1.2.0 through `samples/OAuthMaui` on the iPhone 17 Pro simulator (iOS 26.5) and
an Android 15 emulator, with `samples/OAuthAspNetCore` as the back end. For the relay, the development certificate of the
back end was added to the simulator's trusted roots with `xcrun simctl keychain <device> add-root-cert`, and to the
emulator's user certificates as `tests/Polhem.OAuth2.DeviceTests/scripts/prepare-android-emulator.sh` does, with
`adb reverse tcp:7032 tcp:7032`.

| Provider | Direct, iOS | Relay, iOS | Direct, Android | Relay, Android |
|----------|-------------|------------|-----------------|----------------|
| Google | Accepted, without a client secret | Accepted | Not possible (see the context) | Accepted |
| Microsoft Entra ID | Accepted | Accepted | Accepted | Accepted |
| Auth0 | Accepted | Accepted, after Auth0's consent page for the web application | Accepted | Accepted, after the same consent page |
| Okta | Accepted | Accepted | Accepted | Accepted |
| LINE | Accepted; no email address | Accepted; no email address | Not possible (see the context) | Accepted; no email address |
| Facebook | Accepted | Accepted | Accepted | Accepted |

- This closes the "Test application only" and "Not tested" cells above for iOS, and the relay on Android.
- Before the certificate was trusted, the relay stopped at Safari's certificate warning. Closing it became a failed result
  with `OperationCanceledException`, shown by the sample as canceled.

### Automated tests

- `tests/Polhem.OAuth2.DeviceTests` runs the unit tests of the core package on Android, iOS, Mac Catalyst and Windows, in
  Release, so the core package is trimmed and, on iOS, compiled ahead of time as in an application.
- The same application signs in to `tests/Polhem.OAuth2.FakeProvider`, which stands in for Auth0 over HTTPS: directly, with
  a provider error, and through the back-end relay on Android, iOS and Mac Catalyst, and with `LoopbackOAuth2Client` and the
  default browser on Windows.
- The Device Tests workflow runs both on all four platforms when started by hand, because it takes far longer than the build.
  A user closing the sign-in cannot be automated there and was checked by hand as above.

## Consequences

- An application that signs in with Google on both iOS and Android needs a client of each type, with different client IDs
  and redirect URIs, so application settings must be able to differ by platform.
- Another application can register the same custom scheme. PKCE keeps an intercepted code from being exchanged, and a relay
  code cannot be redeemed without the verifier held by the application that started the sign-in.
- `IDistributedCache` has no atomic read-and-remove. Two redeem requests for the same code that arrive at the same moment
  could both succeed; both need the verifier of the application that started the sign-in.
- Every server that can receive a relayed callback must share the data protection key ring, as in ADR-005, and the relay
  cache. The key ring also protects the entries of the relay cache.
- Version 1.1.0 did not protect the entries. While servers of both versions share a cache, a code issued by one version is
  not redeemed by the other, and the application has to sign in again. That lasts for the lifetime of a code.
- A relayed sign-in, like a web sign-in, must start and end on HTTPS pages and complete within the lifetime of the pending
  sign-in cookie.
- The additions were declared in `PublicAPI.Unshipped.txt` and moved to `PublicAPI.Shipped.txt` with the release of 1.1.0, as
  every addition is: the public API analyzer checks the existing API against the shipped file.
