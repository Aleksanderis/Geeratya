# UX

Index of the interface specification. Parts: [M1 screens](ux-screens.md), [workflows](ux-workflows.md), [key rules](ux-keymap.md), [generated key reference](keymap.md). Mouse arrives with its own part. The planned layout for writes and Scrum (not yet bound) is in [ux-keymap.md](ux-keymap.md), "Layout for writes and Scrum".

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
| Insert | `:profile add` (the first editable form), the jump-to-issue prompt (`<space>g`, same choose keys as the picker), the source picker (`<space>t`, adds `Up`/`Down`/`Ctrl-p`/`Ctrl-n` to choose), the comment editor (`<space>m a`/`m e`; `Enter` is a new line, `Ctrl-s` saves, arrows move; `Esc` asks first when the text changed), the field form (`:labels` until `<space>ie` binds it; adds `Tab`/`Shift-Tab` for fields and `Ctrl-s` to save, see [ux-screens.md](ux-screens.md), "Field form overlay") | printable keys and `Space` are text; `Enter` accepts the field, `Backspace` deletes, `Esc` and `Ctrl-c` cancel (never quit) | last `Enter`, `Esc`, `Ctrl-c` |

In Insert mode printable keys are text and only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.

Paste (terminal paste, e.g. `Ctrl-Shift-V`) fills the active Insert field, and the Command palette, in every form step. Terminal.Gui delivers a bracketed paste as one `Application.Paste` event (not as keys), which the shell turns into a `TextPasted` command; terminals without bracketed paste send plain key events and already work as typing. In a single-line field only the first non-empty line is used and control characters are dropped, so a trailing newline never submits a step; the comment editor keeps all lines. The token stays masked and is never printed or logged.

## Focus model and key style

- Keys are spelled out everywhere: `Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`; never `^d` or `c-d`.
- Exactly one place has focus: the overlay when one is open, otherwise the body. An overlay takes every key; nothing falls through to the body.
- `Esc` backs out one level: it clears a half-typed sequence or the status message, closes an overlay or cancels Command and Confirm mode. In a tab it also pops the issue stack (issue detail back to the view it was opened from). In Normal mode with nothing to back out of, `Esc` does nothing; it never quits.
- `Ctrl-c` quits from Normal, Help and Command mode (never from Confirm).
- `q` quits only in Normal mode. In Help it closes the overlay; in Command mode it is text; in Confirm mode it does nothing.
- Every quit (`q`, `Ctrl-c`, `:q`) asks first while writes are running (`1 write is still running; its result will not be shown. Quit anyway?`, `y`/`n`/`Esc`); after the `y` the app waits up to 5 s for them to finish before it exits; so do `:profile switch` and removing the active profile.
- Multi-key bindings (`g g`) show the typed prefix in the status line; any key that does not continue the binding drops it.
- A key that no mode binds is ignored silently.

## Confirm mode

Destructive or hard-to-undo actions (delete, bulk edits, transitions that may fire workflow side effects (a move to a done status, or a screen with nothing to fill in; other single transitions do not ask; see `architecture.md`, "Write pipeline", Confirm flow), changing assignee or sprint for many issues) ask first. The prompt says exactly what will change and that Jira cannot undo it, and answers only to `y`, `n` and `Esc`; `Enter` never confirms. The reducer receives a `ConfirmRequested` command with the question and the action to run on `y`.

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

`:` opens a one-line prompt. It understands `quit` (or `q`), `help` (or `h`), `profile ...` (see Profiles), `tab ...` and the view commands `jql <query>`, `filter [name]`, `board [name]`, `backlog [name]`, which open a new tab (see [ux-screens.md](ux-screens.md), "Palette commands"; the list is generated in [keymap.md](keymap.md)). Anything else shows "Unknown command: ..." in the status line.

## Help

`?` opens an overlay generated from the keymap and the action catalog, grouped by mode; it is never hand-written.

## Status bar

The bottom line is a bar of exactly the terminal width. It starts at column 0 with a mode badge (` NORMAL `, ` HELP `, ` COMMAND `, ` CONFIRM `, ` INSERT `), followed by the mode's text: the pending keys or message in Normal mode, `:text` in Command mode, the question and `(y/n)` in Confirm mode, the step, field and typed text (the token masked) in Insert mode. Short key hints are right-aligned: the screen hints of the active tab first, then the global `? help`, `: command`, `q quit`. The screen hints are the main keys of the layout, from the keymap: list `j/k move`, `Enter open`, `Space o browser` (plus `Space l columns` on a board); columns `h/l column`, `j/k card`, `Enter open`, `Space l list`, `Space o browser`; issue detail `j/k scroll`, `Enter open`, `Esc back`, `Space o browser`, `Space p parent`. Each hint names its action id (a pair like `j/k` names two) and a test checks that the default keymap binds it; a remapped key shows up. The global hints are kept as long as they fit; the screen hints are dropped one by one from the end first (as in Gitunia). In Normal mode with no notice or pending keys the text starts with the network word of the active tab (`offline`, `rate limited 25s`, `stale`, `error`, `loading`), then the connection line. When the line is too narrow the hints are dropped first, then the text is cut (wide characters are never split).

## Colour tiers

Colours are decided by roles (`PaletteRole`) in `PaletteTable`; views never name a colour. Three tiers, picked from `ConsoleCapabilities`: 24-bit (`COLORTERM=truecolor` or `24bit`), 16-colour (forces Terminal.Gui's 16-colour output) and no colour (`NO_COLOR` set, `TERM=dumb`, redirected output). In the no-colour tier meaning rests on text attributes (the status bar is reversed, error and network roles are bold or underlined), so nothing relies on colour alone. Text roles must reach contrast 4.5 against their background, the divider role 3.0, in both colour tiers (tested; the 16-colour check uses typical xterm values). Network-state roles (error, loading, offline, rate limited, stale) tag the network banner and its badge (`TextRole.Offline`, `RateLimited`, `Stale`, `Error`); see [ux-screens.md](ux-screens.md), "Connection check and network states".

Screens produce styled text, not colours: Core's `TextRole` (body, muted, heading, strong, emphasis, code, link, quote, selected, key, the three status categories, the five issue-type colours, error) tags each `StyledSpan`, and `PaletteTable.For` maps it to a `PaletteRole`. Selection is reversed and bold in the no-colour tier, so it never rests on colour alone; emphasis and quotes use italics there, links underline, strong and headings bold. The colour tiers apply the same bold, italic and underline as the no-colour tier (so emphasis shows everywhere) but never reverse, since a role there has its own background.

## Print frame

`geeratya --print-frame [--size WxH] [--mode normal|help|command|confirm|insert] [--help-scroll N]` prints the plain-text frame the shell would draw for that size (default 80x24, no colour, works without a terminal) and exits. It is the basis of the snapshot tests in `tests/Geeratya.Tui.Tests/Snapshots` at 120x36, 80x24 and 40x10; the frame builder is also tested at every size from 0x0 up so a resize never throws. The help overlay is longer than 36 rows, so besides `help-120x36` the snapshots `help-scroll20|40|60|80|88-120x36` (`--help-scroll N`, with `--mode help`; N is the first visible help line) together cover every mode section and the palette commands to the last line; a test checks that. When the help grows past the last of them, add a further scroll snapshot. To refresh a snapshot after an intended change, rerun the command into the matching file and review the diff.
