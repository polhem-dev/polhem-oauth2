# Other server frameworks

**English** | [繁體中文](../../zh-TW/guides/other-server-frameworks.md)

`OAuth2Client` in the core package runs the same flow as the web packages without an HTTP framework. Keep the pending
values where only the browser that started the sign-in can present them, such as an encrypted, HTTP-only cookie, and
remove them when the callback is handled.

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

When `client.UsesFormPostResponse` is true, as for Sign in with Apple, the provider returns with a form POST: read the
values from the form instead of the query, and pass its `user` field to the `AuthorizationCallback` constructor that takes
it, which carries the name of the user. [Web applications: how a sign-in is kept](web-sign-in-state.md) describes how the
web packages keep such a sign-in.
