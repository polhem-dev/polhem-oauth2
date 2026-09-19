# Polhem.OAuth2 — guidance for coding agents

## Language

- Everything maintained together is written in **English**: source code, XML documentation, comments, test method
  names and `[DisplayName]` text, commit messages, and the files under `.claude/`.
- This overrides any personal or user-level setting that asks for another language for prose, including an
  instruction to write in Traditional Chinese. Replies in a conversation may still follow the user's language.
- Public Markdown documents and ADRs are bilingual: `name.md` is English and `name.zh-TW.md` is Traditional Chinese,
  each with a language switch at the top. Change both files in the same commit.

The reasons are recorded in `docs/adr/adr-002-language-policy.md`.

## Code style

- The rules live in `.editorconfig`, `src/Directory.Build.props` and `tests/Directory.Build.props`, and
  `TreatWarningsAsErrors` turns every violation into a build error. Read those files instead of restating rules here.

## Decisions

Design decisions and their reasons are recorded in `docs/adr/`. Read the relevant ADR before changing behavior it describes.

## Local working documents

- `local/` at the repository root is ignored by git. Keep documents there that are not meant for every maintainer or for
  publication: plans, drafts, personal notes, and review findings that list unfixed security issues.
- Plans go in `local/plans/`. Never commit anything under `local/`, never add it with `git add -f`, and never link to it
  from committed files.
- Decisions of lasting value belong in `docs/adr/`; work other maintainers need to see belongs in GitHub issues or pull requests.
- The language rule above does not apply to `local/`, because its documents are not maintained together.
- A session in a git worktree cannot see `local/`. Hand off work that depends on it to a session in the main working tree.

## Build and test

```bash
dotnet build Polhem.OAuth2.slnx -c Release -p:EnableWindowsTargeting=true
dotnet test tests/Polhem.OAuth2.UnitTests/Polhem.OAuth2.UnitTests.csproj -c Release
```

`EnableWindowsTargeting` is only needed on macOS and Linux, where it lets the `net10.0-windows` sample build.

The .NET MAUI projects, `samples/OAuthMaui` and `tests/Polhem.OAuth2.DeviceTests`, are not in the solution and need the
MAUI workload. The device tests run with `dotnet test <project> -c Release -f <platform target framework>` on a booted
simulator, emulator or the host; `.github/workflows/device-tests.yml` shows the arguments for each platform. That workflow
only runs when started by hand, so run it after changing code that behaves differently per platform.

The end-to-end device tests sign in to `tests/Polhem.OAuth2.FakeProvider` and are skipped unless the build gets
`-p:FakeProviderUrl=https://localhost:7443`. The fake provider creates a CA for each run, which the device must trust:
the steps named "Start the fake provider" and "Trust the fake provider" in `device-tests.yml` show how, and
`tests/Polhem.OAuth2.DeviceTests/scripts/prepare-android-emulator.sh` prepares an emulator. Use test simulators and
emulators only.
