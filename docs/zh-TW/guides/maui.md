# .NET MAUI 應用程式

[English](../../en/guides/maui.md) | **繁體中文**

Android、iOS 或 Mac Catalyst 上的應用程式有兩種登入方式（[ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md)）：

- **直連**：給沒有自己後端的應用程式。`AppOAuth2Client` 以 `WebAuthenticator` 開啟登入頁，provider 導回應用程式的 URI scheme，
  client 再以 PKCE、不帶 client secret 用授權碼換取 token。
- **經由後端中轉**：給要登入自己 ASP.NET Core 後端的應用程式。後端以 web client 身分登入，交給應用程式一個只能用一次的
  code，應用程式再以它兌換使用者資訊。Google 與 LINE 不接受直接導回 Android 應用程式，所以在 Android 上必須用這個方式。
  GitHub 每次換 token 都要求 client secret，Sign in with Apple 則是每個 token 請求都要求，所以應用程式只能用這個方式登入它們。

`WebAuthenticator` 在 Windows 上無法使用，所以 MAUI 應用程式的 Windows 平台跟[桌面應用程式](desktop.md)一樣，以
`LoopbackOAuth2Client` 登入。

## 直連登入

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

- 回呼網址是自訂 scheme 或 `https` 網址。`http`、`javascript`、`data`、`file`、相對網址，以及帶 fragment 的網址都會被拒絕。
- 每個 provider 要求各自的導回形式與後台登記，例如 Facebook 是 `fb<app id>://authorize`，LINE 在 iOS 上是
  `line3rdp.<bundle id>://auth`。各 provider 的文件寫有它的形式，[ADR-006](../../adr/adr-006-app-sign-in.zh-TW.md)
  的表格列出每一家，以及實測過的平台。
- 不要設定 client secret：隨應用程式散佈的任何內容都能被取出。
- 使用者中途關閉登入，會成為帶 `OperationCanceledException` 的失敗結果；provider 回傳錯誤（例如 `access_denied`）則是帶
  `OAuth2Exception` 的失敗結果。
- 使用者資訊只留在應用程式裡，不能當作伺服器端的身份證明；要登入自己後端的應用程式，請改用後端中轉。

## 平台設定

應用程式必須能接收導回自己 scheme 的網址：

- iOS 與 Mac Catalyst：在 `Info.plist` 的 `CFBundleURLTypes` 列出 scheme。在 App Sandbox 裡執行的 Mac Catalyst 應用程式，
  另外需要 `com.apple.security.network.client` entitlement。
- Android：加一個繼承 `WebAuthenticatorCallbackActivity` 的 activity，以 intent filter 接收該 scheme；並在
  `AndroidManifest.xml` 的 `<queries>` 宣告 Custom Tabs 服務，Android 11 以後要有這項宣告才能開啟它。

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

## 經由後端中轉登入

後端照 [ASP.NET Core](aspnetcore.md) 指南登記 web client，再登記應用程式的回呼網址：

```csharp
builder.Services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("com.example.app:/relay"));
```

- 也可以改用 `AddOAuth2AppRelay(builder.Configuration.GetSection("AppRelay"))` 從組態讀取同樣的設定：`AppRedirectUris` 可以是陣列或單一值，
  `CodeLifetime` 的寫法如 `00:01:00`。區段裡出現其他 key 會視為錯誤，拼錯的設定不會被默默忽略。
- 應用程式產生 PKCE 的 code verifier，帶著自己的回呼網址與 S256 code challenge 開啟後端的網址。該端點呼叫
  `oauth2Manager.TryRedirectToAppAuthorization(HttpContext, "Google", redirectUri, codeChallenge)`，回傳 false 時回應 400：
  任何人都能帶任意值開啟這個端點，所以沒有登記的回呼網址只是一個普通的請求，不是例外狀況。
- provider 導回後端原本的回呼端點。`CompleteAuthorizationAsync` 之後，
  `oauth2Manager.RedirectToAppAsync(HttpContext, result, cancellationToken)` 會把由應用程式發起的登入，帶著一次性 code
  導回應用程式並回傳 true；瀏覽器裡的登入則回傳 false。回傳 true 時直接結束回呼，不要再讓使用者登入網站：
  這次登入所在的瀏覽器工作階段，可能與裝置上的瀏覽器共用 cookie。`CreateAppAuthorizationUrl`、`TryCreateAppAuthorizationUrl` 與
  `CreateAppRedirectUrlAsync` 只回傳網址、不導向，給想自己導向的處理常式使用。
- 應用程式把 code 與它的 verifier POST 給後端，後端以 `oauth2Manager.RedeemAppCodeAsync` 取得使用者資訊，或得到 null。
  接著由後端發給應用程式自己的 session；provider 的 token 留在後端。
- code 存在 `IDistributedCache`。有多台伺服器時，改用分散式快取並共用 data protection 金鑰，跟網頁登入一樣。
  code 兌換之前，快取裡的使用者資訊也由這組金鑰保護。
- [OAuthAspNetCore](../../../samples/OAuthAspNetCore) 與 [OAuthMaui](../../../samples/OAuthMaui) 兩個 sample 示範了兩端的寫法。
