# UX

Index of the interface specification. Parts: [key rules](ux-keymap.md), [generated key reference](keymap.md). Mouse arrives with its own part.

## Shell

The shell is one full-screen window with a body, a one-line status line at the bottom and overlays on top. The first frame is drawn before any network reply. Until views exist (later milestones) the body is empty apart from a hint line.

## Modes

The input mode decides which keys act. It lives in `AppState.Mode`; Help, Command and Confirm show their prompt in the status bar.

| Mode | Entered with | Keys | Left with |
|---|---|---|---|
| Normal | start, or leaving any other mode | the Normal table in `keymap.md` | (home mode) |
| Help | `?` | `j`/`k` or `Down`/`Up` scroll; `?`, `q` and `Esc` close | `?`, `q`, `Esc` |
| Command | `:` | printable keys and `Space` are text; `Enter` runs, `Backspace` deletes, `Esc` cancels | `Enter`, `Esc`, `Backspace` on empty text |
| Confirm | a destructive action asks | only `y`, `n` and `Esc` act | `y` (runs the action), `n`, `Esc` (cancel) |

Insert mode (editing a field) arrives with the first editable field: printable keys are text and only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.

## Focus model and key style

- Keys are spelled out everywhere: `Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`; never `^d` or `c-d`.
- Exactly one place has focus: the overlay when one is open, otherwise the body. An overlay takes every key; nothing falls through to the body.
- `Esc` backs out one level: it clears a half-typed sequence or the status message, closes an overlay or cancels Command and Confirm mode. In Normal mode with nothing to back out of, `Esc` does nothing; it never quits.
- `Ctrl-c` quits from Normal, Help and Command mode (never from Confirm).
- `q` quits only in Normal mode. In Help it closes the overlay; in Command mode it is text; in Confirm mode it does nothing.
- Multi-key bindings (`g g`) show the typed prefix in the status line; any key that does not continue the binding drops it.
- A key that no mode binds is ignored silently.

## Confirm mode

Destructive or hard-to-undo actions (delete, bulk edits, workflow-firing transitions, changing assignee or sprint for many issues) ask first. The prompt says exactly what will change and that Jira cannot undo it, and answers only to `y`, `n` and `Esc`; `Enter` never confirms. The reducer receives a `ConfirmRequested` command with the question and the action to run on `y`.

## Command palette

`:` opens a one-line prompt. This stub understands `quit` (or `q`), `help` (or `h`); anything else shows "Unknown command: ..." in the status line. Commands to search and jump arrive with their features.

## Help

`?` opens an overlay generated from the keymap and the action catalog, grouped by mode; it is never hand-written.

## Status bar

The bottom line is a bar of exactly the terminal width. It starts at column 0 with a mode badge (` NORMAL `, ` HELP `, ` COMMAND `, ` CONFIRM `), followed by the mode's text: the pending keys or message in Normal mode, `:text` in Command mode, the question and `(y/n)` in Confirm mode. Short key hints (`? help`, `: command`, `q quit`) are right-aligned; each hint names its action id and a test checks that the default keymap binds it. When the line is too narrow the hints are dropped first, then the text is cut (wide characters are never split).

## Colour tiers

Colours are decided by roles (`PaletteRole`) in `PaletteTable`; views never name a colour. Three tiers, picked from `ConsoleCapabilities`: 24-bit (`COLORTERM=truecolor` or `24bit`), 16-colour (forces Terminal.Gui's 16-colour output) and no colour (`NO_COLOR` set, `TERM=dumb`, redirected output). In the no-colour tier meaning rests on text attributes (the status bar is reversed, error and network roles are bold or underlined), so nothing relies on colour alone. Text roles must reach contrast 4.5 against their background, the divider role 3.0, in both colour tiers (tested; the 16-colour check uses typical xterm values). Network-state roles (error, loading, offline, rate limited, stale) exist from the start and are wired when the states are.

## Print frame

`geeratya --print-frame [--size WxH] [--mode normal|help|command|confirm]` prints the plain-text frame the shell would draw for that size (default 80x24, no colour, works without a terminal) and exits. It is the basis of the snapshot tests in `tests/Geeratya.Tui.Tests/Snapshots` at 120x36, 80x24 and 40x10; the frame builder is also tested at every size from 0x0 up so a resize never throws. To refresh a snapshot after an intended change, rerun the command into the matching file and review the diff.
