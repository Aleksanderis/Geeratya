# Contributing to Geeratya

## Reporting issues

- Use the issue templates (bug report, feature request).
- Check the [user guide](docs/user-guide.md) and the Features and "Not included" lists in the [README](README.md) first.
- One problem per issue. For a bug, include `geeratya --version`, your OS and terminal, and the steps to reproduce.
- Scrub pasted output: no API token, no company site name, no emails, no issue text you may not share.
- Report security problems privately: see [SECURITY.md](SECURITY.md).

## Pull requests

Open an issue first and wait for a reply that the change fits. Unsolicited large PRs or ones adding features outside the README lists may be closed.

- Branch from `main` as `feature/<issue>-<slug>` or `fix/<issue>-<slug>`, one issue per PR, reference it (`Closes #<n>`).
- Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`).
- `dotnet build` and `dotnet test` must pass. Warnings are errors, and formatting is enforced by `.editorconfig` during the build.
- Add tests for behaviour you change or add. Tests run offline against a fake Jira (recorded, scrubbed JSON fixtures); see [docs/internal/testing.md](docs/internal/testing.md).
- Keep Linux, Windows and macOS working and the binary Native AOT compatible (no reflection or runtime codegen). Run the AOT publish after adding a dependency.
- Keep the PR focused and say what changed and why. The maintainer merges.

## Development setup

```
dotnet build
dotnet test
dotnet publish src/Geeratya.Tui -c Release -o publish   # Native AOT; fails on any IL* warning
publish/geeratya
```

The .NET SDK version is pinned in `global.json`. The AOT publish needs the Native AOT prerequisites:

- Linux (Debian/Ubuntu): `clang` and `zlib1g-dev`. Arch: `sudo pacman -S --needed clang zlib dotnet-sdk`.
- Windows: "Desktop development with C++".
- macOS: Xcode command line tools.

 Optional quality inspections: `scripts/inspect.sh --changed` (Windows: `scripts/inspect.ps1`).

To try the app without a Jira site, run the fake server in `tools/FakeJira` with a profile that points at its loopback address and `GEERATYA_TOKEN=fake`; `scripts/screenshot.sh` shows the setup (see "Fake Jira" in [docs/internal/testing.md](docs/internal/testing.md)).

## Code overview

- `src/Geeratya.Core`: pure state, commands, keymap, settings. Runs without a terminal.
- `src/Geeratya.Jira`: Jira Cloud REST calls, typed results, no UI.
- `src/Geeratya.App`: effects that connect Core to Jira and the platform, without a terminal.
- `src/Geeratya.Platform`: the only place for OS-specific code (paths, secret stores, clipboard).
- `src/Geeratya.Tui`: Terminal.Gui rendering and the app entry point.
- Details: [architecture](docs/internal/architecture.md), [testing](docs/internal/testing.md), [Jira layer](docs/internal/jira-layer.md). The design notes are for maintainers and are not required reading.

## Live tests and credentials

Never run tests or tools against a Jira you do not own. Live tests are opt-in and refuse hosts outside `GEERATYA_LIVE_HOSTS`. API tokens are secrets: never log, commit or paste them; fixtures are scrubbed by `scripts/scrub-fixture.py`.

## Agent-assisted development

Geeratya is developed with AI coding agents under the maintainer's direction. [CLAUDE.md](CLAUDE.md) and `.claude/` hold their instructions. Every change is reviewed by an independent reviewer agent and the maintainer reads it afterwards. You do not need the agent setup to contribute.

## License

By contributing you agree that your work is licensed under the [MIT License](LICENSE).
