# Other OpenID Connect providers

**English** | [繁體中文](../../zh-TW/providers/openid-connect.md)

`OidcOAuth2Options` signs in with any provider that follows OpenID Connect, such as Keycloak, GitLab, AWS Cognito or
Authentik. [ADR-008](../../adr/adr-008-generic-openid-connect-provider.md) records the checks and the providers it was tested
with.

## Options

Read the endpoints from the provider's discovery document once, when the application starts, and use the options like any
other:

```csharp
var gitlab = await OidcOAuth2Options.FromDiscoveryAsync("https://gitlab.com");
gitlab.ProviderName = "GitLab";
gitlab.ClientId = "your-client-id";
gitlab.ClientSecret = "your-client-secret";
gitlab.RedirectUri = "https://localhost:7032/auth/callback";
builder.Services.AddOAuth2Client("GitLab", gitlab);
```

- `ProviderName` is required. Users are identified by it together with `UserId`, and two providers can give the same user ID
  to different people, so give each provider a name of its own and keep it. The name of a provider with options of its
  own, such as `Google` or `GitHub`, is refused in any case, and the web packages refuse a second client with the same name
  and another issuer.
- The `issuer` of the discovery document must equal the authority you pass, apart from a trailing slash. The request is not
  cached, so do not call it for each sign-in, and pass only an authority the application trusts.
- The endpoints and `Issuer` can also be set by hand. A provider without a user information endpoint leaves
  `UserInfoEndpoint` empty: the user then comes from the ID token of the sign-in, which needs `Issuer` and the `openid` scope.
- The default scopes are `openid profile email`, and the user name is the `name` claim, the given and family names, then
  `preferred_username` and then `nickname`.
- The options shared by every provider are described in [Options](../guides/options.md).

## Desktop and console applications

`LoopbackOAuth2Client` signs in as a public client, without the client secret, so the provider must accept a public client
with PKCE. Google refuses a desktop client that sends no client secret; use [`GoogleOAuth2Options`](google.md) for Google.

## .NET MAUI applications

`AppOAuth2Client` signs in with a custom scheme that the provider has registered for the client. See
[.NET MAUI applications](../guides/maui.md).

## Web applications

Register the callback URL with the provider, and follow the guide for [ASP.NET Core](../guides/aspnetcore.md) or
[ASP.NET (System.Web)](../guides/aspnet-system-web.md).

An application that signs in with several providers trusts each of them with its sign-ins. A provider that is malicious or
compromised can have the user sign in at another of the application's providers and receive that sign-in's code at its own
token endpoint (a mix-up attack, RFC 9700, section 4.4). The library does not check which provider a callback came from.
What keeps such a code useless is that the other provider requires the client secret of the web application to redeem it,
and the PKCE code verifier of the sign-in it was issued for, so keep a client secret on every web client whose provider
allows one, keep `UsePkce` on, as it is by default, and add only providers you trust.

## Tokens and users

`UserId` is the `sub` claim. The library checks the ID token of each sign-in: its audience, and its issuer when `Issuer`
is set. See [Identifying users](../guides/identifying-users.md).

## Apps with a separate back end

`GetUserInfoAsync` throws `NotSupportedException`: the front end sends its ID token, which the back end validates. See
[Apps with a separate back end](../guides/separate-back-end.md).
