# 桌面與主控台應用程式

[English](../../en/guides/desktop.md) | **繁體中文**

`LoopbackOAuth2Client` 會在回呼網址上監聽、用預設瀏覽器開啟授權網址、等待 provider 導回，再用授權碼換 token。
可在 Windows、macOS、Linux 上使用。

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

- 回呼網址必須是 `http`，主機必須是 `localhost` 或 loopback 位址，而且要在 provider 後台登記。
  port 寫 0 時每次登入都會挑一個可用的 port，只適用於接受任意 loopback port 的 provider。
- client 一律使用 PKCE。隨桌面應用程式散佈的 client secret 可以被取出，所以不會送出，只有 Google、LINE 與 GitHub 例外，它們沒有 secret 時會拒絕部分請求（實測紀錄見 ADR-004）。
- 逾時（`Timeout`，預設 5 分鐘）、取消、provider 回傳錯誤，都會成為失敗結果。port 無法監聽時擲出 `SocketException`，
  找不到預設瀏覽器時擲出 `Win32Exception`（iOS 無法啟動處理程序，擲出 `PlatformNotSupportedException`）。
- 要用其他方式開啟網址時設定 `OpenBrowser`，例如搭配 Avalonia 的 `ILauncher` 寫成 `uri => launcher.LaunchUriAsync(uri)`，
  或在 .NET MAUI 裡寫成 `url => Launcher.Default.OpenAsync(url)`。
- 這段範例使用最上層陳述式。在 .NET Framework 的 Windows Forms 應用程式裡怎麼登入，見 [OAuthWinForms](../../../samples/OAuthWinForms) sample。
- 目標框架為 .NET Framework 4.7.2、並在開啟 FIPS 模式的機器上執行的應用程式，需要
  [ASP.NET（System.Web）](aspnet-system-web.md#部署前確認)「部署前確認」裡的設定。
- 這是沒有自家後端的應用程式的登入方式。有後端的應用程式經由後端登入，見下方說明。

這個設計的理由，以及各 provider 對 loopback 回呼的實測結果，記錄在
[ADR-004](../../adr/adr-004-system-browser-loopback.zh-TW.md)。

## 登記 loopback 回呼網址

各 provider 要登記的應用程式類型、port 是否必須一致、接受 `localhost` 還是 `127.0.0.1`，都不一樣。
每個 provider 的文件都有一節說明桌面登入與實測過的登記方式：

- [Google](../providers/google.md#桌面與主控台應用程式)
- [Facebook](../providers/facebook.md#桌面與主控台應用程式)
- [LINE](../providers/line.md#桌面與主控台應用程式)
- [Microsoft Entra ID](../providers/microsoft-entra-id.md#桌面與主控台應用程式)
- [Auth0](../providers/auth0.md#桌面與主控台應用程式)
- [Okta](../providers/okta.md#桌面與主控台應用程式)
- [GitHub](../providers/github.md#桌面與主控台應用程式)
- [其他任何 OpenID Connect provider](../providers/openid-connect.md#桌面與主控台應用程式)

Sign in with Apple 沒有直接的桌面登入：`LoopbackOAuth2Client` 會拒絕 `AppleOAuth2Options`。有後端的桌面應用程式經由後端登入 Apple。

沒寫 port 的回呼網址等於 port 80，監聽它通常需要管理員權限，所以請寫出 port。

[LoopbackRedirectProbe](../../../tools/LoopbackRedirectProbe/README.zh-TW.md) 可以在動工前先確認 provider 接不接受某個
loopback 回呼網址。

## 經由後端登入

有自家 ASP.NET Core 後端的應用程式經由後端登入，理由見[有後端的應用程式](separate-back-end.md)。`LoopbackRelayClient`
用預設瀏覽器開啟後端的登入網址，後端以網頁 client 登入，再帶著一次性代碼導回 loopback 位址：

```csharp
using Polhem.OAuth2;

var client = new LoopbackRelayClient("http://127.0.0.1:0/callback");
RelaySignInResult result = await client.SignInAsync((redirectUri, codeChallenge) => new Uri(
    $"https://api.example.com/auth/app/GitHub?redirect_uri={Uri.EscapeDataString(redirectUri.AbsoluteUri)}&code_challenge={codeChallenge}"));

if (result.IsSuccess)
{
    // 把 result.Code 與 result.CodeVerifier POST 給後端，後端以 RedeemAppCodeAsync 兌換，並發出應用程式自己的 session。
}
```

- 後端的設定見 .NET MAUI 指南的[經由後端中轉登入](maui.md#經由後端中轉登入)，loopback 網址登記時不帶 port，任何 port 都會被接受：
  `options.AppRedirectUris.Add("http://127.0.0.1/callback")`。只接受 `127.0.0.1` 與 `[::1]`，不接受 `localhost`。
- client 會在回呼網址加上實際監聽的 port 與 `state` 參數，例如 `http://127.0.0.1:53682/callback?state=...`，並且只等帶著這個
  state 的導回：瀏覽器裡任何網頁都能對 loopback 位址送出請求。
- client 回傳代碼，以及當初產生 challenge 的 PKCE code verifier。應用程式怎麼把它們交給後端、後端回傳什麼，由兩者自行約定。
- 逾時、取消、後端回傳錯誤，都會成為失敗結果，與 `LoopbackOAuth2Client` 相同。
- 電腦上任何程式都能監聽 loopback port，所以不帶 port 登記的後端，也會中轉給這樣的程式，RFC 8252 對 loopback 回呼接受這一點。
  應用程式若能固定監聽同一個 port，請帶 port 登記。
- provider 只看得到後端，所以沒有桌面登入的 provider 也能用這個方式：Sign in with Apple，以及不必隨程式附上 client secret 的 GitHub。
- [OAuthConsole](../../../samples/OAuthConsole) sample 加上 `--back-end` 就會用這個方式，向 [OAuthAspNetCore](../../../samples/OAuthAspNetCore)
  sample 登入。
