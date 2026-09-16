# Loopback redirect probe

**English** | [繁體中文](README.zh-TW.md)

A console tool that signs in once with one provider through the system browser and a loopback redirect URI, such as
`http://127.0.0.1:53682/callback`. It signs in with `LoopbackOAuth2Client` from Polhem.OAuth2, the same code that
applications use, and shows whether the provider accepts that redirect URI and whether the authorization code can be exchanged.

## Set up

1. Register the redirect URI you want to test with the provider.
2. Copy `OAuthConfig.example.json` in the repository root to `OAuthConfig.json`. For each provider you test, fill in the
   client credentials and the registered `RedirectUri` in the `Desktop` section of that provider. `OAuthConfig.json` is
   ignored by git; do not commit credentials. The build copies it to the output folder, where the probe reads it. The
   samples share this file, and the "Samples" section of the [repository README](../../README.md) describes its shape.

Providers compare redirect URIs differently, so each `Desktop` section has its own `RedirectUri`. Always write the port:
a URI without one means port 80, which a program without administrator rights cannot listen on in macOS. Port `0` picks
a free port, for providers that accept any loopback port.

## Run

```bash
cd tools/LoopbackRedirectProbe
dotnet run -- --provider Google
```

| Option | Meaning |
|--------|---------|
| `--provider` | `Google`, `Facebook`, `Line`, `Azure`, `Auth0` or `Okta` |
| `--redirect` | Optional. A loopback redirect URI that replaces the `RedirectUri` of the provider's `Desktop` section for this run. |
| `--settings` | The settings file. The default is `OAuthConfig.json` in the output folder, which the build copies from the repository root. |
| `--timeout` | Seconds to wait for the redirect. The default is 180. |

## Reading the result

| Exit code | Output | Meaning |
|-----------|--------|---------|
| 0 | `The provider accepted the loopback redirect, and the code exchange succeeded.` | The redirect URI works end to end. |
| 1 | `No redirect with the state of this sign-in arrived before the timeout.` | The browser never reached the redirect URI. The provider page usually shows a redirect URI error. |
| 1 | `The provider accepted the redirect, but the code exchange failed` | The redirect URI is accepted, but the token request failed, for example because the provider requires the client secret. |
| 1 | `The sign-in failed` | The provider redirected back with an error, such as `access_denied`, or its response could not be read. |
| 2 | Any other message | The arguments, the settings file or the listening socket are wrong. |

The tool prints the user identifier, name and email address of the account that signed in. It never prints tokens.
