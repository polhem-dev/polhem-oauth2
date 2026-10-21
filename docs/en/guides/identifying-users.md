# Identifying users

**English** | [繁體中文](../../zh-TW/guides/identifying-users.md)

- Identify a user by the provider name together with `UserInfo.UserId`, not by `Email`: an address can change, and
  providers differ in whether they verify it.
- An OpenID Connect provider (Google, Auth0, Okta, Microsoft Entra ID, Sign in with Apple and `OidcOAuth2Options`) leaves
  `Email` null when its `email_verified` claim is false. A provider that does not send the claim, such as Microsoft Entra ID,
  returns the address unmarked, so do not link a sign-in to an existing account by its address alone.
- The provider name is `AuthorizationResult.ProviderName`: `Google`, `Facebook`, `LINE`, `Azure` (Microsoft Entra ID),
  `Auth0`, `Okta`, `GitHub`, `Apple`, or the `ProviderName` of `OidcOAuth2Options`. It does not depend on the name a client is
  registered under.
- What `UserId` is, and where the email address comes from, differs by provider; each provider's document says so under
  "Tokens and users". For example, the `UserId` of Microsoft Entra ID and of Facebook is different for each application the
  user signs in to.
- The library reads the ID token of its own sign-in, which comes straight from the token endpoint. Sign in with Apple, and
  `OidcOAuth2Options` without a user information endpoint, take the user from it, and LINE the email address. For Google,
  Auth0, Okta, Microsoft Entra ID and `OidcOAuth2Options` with a user information endpoint, the user information must name the
  same subject as the ID token, when the response has one.
- Those checks require the ID token to be issued to the client. The issuer is checked for Google, Auth0, Sign in with Apple, and
  `OidcOAuth2Options` when `Issuer` is set; not for Okta and Microsoft Entra ID, whose issuer the options do not fix, nor for
  LINE. The signature is not checked, which a token from the token endpoint over TLS does not need.
  [ADR-008](../../adr/adr-008-generic-openid-connect-provider.md) records the checks.
- `Token.IdToken` is otherwise returned as the provider sent it; validate it before relying on its claims anywhere else, such
  as in another back end.
