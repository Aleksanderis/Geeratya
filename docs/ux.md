# UX

Index of the interface specification. Parts: [M1 screens](ux-screens.md), [workflows](ux-workflows.md), [key rules](ux-keymap.md), [generated key reference](keymap.md). Mouse arrives with its own part.

## Shell

The shell is one full-screen window with a body, a one-line status line at the bottom and overlays on top. The first frame is drawn before any network reply. Until views exist (M1) the body is empty apart from a hint line; the M1 views (tab bar, list, columns, issue detail, source picker, network banner) are specified in [ux-screens.md](ux-screens.md).

## Modes

The input mode decides which keys act. It lives in `AppState.Mode`; Help, Command, Confirm and Insert show their prompt in the status bar.

| Mode | Entered with | Keys | Left with |
|---|---|---|---|
| Normal | start, or leaving any other mode | the Normal table in `keymap.md` | (home mode) |
| Help | `?` | `j`/`k` or `Down`/`Up` scroll; `?`, `q` and `Esc` close | `?`, `q`, `Esc` |
| Command | `:` | printable keys and `Space` are text; `Enter` runs, `Backspace` deletes, `Esc` cancels | `Enter`, `Esc`, `Backspace` on empty text |
| Confirm | a destructive action asks | only `y`, `n` and `Esc` act | `y` (runs the action), `n`, `Esc` (cancel) |
| Insert | `:profile add` (the first editable form), the source picker (`<space>t`, adds `Up`/`Down`/`Ctrl-p`/`Ctrl-n` to choose) | printable keys and `Space` are text; `Enter` accepts the field, `Backspace` deletes, `Esc` and `Ctrl-c` cancel (never quit) | last `Enter`, `Esc`, `Ctrl-c` |

In Insert mode printable keys are text and only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.

Paste (terminal paste, e.g. `Ctrl-Shift-V`) fills the active Insert field, and the Command palette, in every form step. Terminal.Gui delivers a bracketed paste as one `Application.Paste` event (not as keys), which the shell turns into a `TextPasted` command; terminals without bracketed paste send plain key events and already work as typing. Only the first non-empty line is used and control characters are dropped, so a trailing newline never submits a step. The token stays masked and is never printed or logged.

## Focus model and key style

- Keys are spelled out everywhere: `Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`; never `^d` or `c-d`.
- Exactly one place has focus: the overlay when one is open, otherwise the body. An overlay takes every key; nothing falls through to the body.
- `Esc` backs out one level: it clears a half-typed sequence or the status message, closes an overlay or cancels Command and Confirm mode. In a tab it also pops the issue stack (issue detail back to the view it was opened from). In Normal mode with nothing to back out of, `Esc` does nothing; it never quits.
- `Ctrl-c` quits from Normal, Help and Command mode (never from Confirm).
- `q` quits only in Normal mode. In Help it closes the overlay; in Command mode it is text; in Confirm mode it does nothing.
- Multi-key bindings (`g g`) show the typed prefix in the status line; any key that does not continue the binding drops it.
- A key that no mode binds is ignored silently.

## Confirm mode

Destructive or hard-to-undo actions (delete, bulk edits, workflow-firing transitions, changing assignee or sprint for many issues) ask first. The prompt says exactly what will change and that Jira cannot undo it, and answers only to `y`, `n` and `Esc`; `Enter` never confirms. The reducer receives a `ConfirmRequested` command with the question and the action to run on `y`.

## Profiles

A profile is a Jira site plus an account (`Core/Profiles`); one is active. The palette manages them:

