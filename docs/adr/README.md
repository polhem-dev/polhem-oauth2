# Architecture decision records

**English** | [繁體中文](README.zh-TW.md)

Each record explains a design decision in Polhem.OAuth2 and the reasons behind it.

| Record | Decision |
|--------|----------|
| [ADR-001](adr-001-drop-bee-base.md) | Drop the Bee.Base dependency and carry the state encryption in the package (the encryption is superseded by ADR-005) |
| [ADR-002](adr-002-language-policy.md) | English for everything maintained together; bilingual public documents |
| [ADR-003](adr-003-exception-semantics.md) | Only expected OAuth2 failures become failed results |
| [ADR-004](adr-004-system-browser-loopback.md) | Sign in from desktop applications through the system browser and a loopback redirect |
| [ADR-005](adr-005-web-sign-in-cookie.md) | Keep each web sign-in in its own protected cookie |
| [ADR-006](adr-006-app-sign-in.md) | Sign in from mobile applications directly or through the application's own back end |
| [ADR-007](adr-007-access-token-from-front-end.md) | Return the user of an access token from a front end only after the provider names its client |
| [ADR-008](adr-008-generic-openid-connect-provider.md) | Sign in with any OpenID Connect provider through one options type |
| [ADR-009](adr-009-sign-in-with-apple.md) | Sign in with Apple through the web flow, with a client secret the library signs |
| [ADR-010](adr-010-breaking-changes-in-1-x-minors.md) | Allow breaking changes in 1.x minor versions, and start strict semantic versioning at 2.0 |
