# 其他伺服器端框架

[English](../../en/guides/other-server-frameworks.md) | **繁體中文**

核心套件的 `OAuth2Client` 不依賴任何 HTTP 框架，走的是與網頁套件同一套流程。待完成的值要存放在只有發起登入的瀏覽器才拿得出來的地方，
例如加密的 HTTP-only cookie，並在處理完回呼後刪除。

```csharp
var client = new OAuth2Client(options);

// Start the sign-in. Keep and Redirect stand for code of your framework.
AuthorizationRequest request = client.CreateAuthorizationRequest();
Keep(request.Pending.State, request.Pending.CodeVerifier, request.Pending.RedirectUri);
Redirect(request.Url);

// Complete it in the callback.
var pending = new PendingAuthorization(keptState, keptCodeVerifier, keptRedirectUri);
var callback = new AuthorizationCallback(query["code"], query["state"], query["error"], query["error_description"]);
AuthorizationResult result = await client.CompleteAuthorizationAsync(callback, pending, cancellationToken);
```

`client.UsesFormPostResponse` 為 true 時（例如 Sign in with Apple），provider 會以 form POST 導回：改從 form 而不是 query
讀取這些值，並把其中的 `user` 欄位傳給接受它的 `AuthorizationCallback` 建構式，這個欄位帶著使用者的姓名。網頁套件怎麼保存
這種登入，見[網頁應用程式：登入狀態怎麼保存](web-sign-in-state.md)。
