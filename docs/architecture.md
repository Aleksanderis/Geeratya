# Architecture

Solution layout, layering, composition, threading and cross-platform rules. Code style is in `.claude/rules/csharp-style.md`; the Jira contract is in `docs/jira-layer.md`.

## Projects

`Geeratya.slnx` holds four source projects and one test project per source project. Shared settings live in `Directory.Build.props` (net10.0, nullable, warnings as errors, `EnforceCodeStyleInBuild`, trim and AOT analyzers), versions in `Directory.Packages.props` (central package management), the SDK pin in `global.json`, formatting in `.editorconfig`. The product version is `Version` in `Directory.Build.props`; `ApplicationInfo.Version` reads it from the assembly.

| Project | Role | References |
|---|---|---|
| `src/Geeratya.Jira` | Jira Cloud REST client: connection, transport, typed models and errors. Knows nothing of Core or the UI. | none |
| `src/Geeratya.Core` | State layer: views, commands, reducer and effects, caches. Pure, runs without a terminal. | Jira |
| `src/Geeratya.Platform` | Operating-system abstraction: config/state/cache directories, console capabilities, environment lookup. | none |
| `src/Geeratya.Tui` | The `geeratya` executable: composition root, Terminal.Gui rendering, input. The only project that touches the console. | Core, Jira, Platform |
| `tools/FakeJira` | Local fake Jira server over the recorded fixtures (Native AOT compatible); tests in `tests/Geeratya.FakeJira.Tests`. See `docs/testing.md`. | none |
| `tests/Geeratya.<Name>.Tests` | xUnit, one per source project, same folder layout as the code under test. | its source project |

Layering is `Jira -> Core -> Tui`, with `Platform` beside them: Jira never references Core, Core never references Tui, and only Tui touches Terminal.Gui. Core takes platform facts (paths, capabilities) as plain values from the composition root rather than referencing Platform, so the state layer stays testable with literals.

## View model

A view is a source plus a layout (`docs/decisions.md`, 2026-11-01). `ViewDefinition(ViewSourceKind Source, string SourceValue, ViewLayout Layout)` in `Core/Views`: sources are JQL, saved filter, board and backlog; layouts are list and columns. Issue details and every write work the same in every view. A new board kind is a new source or layout, not new loading code. All view state belongs to a tab; nothing is global.

## Composition root

Dependencies are wired by hand in `Program.cs` (through `ApplicationRunner`) and `JiraServices.Create` once the Jira layer exists. No DI container: it needs reflection or code generation and costs startup. Tests build the same records or swap one part with `with` and a fake. `Program.cs` stays a few lines; the logic sits in `ApplicationRunner` so tests drive it without a process. Interfaces exist only where another layer consumes a class and tests need a fake.

## Threading

- The UI thread renders and handles input. It never waits on the network or the file system for long.
- Every Jira call is async, cancellable, has a timeout and honours rate limits; requests run on background tasks and report back as commands into the state layer, which the UI thread applies.
- The first frame is drawn before any network reply (startup target well under 100 ms).

## Cross-platform

Linux is built and tested first, then Windows, macOS last. Rules:

- Platform-specific code lives only in `Geeratya.Platform`; each such feature has a branch for every supported OS or an explicit unsupported path.
- Directories: Linux and macOS follow XDG (`XDG_CONFIG_HOME`, `XDG_STATE_HOME`, `XDG_CACHE_HOME`, falling back to `~/.config`, `~/.local/state`, `~/.cache`; a relative XDG value is ignored; with no absolute XDG value and no absolute home, e.g. `HOME` unset, `ApplicationDirectories` throws `InvalidOperationException` naming `HOME` rather than yield relative paths), each with a `geeratya` folder. Windows uses `%APPDATA%\Geeratya` for configuration and `%LOCALAPPDATA%\Geeratya\State` and `\Cache` for the rest. `ApplicationDirectories` computes them from an `OperatingSystemKind`, an `EnvironmentReader` and `PlatformFolders`, so tests cover all three systems on any host.
- Console capabilities (`ConsoleCapabilities.Detect`): colour tier from `NO_COLOR`, `TERM=dumb` and `COLORTERM`; redirected input or output means non-interactive.
- Environment access goes through `EnvironmentReader` with an injected lookup; no direct `Environment.GetEnvironmentVariable` elsewhere.
- Secrets (`Platform/Secrets`): `ISecretStore` per system, picked by `SecretStores.Create`. Linux runs `secret-tool` (libsecret) with the token on standard input, Windows calls Credential Manager (advapi32 `LibraryImport`), macOS uses `UnsupportedSecretStore`; on Linux without `secret-tool`, `LinuxSecretStore` flips `IsAvailable` to false at first use; `TokenProvider` then falls back to `GEERATYA_TOKEN`. Profiles (site, email, no token) are `profiles.json` in the config directory, owned by `Core/Profiles`; the token is looked up by `Profile.CredentialKey`; `GEERATYA_TOKEN` is one value used for whichever profile is active. Core never sees a token or the store.
- Paths use `Path` APIs only: no hardcoded separators, `/dev/null` or `/tmp`.

## Native AOT

`Geeratya.Tui` sets `PublishAot`. All projects have the trim and AOT analyzers on and warnings as errors, so any `IL*` warning fails the build. Run the publish after adding a dependency or touching anything reflection-adjacent: `dotnet publish src/Geeratya.Tui -c Release -o <dir>`, then `<dir>/geeratya --version`. Reading assembly attributes (`ApplicationInfo`) is the one metadata use and is AOT-safe.

## Terminal.Gui

`Geeratya.Tui` references Terminal.Gui 2.5.0 (stable v2, pinned in `Directory.Packages.props`). The native AOT publish has no `IL*` warnings with it and no suppressions are needed.

Layout in `Geeratya.Tui`: `Input/` (`KeyTranslator`: Terminal.Gui `Key` to the renderer-agnostic spelling of `KeyNotation`), `Rendering/` (pure: `FrameBuilder`, `StatusBar`, `PaletteTable`, `Palette`, `TextWidth`; no Terminal.Gui), `Views/` (`ShellScreen`, the only code that builds views; draws the frame in palette colours and redraws it on resize), `ShellHost` (loop, keys to `AppReducer`, state to `ShellScreen.Render`). Core knows no Terminal.Gui types.

Settings and pitfalls:

- Use the instance API: `Application.Create()`, `application.Init()`, `application.Run(window)`; no static `Application.Init`.
- Keys are read from `application.Keyboard.KeyDown` and marked `Handled`, so no view or default binding sees them. The default app key bindings (`Esc` quits) are cleared at start, otherwise `Esc` would quit; quitting is the keymap's decision.
- Letters arrive as upper-case key codes; without `Shift` they are lower-case characters. `KeyTranslator` handles this (tested). A terminal sends `Esc` immediately followed by a key as `Alt-<key>`, as in any terminal program.
- Non-interactive stdin or stdout (pipes, `TERM=dumb`) never starts Terminal.Gui: the binary prints a one-line explanation and exits 1. `--version` and `--print-keymap-doc` work everywhere.
