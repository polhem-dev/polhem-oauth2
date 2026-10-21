# Results, tokens and errors

**English** | [繁體中文](../../zh-TW/guides/results-and-errors.md)

- A successful result has `ProviderName`, `UserInfo` and `Token`; a failed result has `Exception`.
- `Token` holds the access token, and the refresh token, ID token, lifetime and scopes when the provider returns them.
  `RefreshTokenAsync` on a client obtains new tokens. Keep the refresh token of the latest response, because some providers
  issue a new one each time.
- Whether a provider issues a refresh token depends on the provider and on the request; each provider's document says when,
  under "Tokens and users": [Google](../providers/google.md#tokens-and-users),
  [Facebook](../providers/facebook.md#tokens-and-users), [LINE](../providers/line.md#tokens-and-users),
  [Microsoft Entra ID](../providers/microsoft-entra-id.md#tokens-and-users), [Auth0](../providers/auth0.md#tokens-and-users),
  [Okta](../providers/okta.md#tokens-and-users), [GitHub](../providers/github.md#tokens-and-users).
- `Exception` holds only the failures a sign-in is expected to produce, such as a failed HTTP request, a state that does
  not match, or an error from the provider. Configuration and programming errors, such as an unregistered client name, are
  thrown. See [ADR-003](../../adr/adr-003-exception-semantics.md).
- An error from the provider is an `OAuth2Exception`: `Error` holds the error code, such as `access_denied`, and
  `ErrorDescription` the provider's text. Anyone who sends the user a link can set the values of an error in a redirect,
  so encode them before showing them. Some providers report errors of their token endpoint in a form of their own, which
  becomes an `OAuth2Exception` all the same; [Facebook](../providers/facebook.md#errors) and
  [GitHub](../providers/github.md#errors) describe theirs.
