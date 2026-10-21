# ADR-010: Breaking changes are allowed in 1.x minor versions; strict semantic versioning starts at 2.0

**English** | [繁體中文](adr-010-breaking-changes-in-1-x-minors.zh-TW.md)

## Status

Accepted (2026-10-21). Replaces the sentence of `CHANGELOG.md` that said "from 1.4.0 on, a breaking change raises the
major version". The Polhem framework follows the same policy, in its
[ADR-052](https://github.com/polhem-dev/polhem/blob/main/maintainers/adr/adr-052-breaking-changes-in-1-x-minors.md).

## Context

1.0.0 was released on 2026-09-14, and `CHANGELOG.md` declared semantic versioning. Two of the three minor versions that
followed contain breaking changes:

| Version | Date | Breaking changes |
|---------|------|------------------|
| 1.2.0 | 2026-09-25 | The relay protects its cache entries; it refuses an unlisted redirect URI and a long code lifetime; a Facebook token endpoint error becomes an `OAuth2Exception` |
| 1.3.0 | 2026-09-26 | A token type other than `Bearer` fails the sign-in; the shared `HttpClient` no longer follows redirects |

They were identified as breaking only when the changelog was rewritten, and the changelog then promised that from 1.4.0
on a breaking change would raise the major version. 1.4.0 breaks that promise in its turn: Google, Auth0, Okta and
Microsoft Entra ID now check the ID token of a sign-in, and a sign-in whose ID token is issued to another client, by
another issuer or for another subject than the user information fails where it used to succeed.

Every one of these breaks is a change of behavior, not of the API. `PublicAPI.Shipped.txt` and the public API analyzer
catch a removed or changed member, but nothing catches a call that starts to fail on a response it used to accept. The
"follows Semantic Versioning" sentence of the changelog was therefore a guarantee with no mechanism behind it.

Most of these breaks make the library refuse what a specification or a provider's own rules forbid, such as a token type
it does not understand or an ID token for another client. Deferring each of them to a major version would leave a known
weakness in place for as long as 1.x lasts, or spend a major version on a change that leaves nothing to migrate.

The cost of breaking is low at this stage: no consumer outside the polhem-dev organization is known to depend on the
packages.

## Decision

### 1. A 1.x minor version may contain breaking changes

During 1.x, a **minor** version may change behavior so that a call that used to succeed fails or returns other data, and
may remove, rename or change public API. A **patch** version may not: it fixes defects, and it neither removes nor
changes anything in `PublicAPI.Shipped.txt`.

### 2. Every breaking change is listed, whatever its reason

A breaking change is marked **Breaking:** in `CHANGELOG.md` in one sentence that links to the version's release notes
under `docs/en/changelogs/`, which say whom it affects, the behavior before and now, and how to migrate. A change is
breaking when an application that worked with the previous version can fail with this one, even when the change only
enforces a specification and leaves nothing to migrate. That is how the token type check of 1.3.0 was listed, and the ID
token check of 1.4.0 is listed the same way.

### 3. Consumers pin to a minor version

`CHANGELOG.md` and `README.md` (both languages) state this policy and recommend a package reference with a range that
stays within one minor version, for example `[1.4,1.5)`, rather than a floating `1.*`.

### 4. From 2.0, semantic versioning is enforced by package validation

2.0.0 starts strict semantic versioning, and the same release turns on the .NET SDK's package validation for every
packable project: `EnablePackageValidation`, with `PackageValidationBaselineVersion` set to the previous release, so that
`dotnet pack` fails on an API or binary incompatibility. Package validation does not see a change of behavior, so from
2.0 such a change waits for a major version, and its review is the one that judges it.

### 5. When 2.0 is released

2.0.0 is released at the first of:

- a consumer outside the polhem-dev organization is known to depend on the packages, or
- two consecutive minor versions ship with no **Breaking:** entry.

The maintainer may release 2.0 earlier; the conditions make sure that it is not deferred indefinitely.

## Consequences

- **A 1.x version number says nothing about compatibility.** Consumers learn it from the changelog, and the pinning
  recommendation is what protects them.
- **The library can refuse what it should refuse, as soon as it finds it.** A check such as the one of the ID token ships
  in the next minor version instead of waiting for 2.0.
- **The release notes are the mechanism.** Until 2.0 no tool finds a behavioral break, so the review of each pull
  request asks whether a change can make a working application fail, and if it can, the change is listed.
- **1.2.0 and 1.3.0 are ordinary 1.x versions in hindsight.** Their breaking changes are listed as they are.

## Alternatives considered

### Raise the major version for every breaking change

Strictly correct under semantic versioning, and what the changelog promised. Rejected: 1.4.0 would be 2.0.0, and at the
pace of 1.2.0 and 1.3.0 a major version would follow every few days. That tells a consumer no more than this policy does,
and it spends 2.0 as the milestone that marks a stable surface.

### Treat a change that only enforces a specification as not breaking

Keep semantic versioning, and leave out of the breaking changes those that only refuse what a specification forbids.
Rejected: whether a provider breaks a specification is not something an application can see in advance, so to a consumer
such a change is as breaking as any other. It also contradicts 1.3.0, which listed the token type check, and the claim
"only what the specification forbids" has to be right about every clause of the specification to hold.

## Implementation

- `CHANGELOG.md` and `CHANGELOG.zh-TW.md`: replace the semantic versioning sentence with the 1.x policy and the pinning
  recommendation.
- `README.md` and `README.zh-TW.md`: add the same short statement where installation is described.
- `docs/en/changelogs/1.4.0.md` and its translation: list the ID token check under "Breaking changes", and mark it
  **Breaking:** in the changelog.
- `docs/adr/README.md` and its translation: add this record to the index.
