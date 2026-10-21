# Apps with a separate back end

**English** | [繁體中文](../../zh-TW/guides/separate-back-end.md)

For brevity, the desktop example signs in and reads the user information in one call, which suits an application that
uses the result itself. When the front end signs in and then signs in to a separate back end, do not send the user
information from the front end to the back end as proof of identity: the back end cannot tell whether it was forged.
Sending a token is not enough either: another application that the same user signed in to holds a valid token for that user,
and the user information endpoint answers it with the same user. In order of preference:

1. Let the back end run the sign-in, so that no token leaves it: [ASP.NET Core](aspnetcore.md) for a web front end, and
   [Signing in through the back end](maui.md#signing-in-through-the-back-end) for a .NET MAUI application.
2. Send the ID token of an OpenID Connect sign-in, and validate it in the back end, for example with JwtBearer
   authentication: its signature with the provider's published keys, its issuer and expiry, and an audience that is the
   client ID of the front end. A validator that skips the audience can accept the tokens of other applications. The library
   does not validate an ID token that a front end sends.
3. Send the access token, and have the back end call `GetUserInfoAsync`: for Facebook and GitHub, which issue no standard ID
   token. Google and LINE are supported as well, but their ID token is the better choice.

```csharp
OAuth2Client client = oauth2Manager.GetClient("Facebook")!;
var result = await client.GetUserInfoAsync(accessToken, cancellationToken);
if (!result.IsSuccess)
    return Unauthorized();
// result.UserInfo is the user; issue the back end's own session.
```

- Before it asks for the user information, `GetUserInfoAsync` checks with the provider that the token was issued to
  `ClientId` or one of `AcceptedClientIds`, such as the iOS and Android clients of a Google Cloud project. The check cannot be
  turned off.
- It supports Google, LINE, Facebook and GitHub. Facebook and GitHub check the token with the client secret, so their options
  need `ClientSecret`. They also check only tokens issued to `ClientId`, so the front end that obtains the token, such as a
  desktop or mobile application, uses the same Facebook or GitHub application as the back end.
- Microsoft Entra ID, Auth0, Okta, Sign in with Apple and `OidcOAuth2Options` throw `NotSupportedException`: they are OpenID
  Connect providers, so use the ID token instead.
- A token that the provider refuses, has expired, or was issued to another client is a failed result, as are network failures
  and timeouts. A missing client secret, and client credentials that GitHub refuses, are thrown.
- LINE and Facebook take the token only in the query of the check, so an HTTP client that logs request URIs writes it to the
  log. Every check is a request to the provider, which limits how many it answers, so limit how often the back end's endpoint
  can be called.

[ADR-007](../../adr/adr-007-access-token-from-front-end.md) records the reasons and how each provider is checked.
