# Samples

**English** | [繁體中文](../../zh-TW/guides/samples.md)

| Sample | Shows |
|--------|-------|
| [OAuthConsole](../../../samples/OAuthConsole) | Desktop sign-in from a console application, on any operating system |
| [OAuthDesktop](../../../samples/OAuthDesktop) | Desktop sign-in from Windows Forms on .NET |
| [OAuthWinForms](../../../samples/OAuthWinForms) | Desktop sign-in from Windows Forms on .NET Framework 4.8 |
| [OAuthAspNetCore](../../../samples/OAuthAspNetCore) | ASP.NET Core, including the relay endpoints for OAuthMaui |
| [OAuthMaui](../../../samples/OAuthMaui) | .NET MAUI: direct and back-end relay sign-in on Android, iOS and Mac Catalyst, loopback sign-in on Windows. It needs the MAUI workload and is not part of the solution. |

[LoopbackRedirectProbe](../../../tools/LoopbackRedirectProbe/README.md) checks whether a provider accepts a loopback
redirect URI before you build on it.

## Settings

The samples and the loopback redirect probe read their provider settings from one `OAuthConfig.json` in the repository
root. Copy `OAuthConfig.example.json` to `OAuthConfig.json` and fill it in; the build copies the file to the output
folder of each project. `OAuthConfig.json` is ignored by git; keep credentials out of `OAuthConfig.example.json`.

Every provider holds one client per client type, because a provider registers desktop, web and mobile clients
separately:

```json
{
  "Providers": {
    "Okta": {
      "Domain": "",
      "Desktop": { "ClientId": "", "RedirectUri": "http://localhost:53682/callback" },
      "Web": { "ClientId": "", "ClientSecret": "", "RedirectUri": "https://localhost:7032/auth/callback" }
    }
  }
}
```

- The console and Windows Forms samples and the probe read `Desktop`, and the ASP.NET Core sample reads `Web`. A client
  section takes the properties of that provider's options type, such as `Scopes` and `UsePkce`, and what it leaves out
  keeps the default of the type.
- The fields both clients share, none of which are credentials, sit in the provider section: `Domain` for Auth0 and
  Okta, `AuthorizationServerId` for Okta, and `Tenant` for Azure. Anything else there is an error, so credentials cannot
  end up shared by accident.
- A provider whose back end registers one client for both, such as a LINE channel with two callback URLs, gets the same
  `ClientId` and `ClientSecret` in both sections.
- A section whose `ClientId` is still empty is skipped, so the samples offer only the providers you filled in.
- The OAuthMaui sample reads `iOS` on iOS and Mac Catalyst, `Android` on Android, and `Desktop` on Windows. It also reads the
  `ClientId` of each `Web` section, to offer a sign-in through the back end for the providers that have one. An `iOS` or
  `Android` section cannot hold a `ClientSecret`, because an application cannot keep one: loading such a file fails. The
  build of OAuthMaui packages only `ClientId`, `RedirectUri`, `Scopes`, `UsePkce` and the shared fields, and fails if the
  packaged settings would hold a field whose name contains `Secret` or `PrivateKey`.
  Google and LINE refuse a direct redirect to an Android application
  ([ADR-006](../../adr/adr-006-app-sign-in.md)), so they have no `Android` section, and the sample signs in to them through
  the back end there. GitHub and Apple require the client secret, so they have neither an `iOS` nor an `Android` section.
- Apple's `Web` section names the `.p8` file in `PrivateKeyPath`, a path outside the repository such as
  `~/.config/polhem-oauth2/AuthKey_<key id>.p8`, and the sample reads the key from it. A `PrivateKey` written into the settings
  file is an error. Apple refuses a localhost return URL, so point `RedirectUri`, and `AppRelay.BackendUrl` for the relay, at a
  public https host that forwards to the ASP.NET Core sample, such as a tunnel.
- The top-level `AppRelay` section configures the back-end relay: `BackendUrl` is where the ASP.NET Core sample runs, and
  `RedirectUri` is the relay callback of the application, which the ASP.NET Core sample registers with
  `AddOAuth2AppRelay`. The relay runs over HTTPS, so the simulator or emulator must trust the ASP.NET Core development
  certificate. On the Android emulator, run `adb reverse tcp:7032 tcp:7032` so that `localhost` reaches the host.
