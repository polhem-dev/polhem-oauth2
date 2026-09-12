# ADR-001: Drop Bee.Base and carry the state encryption in the package

**English** | [繁體中文](adr-001-drop-bee-base.zh-TW.md)

## Status

Accepted (2026-09-13)

## Context

Bee.OAuth2, the predecessor of this package, depended on Bee.Base 3.4.0 for two things: string helpers (`StrFunc`) and
the AES-CBC-HMAC primitives behind `OAuth2StateCryptor`. Bee.Base also brought in Newtonsoft.Json, which the providers
used without referencing it themselves.

Keeping that dependency would list a Bee package among the dependencies of every Polhem.OAuth2 package and tie them to a
release line that is no longer developed. A shared base package from the Polhem framework would not help either: the
framework targets current .NET only, while Polhem.OAuth2 keeps supporting netstandard2.0 and .NET Framework 4.8.

## Decision

- Replace `StrFunc.IsEmpty` and `StrFunc.IsNotEmpty` with `string.IsNullOrWhiteSpace`. The semantics are the same,
  because `StrFunc` trims the string before comparing it with an empty string.
- Copy the AES-CBC-HMAC implementation into the package as the internal types `AesCbcHmacCryptor` and
  `AesCbcHmacKeyGenerator`, keeping the byte layout of Bee.Base 3.4.0.
- Reference Newtonsoft.Json directly until JSON parsing moves to System.Text.Json.

### Why the byte layout stays compatible

A state value is produced before the redirect to the provider and read again in the callback. An application that
upgrades between those two moments, or runs old and new instances side by side, must be able to decrypt states that the
old code produced with the same `OAUTH2_STATE_KEY`. Keeping the layout also keeps existing keys valid.

## Consequences

- `tests/Polhem.OAuth2.UnitTests/AesCbcHmacCryptorTests.cs` contains ciphertexts produced once with Bee.Base 3.4.0.
  Those tests fail if the byte layout changes.
- The encryption types are internal, so applications create `OAUTH2_STATE_KEY` from any source of 64 random bytes
  instead of calling a library method.
- Fixes to the encryption code are now the responsibility of this repository.
