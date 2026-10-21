# .NET MAUI applications

**English** | [繁體中文](../../zh-TW/guides/maui.md)

An application on Android, iOS or Mac Catalyst signs in in one of two ways
([ADR-006](../../adr/adr-006-app-sign-in.md)):

- **Directly**, for an application without a back end of its own. `AppOAuth2Client` opens the sign-in with
  `WebAuthenticator`, the provider redirects to a URI scheme of the application, and the client exchanges the code with
  PKCE and without a client secret.
- **Through the back end**, for an application that signs in to its own ASP.NET Core back end. The back end signs in as a
  web client and gives the application a single-use code, which the application redeems for the user information. Google
  and LINE accept no direct redirect to an Android application, so on Android they need this way. GitHub requires the client
  secret for every code exchange, and Sign in with Apple for every token request, so an application signs in to them only
  this way.

`WebAuthenticator` does not work on Windows, so the Windows platform of a MAUI application signs in with
`LoopbackOAuth2Client`, as a [desktop application](desktop.md) does.

## Signing in directly

```csharp
using Polhem.OAuth2;

var options = new Auth0OAuth2Options
{
    Domain = "your-tenant.auth0.com",
    ClientId = "your-native-client-id",
    RedirectUri = "com.example.app:/oauth2redirect"
};

var client = new AppOAuth2Client(options, async (url, redirectUri, cancellationToken) =>
    (await WebAuthenticator.Default.AuthenticateAsync(url, redirectUri)).CallbackUri);

AuthorizationResult result = await client.SignInAsync();
```

- The redirect URI is a custom scheme or an `https` URI. `http`, `javascript`, `data` and `file` URIs, relative URIs, and
  URIs with a fragment are rejected.
- Each provider needs its own redirect form and registration, such as `fb<app id>://authorize` for Facebook and
  `line3rdp.<bundle id>://auth` for LINE on iOS. Each provider's document gives its form, and the table in
  [ADR-006](../../adr/adr-006-app-sign-in.md) lists them with the platforms tested.
- Set no client secret: anything shipped with an application can be extracted.
- Closing the sign-in becomes a failed result with `OperationCanceledException`, and an error from the provider, such as
  `access_denied`, one with `OAuth2Exception`.
- The user information stays in the application. It is no proof of identity for a server, so an application that signs in
  to its own back end signs in through the back end.

## Platform settings

The application must receive the redirect to its scheme:

- iOS and Mac Catalyst: list the scheme under `CFBundleURLTypes` in `Info.plist`. A Mac Catalyst application in the App
  Sandbox also needs the `com.apple.security.network.client` entitlement.
- Android: add an activity that derives from `WebAuthenticatorCallbackActivity`, with an intent filter for the scheme, and
  declare the Custom Tabs service under `<queries>` in `AndroidManifest.xml`, which Android 11 and later require to open it.

```csharp
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "com.example.app")]
public class CallbackActivity : WebAuthenticatorCallbackActivity
{
}
```

```xml
<queries>
  <intent>
    <action android:name="android.support.customtabs.action.CustomTabsService" />
  </intent>
</queries>
```

## Signing in through the back end

The back end registers its web clients as in the [ASP.NET Core](aspnetcore.md) guide, and the redirect URIs of its
applications:

```csharp
builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("com.example.app:/relay"));
```

- `AddOAuth2AppRelay(builder.Configuration.GetSection("AppRelay"))` reads the same settings from configuration instead:
  `AppRedirectUris`, an array or a single value, and `CodeLifetime`, such as `00:01:00`. Any other key in the section is an
  error, so a misspelled setting is not ignored.
- The application creates a PKCE code verifier and opens a URL of the back end with its redirect URI and the S256 code
  challenge. That endpoint calls `oauth2Manager.TryRedirectToAppAuthorization(HttpContext, "Google", redirectUri, codeChallenge)`
  and answers with status 400 when it returns false: anyone can open the endpoint with any values, so a redirect URI that is
  not registered is an ordinary request, not an exception.
- The provider returns to the back end's usual callback. After `CompleteAuthorizationAsync`,
  `oauth2Manager.RedirectToAppAsync(HttpContext, result, cancellationToken)` sends a sign-in that an application started
  back to the application with a single-use code and returns true. For a sign-in in the browser it returns false. When it
  returns true, return without signing the user in to the web application: the browser session of the sign-in can share its
  cookies with the browser of the device. `CreateAppAuthorizationUrl`, `TryCreateAppAuthorizationUrl` and
  `CreateAppRedirectUrlAsync` return the URL instead of redirecting, for a handler that redirects in its own way.
- The application posts the code and its verifier to the back end, where `oauth2Manager.RedeemAppCodeAsync` returns the
  user information, or null. The back end then issues the application's own session; the provider's tokens stay on the
  back end.
- The codes are kept in `IDistributedCache`. With several servers, use a distributed cache and share the data protection
  keys, as for web sign-ins. The keys also protect the user information that the cache holds until the code is redeemed.
- The [OAuthAspNetCore](../../../samples/OAuthAspNetCore) and [OAuthMaui](../../../samples/OAuthMaui) samples show both sides.