- `:profile add` opens the Insert-mode form, one field per step: name, site URL (https; a bare host gets `https://`), account email, API token. The token is masked (`*` per character) and never printed, logged or kept in a printable value (`SecretText`). `Enter` validates the field (empty name, an existing name, a non-https URL, a malformed email, an empty token stay on the step with the reason in the status line); the last `Enter` stores the token in the OS secret store under the profile's credential key and saves `profiles.json`. The first profile becomes active.
- No secret store (macOS for now, or Linux without `secret-tool`): the profile is still saved and the status line says to set `GEERATYA_TOKEN`. A store that fails (locked keyring) reports the error and saves nothing.
- `GEERATYA_TOKEN` is one token for the whole process, so it is used only while exactly one profile exists. With several profiles a profile without a stored token does not connect and the status line says to store a token for it (`:profile remove` and `:profile add` again; without a secret store several profiles are not usable); the variable is never sent to another profile's host. A profile already connected through the variable stays connected after a second profile is added; a switch back then needs a stored token.
- `:profile list` (or `:profile`) shows the names, the active one marked `*`.
- `:profile switch <name>` makes another profile active and saves the choice. The connection is replaced: views and the memory cache belong to the active profile and are dropped with it (the disk cache of a profile is kept until the profile is removed). `AppState.Session` holds the tabs (the cache is a host object dropped by the same effect, `docs/architecture.md`, "Cache and refresh"); when `ProfilesChanged` changes the active profile (including removal of the active one) the reducer replaces it and emits `DropSiteCacheEffect` for the old profile. The host (`SiteConnections`) then closes that connection and builds the new one from the profile's site, account and token; a failure reply for a profile that is no longer active is ignored.
- Connection check: at start, after `:profile add` and a switch, and on `:profile check`, the shell asks `/myself` (off the UI thread, after the first frame). `SiteSession.Connection` is `Checking`, `Connected(display name)` or a failure with its kind and hint (no token, 401 token or email wrong, 403, cannot reach the host, timeout, host refused, 429). The start screen and, when no notice or pending keys occupy it, the status line show `Connected as <name> on <host>` or the hint; the token and the email are never repeated in them. A reply for a profile that is no longer active is ignored.
- `:profile remove <name>` asks in Confirm mode, naming the profile, site and account and saying the removal cannot be undone, then deletes the profile and its token (the token stays while another profile uses the same site and account).
- A `profiles.json` that cannot be parsed is ignored, a copy is kept as `profiles.json.corrupt`, and the status line says so at start. A start without a home directory (`HOME` unset and no absolute `XDG_*` directory) prints the reason to standard error and exits with code 1.

The start screen shows the active profile, or `:profile add` when there is none. Mouse paths for these commands arrive with the mouse part.

## Command palette

`:` opens a one-line prompt. This stub understands `quit` (or `q`), `help` (or `h`), `profile ...` (see Profiles); anything else shows "Unknown command: ..." in the status line. Commands to search and jump arrive with their features.

## Help

`?` opens an overlay generated from the keymap and the action catalog, grouped by mode; it is never hand-written.

## Status bar

The bottom line is a bar of exactly the terminal width. It starts at column 0 with a mode badge (` NORMAL `, ` HELP `, ` COMMAND `, ` CONFIRM `, ` INSERT `), followed by the mode's text: the pending keys or message in Normal mode, `:text` in Command mode, the question and `(y/n)` in Confirm mode, the step, field and typed text (the token masked) in Insert mode. Short key hints (`? help`, `: command`, `q quit`) are right-aligned; each hint names its action id and a test checks that the default keymap binds it. When the line is too narrow the hints are dropped first, then the text is cut (wide characters are never split).

## Colour tiers

Colours are decided by roles (`PaletteRole`) in `PaletteTable`; views never name a colour. Three tiers, picked from `ConsoleCapabilities`: 24-bit (`COLORTERM=truecolor` or `24bit`), 16-colour (forces Terminal.Gui's 16-colour output) and no colour (`NO_COLOR` set, `TERM=dumb`, redirected output). In the no-colour tier meaning rests on text attributes (the status bar is reversed, error and network roles are bold or underlined), so nothing relies on colour alone. Text roles must reach contrast 4.5 against their background, the divider role 3.0, in both colour tiers (tested; the 16-colour check uses typical xterm values). Network-state roles (error, loading, offline, rate limited, stale) exist from the start and are wired when the states are.

Screens produce styled text, not colours: Core's `TextRole` (body, muted, heading, strong, emphasis, code, link, quote, selected, key, the three status categories, error) tags each `StyledSpan`, and `PaletteTable.For` maps it to a `PaletteRole`. Selection is reversed and bold in the no-colour tier, so it never rests on colour alone; emphasis and quotes use italics there, links underline, strong and headings bold. The colour tiers apply the same bold, italic and underline as the no-colour tier (so emphasis shows everywhere) but never reverse, since a role there has its own background.

## Print frame

`geeratya --print-frame [--size WxH] [--mode normal|help|command|confirm|insert]` prints the plain-text frame the shell would draw for that size (default 80x24, no colour, works without a terminal) and exits. It is the basis of the snapshot tests in `tests/Geeratya.Tui.Tests/Snapshots` at 120x36, 80x24 and 40x10; the frame builder is also tested at every size from 0x0 up so a resize never throws. To refresh a snapshot after an intended change, rerun the command into the matching file and review the diff.
