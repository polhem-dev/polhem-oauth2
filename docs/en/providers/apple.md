# Sign in with Apple

**English** | [繁體中文](../../zh-TW/providers/apple.md)

`AppleOAuth2Options` signs in a web application with an Apple Account through OpenID Connect. The provider name of its
results is `Apple`. The decision and the tests are recorded in [ADR-009](../../adr/adr-009-sign-in-with-apple.md).

## Options

```csharp
builder.Services.AddOAuth2Client("Apple", new AppleOAuth2Options
{
    ClientId = "com.example.web",          // the Services ID
    TeamId = "your-team-id",
    KeyId = "your-key-id",
    PrivateKey = File.ReadAllText(keyPath), // the .p8 file of the Sign in with Apple key
    RedirectUri = "https://www.example.com/auth/callback"
});
```

- The library signs the client secret with `PrivateKey` and replaces it before it expires. A client secret signed in advance
  can be set in `ClientSecret` instead, and is then yours to replace. Keep the `.p8` file out of source control, as a secret.
- Apple refuses localhost and IP return URLs, so development needs a public https host for the callback, such as a tunnel.
- The options shared by every provider are described in [Options](../guides/options.md).

## Desktop and console applications

Not supported. `LoopbackOAuth2Client` refuses these options, because Apple accepts only an https return URL on a domain name
and requires the client secret for every token request.

## .NET MAUI applications

Mobile applications sign in through the back end, as
[Signing in through the back end](../guides/maui.md#signing-in-through-the-back-end) describes. `AppOAuth2Client` refuses
these options for the same reasons as the loopback client.

## Web applications

- The default scopes `name email` make Apple return to the callback with a form POST from its own site: map the callback for
  GET and POST and leave it out of antiforgery validation. See
  [Web applications: how a sign-in is kept](../guides/web-sign-in-state.md).
- Follow the guide for [ASP.NET Core](../guides/aspnetcore.md) or [ASP.NET (System.Web)](../guides/aspnet-system-web.md). Another
  server framework passes the `user` field of the form to `AuthorizationCallback`; see
  [Other server frameworks](../guides/other-server-frameworks.md).

## Tokens and users

- `UserId` and `Email` come from the ID token. The name comes only with the first sign-in of a user, so store it then;
  `UserName` is null afterwards.
- `Email` can be an address of Apple's private email relay.

## Apps with a separate back end

`GetUserInfoAsync` throws `NotSupportedException`: the front end sends its ID token, which the back end validates. See
[Apps with a separate back end](../guides/separate-back-end.md).
