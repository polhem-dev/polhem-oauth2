# Contributing to Polhem.OAuth2

**English** | [繁體中文](CONTRIBUTING.zh-TW.md)

This guide describes how changes reach the repository.

## Workflow

1. Fork the repository and create a branch from the latest `main` in your fork.
2. Make the change, with tests. A change to a public document or an ADR updates both language versions
   ([ADR-002](docs/adr/adr-002-language-policy.md)).
3. Build and test locally, with the commands under "Build and test" in [`.claude/CLAUDE.md`](.claude/CLAUDE.md).
4. Open a pull request against `main`. The Build CI workflow runs on it.

For a larger change (a new feature, a change to public API, a new dependency), open an issue first so the approach can
be agreed on before you spend time on it.

## Who merges

The repository has one maintainer, [@jeff377](https://github.com/jeff377). Other contributors are not given write
access: they work from a fork, and the maintainer reviews every pull request from a fork before merging it. The
maintainer commits their own changes to `main` directly.
