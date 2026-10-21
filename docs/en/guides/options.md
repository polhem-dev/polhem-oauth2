# Options shared by every provider

**English** | [繁體中文](../../zh-TW/guides/options.md)

Each provider has its own options type, and any other OpenID Connect provider uses `OidcOAuth2Options`:

| Options type | Account |
|--------------|---------|
| `GoogleOAuth2Options` | [Google](../providers/google.md) |
| `FacebookOAuth2Options` | [Facebook](../providers/facebook.md) |
| `LineOAuth2Options` | [LINE](../providers/line.md) |
| `AzureOAuth2Options` | [Microsoft Entra ID](../providers/microsoft-entra-id.md) |
| `Auth0OAuth2Options` | [Auth0](../providers/auth0.md) |
| `OktaOAuth2Options` | [Okta](../providers/okta.md) |
| `GitHubOAuth2Options` | [GitHub](../providers/github.md) |
| `AppleOAuth2Options` | [Sign in with Apple](../providers/apple.md) |
| `OidcOAuth2Options` | [Any other OpenID Connect provider](../providers/openid-connect.md) |

The settings that only one provider has, such as `Domain` of Auth0 and Okta, are described in that provider's document.

- `ClientId` and `RedirectUri` are required. A client copies and checks the options when it is created, so later changes
  to them have no effect, and invalid options throw `ArgumentException` right away.
- Every endpoint must be an absolute `https` URI without a fragment. A query is kept, so an endpoint can carry a parameter of the
  provider, such as `https://tenant.auth0.com/authorize?audience=...`.
- `UsePkce` is `true` by default.
- `AcceptedClientIds` lists the client IDs of front ends whose access tokens a back end accepts, besides `ClientId`; see
  [Apps with a separate back end](separate-back-end.md).
- `ClientAuthentication` chooses how the client secret reaches the token endpoint: in the request body (`ClientSecretPost`, the
  default) or in an HTTP Basic header (`ClientSecretBasic`). Set `ClientSecretBasic` for an application that the provider
  registered for `client_secret_basic` and that refuses the secret in the body. [Okta](../providers/okta.md) registers a web
  application that way unless told otherwise.
