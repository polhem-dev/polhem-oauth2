# ADR-008: Sign in with any OpenID Connect provider through one options type

**English** | [繁體中文](adr-008-generic-openid-connect-provider.zh-TW.md)

## Status

Accepted (2026-10-20). Implemented on 2026-10-20 in pull requests [#23](https://github.com/polhem-dev/polhem-oauth2/pull/23)
and [#24](https://github.com/polhem-dev/polhem-oauth2/pull/24), and tested with Keycloak, GitLab.com, Google and Okta on
2026-10-20, as "Test results" records. The dedicated OpenID Connect providers took up the ID token check on 2026-10-20 in
[#26](https://github.com/polhem-dev/polhem-oauth2/pull/26), tested with Google, Auth0, Okta and Microsoft Entra ID.

## Context

Keycloak, GitLab, AWS Cognito, Authentik, Zitadel and many other identity providers follow OpenID Connect, but the library
could not sign in to them. `OAuth2Options` can only be derived from inside the package, and writing a provider for each of
them is not worth it, because they differ only in their endpoints. Auth0 and Okta already show that: their providers map the
same standard claims and differ only in how the endpoints are built from a domain.

OpenID Connect also lets a provider leave out the user information endpoint (OpenID Connect Core 1.0, section 5.3), and Apple
has none. The user is then only in the ID token of the token response.

## Decision

- `OidcOAuth2Options` signs in with any OpenID Connect provider. The endpoints are set by hand or read from the discovery
  document. The default scopes are `openid profile email`, and the provider maps `sub`, the name (`name`, then the given and
  family names, then `preferred_username`, then `nickname`) and `email`.
- `ProviderName` is required and has no default. A user is identified by the provider name together with the user ID, and two
  providers can issue the same `sub` to different people. A shared default name would merge those users the moment an
  application registered a second provider without naming it.
- **Options stay synchronous, and discovery is an asynchronous factory**: `OidcOAuth2Options.FromDiscoveryAsync(authority)`
  returns options that the application completes and passes to a client as usual. A client checks its options when it is
  created, so fetching the document on the first sign-in would have postponed that check, and added a request to the first sign-in
  of every client. The application calls the factory once when it starts and keeps the result; the library caches nothing, and
  offers no asynchronous registration method for dependency injection.
- Discovery checks the document before it returns options:
  - The `issuer` must equal the authority (OpenID Connect Discovery 1.0, section 4.3), compared ordinally, except that a single
    trailing slash on either side is ignored. Auth0 writes its issuer with a trailing slash and the other providers tested
    without one. `Issuer` keeps the value of the document, because ID tokens carry it exactly.
  - The endpoints pass the same check as every other options type: an absolute https URI without a fragment.
  - When the document lists `response_types_supported`, it must contain `code`, and when it lists
    `code_challenge_methods_supported`, it must contain `S256`. A document without these lists is accepted.
  - `ClientAuthentication` is `ClientSecretPost` when `token_endpoint_auth_methods_supported` lists `client_secret_post`, and
    `ClientSecretBasic` otherwise, the default of the specification.
  - The document is limited to 1 MiB, and `HttpClient.Timeout` covers its body as well as its headers.
  - Failures follow ADR-003: `HttpRequestException` for a failed request, an unsuccessful status or a document that is too large,
    `JsonException` for a body that is not a JSON object, and `OAuth2Exception` for a document that does not describe a usable
    provider.
- `UserInfoEndpoint` may be empty. The user is then the payload of the ID token, and `Issuer` and the `openid` scope are
  required.
- Every ID token from the token endpoint is checked before it is used: its audience must contain the client ID; an authorized
  party (`azp`) must be the client ID, and must be present when the token has other audiences; the issuer must equal `Issuer`
  when it is set; and the token needs a `sub`. With a user information endpoint, its `sub` must equal that of the ID token
  (OpenID Connect Core 1.0, section 5.3.2). A token that fails is an `OAuth2Exception`, so the sign-in fails.
- The signature and the expiry of the ID token are not checked. The token comes straight from the token endpoint over TLS,
  which item 6 of OpenID Connect Core 1.0, section 3.1.3.7, accepts in place of the signature. An encrypted ID token is not
  supported.
- The checks are shared code that LINE uses for the email address in its ID token, and that Apple can use. LINE keeps its
  behavior: it does not check the issuer, and an ID token that cannot be read leaves the email address out instead of failing
  the sign-in.
- The dedicated OpenID Connect providers, Google, Auth0, Okta and Microsoft Entra ID, run the same checks on the ID token of a
  sign-in and compare its `sub` with that of the user information. A token response without an ID token, when `openid` is not
  requested, is not checked, as before. The issuer is checked where it is known from the options alone:
  - Google: `https://accounts.google.com` or `accounts.google.com`, the two values that Google documents.
  - Auth0: `https://<Domain>/`. Auth0 names the domain the request went to, with a trailing slash, and every endpoint is built
    from `Domain`, so a custom domain is named too.
  - Okta: not checked. The issuer mode of an authorization server decides whether a token names the Okta domain or a custom
    domain, so the issuer can differ from `Domain`.
  - Microsoft Entra ID: not checked. The issuer names the tenant of the user, which `common`, `organizations` and `consumers`
    do not fix in advance.

  The audience is checked for all of them. The generic and the dedicated providers share one base class for the check and for
  the mapping of the standard claims; each provider adds only the field it falls back to for the user name.
- `OAuth2Client.GetUserInfoAsync(accessToken)` throws `NotSupportedException` for these options, as for Okta: an OpenID Connect
  front end proves who signed in with its ID token (ADR-007).

## Out of scope

- Validating an ID token that a front end sends. JwtBearer authentication of ASP.NET Core and
  `Microsoft.IdentityModel.Protocols.OpenIdConnect` do that, with signing keys that rotate, which this library should not
  reimplement.
- `nonce`, which PKCE replaces (RFC 9700, section 2.1.1), dynamic client registration, the implicit and hybrid flows, and the
  `iss` parameter of the authorization response (RFC 9207).
- Moving Auth0 and Okta onto the generic provider. Their options keep the convenience of `Domain` and `AuthorizationServerId`.

## Consequences

- Keycloak, GitLab and other OpenID Connect providers work with the web, loopback and application clients, as far as the
  provider accepts their redirect URIs.
- Google refuses the code exchange of a desktop client without its client secret, and a public client of these options sends
  none. Use `GoogleOAuth2Options`, which sends it.
- A provider whose discovery document names a template as its issuer, such as the `common` tenant of Microsoft Entra ID, cannot
  be read by discovery.
- A sign-in with Google, Auth0, Okta or Microsoft Entra ID that used to succeed now fails when the user information names
  another subject than the ID token, or when the ID token cannot be read or is for another client. The sign-ins in "Test
  results" all passed the check.
- `tests/Polhem.OAuth2.UnitTests/IdTokenTests.cs`, `OidcOAuth2ProviderTests.cs`, `OpenIdConnectProviderTests.cs` and
  `OidcDiscoveryTests.cs` cover the checks.
  The fake provider serves an OpenID Connect provider with a discovery document and no user information endpoint under `/oidc`,
  which the device tests sign in to.

## Test results

On 2026-10-20 the discovery documents named these issuers, which decided the trailing slash rule:

- Google, GitLab.com, Okta (a custom and the org authorization server) and Keycloak 26.4: no trailing slash.
- Auth0: a trailing slash.
- Microsoft Entra ID, `common` tenant: a template with `{tenantid}`.

On 2026-10-20 the options were read with `FromDiscoveryAsync` and signed in with real accounts:

- Keycloak 26.4 in Docker, with a certificate from a test authority: the web client with `client_secret_basic` and with
  `client_secret_post`, with and without the user information endpoint, and a refresh; a wrong secret failed with
  `unauthorized_client`. The loopback client signed in on a random port to a client that registered `http://127.0.0.1/callback`
  without a port; a registration with `localhost` did not match `127.0.0.1`. The application client signed in with a custom
  scheme redirect URI, and Keycloak refused a scheme that was not registered.
- GitLab.com, with an application that is not confidential and has the scopes `openid profile email`: the loopback client
  signed in on a random port to the redirect URI `http://127.0.0.1/callback` registered without a port, with PKCE and no
  secret, and refreshed. With and without the user information endpoint the user was the same. `sub` is the numeric account ID
  written as a string, and the ID token carries `name`, `email`, `preferred_username` and `nickname`. The user information has
  a `sub_legacy` field, which the library does not use.
- Google and Okta: the user of the generic options, with and without the user information endpoint, equalled that of
  `GoogleOAuth2Options` and `OktaOAuth2Options` in the user ID, the name and the email address. Google refused the desktop
  client as a public client of the generic options with `invalid_request`, and accepted it as a confidential client.

The device tests of [run 39354330719](https://github.com/polhem-dev/polhem-oauth2/actions/runs/39354330719) signed in to the
OpenID Connect mode of the fake provider directly on iOS, Mac Catalyst and Android, and through the loopback client on Windows.

On 2026-10-20 the dedicated providers signed in with real accounts through the loopback client, with the ID token check in
place. Every sign-in succeeded, the ID token was issued to the client as a single audience, and the `sub` of the user
information equalled that of the ID token:

- Google: the issuer was `https://accounts.google.com`, the ID token had an `azp` equal to the client ID, and `sub` is numeric.
- Auth0: the issuer was the domain with a trailing slash, and `sub` has the form `<provider>|<id>`.
- Okta, with the `default` authorization server: the issuer was `https://<domain>/oauth2/default`.
- Microsoft Entra ID, `common` tenant, with a personal account and a work account: the issuer named the tenant of each account,
  the consumers tenant for the personal account. The Microsoft Graph user information returned the same pairwise `sub` as the
  ID token in both cases; it names the parts of a personal account's name `givenname` and `familyname`, and those of a work
  account `given_name` and `family_name` with a `name`.
