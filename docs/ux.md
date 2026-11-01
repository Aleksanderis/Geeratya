# UX

Index of the interface specification. Parts: [key rules](ux-keymap.md), [generated key reference](keymap.md). Mouse, colour tiers, status bar and `--print-frame` arrive with the rest of the app shell (#15) and get their own parts.

## Shell

The shell is one full-screen window with a body, a one-line status line at the bottom and overlays on top. The first frame is drawn before any network reply. Until views exist (later milestones) the body is empty apart from a hint line.

## Modes

The input mode decides which keys act. It lives in `AppState.Mode`; Help, Command and Confirm show their prompt in the status line (the full status bar is #15).

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
