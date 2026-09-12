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
- `Polhem.OAuth2.Desktop` and `Polhem.OAuth2.WinForms` opt out of the code-style gate in their project files, because
  they are removed when the WebView2 sign-in flow is replaced.

## Decisions

Design decisions and their reasons are recorded in `docs/adr/`. Read the relevant ADR before changing behavior it describes.

## Build and test

```bash
dotnet build Polhem.OAuth2.slnx -c Release -p:EnableWindowsTargeting=true
dotnet test tests/Polhem.OAuth2.UnitTests/Polhem.OAuth2.UnitTests.csproj -c Release
```

`EnableWindowsTargeting` is only needed on macOS and Linux, where it lets the `net8.0-windows` project build.
