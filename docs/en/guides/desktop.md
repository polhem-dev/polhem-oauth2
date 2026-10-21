# Desktop and console applications

**English** | [繁體中文](../../zh-TW/guides/desktop.md)

`LoopbackOAuth2Client` listens on the redirect URI, opens the authorization URL in the default browser, waits for the
provider to redirect back, and exchanges the authorization code. It works on Windows, macOS and Linux.

```csharp
using Polhem.OAuth2;

var options = new GoogleOAuth2Options
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "http://127.0.0.1:0/callback"
};

var client = new LoopbackOAuth2Client(options);
AuthorizationResult result = await client.SignInAsync();

if (result.IsSuccess)
    Console.WriteLine($"{result.UserInfo.UserId} {result.UserInfo.UserName} {result.UserInfo.Email}");
else
    Console.WriteLine($"The sign-in failed: {result.Exception.Message}");
```

- The redirect URI must be an `http` URI on `localhost` or a loopback address, and it must be registered with the provider.
  Port 0 picks a free port for each sign-in, which only works with providers that accept any loopback port.
- The client always uses PKCE. A client secret shipped with a desktop application can be extracted, so it is not sent,
  except to Google, LINE and GitHub, which refuse some requests without it (ADR-004 records the tests).
- A timeout (`Timeout`, 5 minutes by default), cancellation, or an error from the provider becomes a failed result. A port
  that cannot be listened on throws `SocketException`, and a missing default browser throws `Win32Exception`
  (`PlatformNotSupportedException` on iOS, which cannot start a process).
- Set `OpenBrowser` to open the URL another way, for example `uri => launcher.LaunchUriAsync(uri)` with the `ILauncher` of
  Avalonia, or `url => Launcher.Default.OpenAsync(url)` in .NET MAUI.
- The snippet uses top-level statements. The [OAuthWinForms](../../../samples/OAuthWinForms) sample shows the same sign-in in
  a Windows Forms application on .NET Framework.
- An application that targets .NET Framework 4.7.2 and runs on a machine with FIPS mode enabled needs the setting described
  under "Before deploying" in [ASP.NET (System.Web)](aspnet-system-web.md#before-deploying).
- To sign in to a separate back end as well, see [Apps with a separate back end](separate-back-end.md).

The reasons behind this design, and how each provider handled loopback redirects, are recorded in
[ADR-004](../../adr/adr-004-system-browser-loopback.md).

## Registering a loopback redirect URI

Providers differ in the application type to register, in whether the port must match, and in whether they accept
`localhost` or `127.0.0.1`. Each provider's document has a section on the desktop and the tested registration:

- [Google](../providers/google.md#desktop-and-console-applications)
- [Facebook](../providers/facebook.md#desktop-and-console-applications)
- [LINE](../providers/line.md#desktop-and-console-applications)
- [Microsoft Entra ID](../providers/microsoft-entra-id.md#desktop-and-console-applications)
- [Auth0](../providers/auth0.md#desktop-and-console-applications)
- [Okta](../providers/okta.md#desktop-and-console-applications)
- [GitHub](../providers/github.md#desktop-and-console-applications)
- [Any other OpenID Connect provider](../providers/openid-connect.md#desktop-and-console-applications)

Sign in with Apple has no desktop sign-in: `LoopbackOAuth2Client` refuses `AppleOAuth2Options`.

A redirect URI without a port means port 80, which usually needs administrator rights to listen on. Name the port.

[LoopbackRedirectProbe](../../../tools/LoopbackRedirectProbe/README.md) checks whether a provider accepts a loopback
redirect URI before you build on it.
