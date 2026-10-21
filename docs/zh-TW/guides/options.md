# 所有 provider 共用的 Options

[English](../../en/guides/options.md) | **繁體中文**

每個 provider 各有自己的 options 型別，其他 OpenID Connect provider 使用 `OidcOAuth2Options`：

| Options 型別 | 帳號 |
|--------------|------|
| `GoogleOAuth2Options` | [Google](../providers/google.md) |
| `FacebookOAuth2Options` | [Facebook](../providers/facebook.md) |
| `LineOAuth2Options` | [LINE](../providers/line.md) |
| `AzureOAuth2Options` | [Microsoft Entra ID](../providers/microsoft-entra-id.md) |
| `Auth0OAuth2Options` | [Auth0](../providers/auth0.md) |
| `OktaOAuth2Options` | [Okta](../providers/okta.md) |
| `GitHubOAuth2Options` | [GitHub](../providers/github.md) |
| `AppleOAuth2Options` | [Sign in with Apple](../providers/apple.md) |
| `OidcOAuth2Options` | [其他任何 OpenID Connect provider](../providers/openid-connect.md) |

只有單一 provider 才有的設定，例如 Auth0 與 Okta 的 `Domain`，寫在該 provider 的文件裡。

- `ClientId` 與 `RedirectUri` 為必填。client 建立時會複製並檢查 options，之後再修改原物件不會有影響；options 不合法時當場擲出 `ArgumentException`。
- 所有端點都必須是絕對的 `https` URI，而且不能有 fragment。端點自帶的 query 會被保留，所以可以在端點上加 provider 的參數，
  例如 `https://tenant.auth0.com/authorize?audience=...`。
- `UsePkce` 預設為 `true`。
- `AcceptedClientIds` 列出後端除了 `ClientId` 之外，還接受哪些前端 client ID 的 access token；見
  [前後端分離的應用程式](separate-back-end.md)。
- `ClientAuthentication` 決定 client secret 怎麼送到 token 端點：放在請求本文（`ClientSecretPost`，預設值），或放在 HTTP Basic
  標頭（`ClientSecretBasic`）。provider 把應用程式登記為 `client_secret_basic`、且不接受本文裡的 secret 時，請設為
  `ClientSecretBasic`。[Okta](../providers/okta.md) 的網頁應用程式預設就是這樣登記。
