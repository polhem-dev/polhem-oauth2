# ADR-002: English for everything maintained together, bilingual public documents

**English** | [繁體中文](adr-002-language-policy.zh-TW.md)

## Status

Accepted (2026-09-13)

## Context

Polhem projects are meant to be maintained by more than one person. Bee.OAuth2 was written by a single maintainer, with
XML documentation, comments and commit messages in Chinese that a wider group of contributors cannot read. Its users,
on the other hand, include readers who rely on documentation in Traditional Chinese.

## Decision

- Everything maintained together is written in English: source code, XML documentation (which also ships to IntelliSense),
  comments, test method names and `[DisplayName]` text, commit messages, and the agent guidance under `.claude/`.
- Public Markdown documents are bilingual. `name.md` is English and `name.zh-TW.md` is Traditional Chinese, and each
  links to the other at the top.
- ADRs follow the same rule as public documents, so that every contributor can read why the code is the way it is.

## Consequences

- A change to a public document or an ADR updates both language versions in the same commit. No automated check
  enforces this yet.
- `.claude/CLAUDE.md` states the policy explicitly, because a personal assistant setting may default to another language.
