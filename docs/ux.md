# UX

Index of the interface specification. Parts: [M1 screens](ux-screens.md), [workflows](ux-workflows.md), [key rules](ux-keymap.md), [generated key reference](keymap.md), [customising keys](customising-keys.md). [mouse](ux-mouse.md) (gesture to action id table). The planned layout for writes and Scrum (not yet bound) is in [ux-keymap.md](ux-keymap.md), "Layout for writes and Scrum".

## Shell

The shell is one full-screen window with a body, a one-line status line at the bottom and overlays on top. The first frame is drawn before any network reply. Until views exist (M1) the body is empty apart from a hint line; the M1 views (tab bar, list, columns, issue detail, source picker, network banner) are specified in [ux-screens.md](ux-screens.md).

## Modes

The input mode decides which keys act. It lives in `AppState.Mode`; Help, Command and Insert show their prompt in the status bar; Confirm shows a centred dialog and only the short `Confirm  (y/n)` in the status bar.

| Mode | Entered with | Keys | Left with |
|---|---|---|---|
| Normal | start, or leaving any other mode | the Normal table in `keymap.md` | (home mode) |
| Help | `?` | `j`/`k` or `Down`/`Up` scroll; `?`, `q` and `Esc` close | `?`, `q`, `Esc` |
| Command | `:` | printable keys and `Space` are text; `Enter` runs, `Backspace` deletes, `Esc` cancels | `Enter`, `Esc`, `Backspace` on empty text |
| Confirm | a destructive action asks | only `y`, `n` and `Esc` act; every other key is swallowed, `Enter` never confirms; the two rows and the backdrop are clickable | `y` (runs the action), `n`, `Esc`, a click outside the box (cancel) |
| Insert | `:profile add` (the first editable form), the jump-to-issue prompt (`<space>g`, same choose keys as the picker), the source picker (`<space>t`, adds `Up`/`Down`/`Ctrl-p`/`Ctrl-n` to choose), the comment editor (`<space>m a`/`m e`; `Enter` is a new line, `Ctrl-s` saves, arrows move; `Esc` asks first when the text changed), the link form (`<space>il`) and the field form (`<space>ie`, `<space>ia`, `:edit`, `:priority`, `:labels`, `:assign`; adds `Tab`/`Shift-Tab` for fields and `Ctrl-s` to save, see [ux-screens.md](ux-screens.md), "Field form overlay") | printable keys and `Space` are text; `Enter` accepts the field, `Backspace` deletes, `Esc` and `Ctrl-c` cancel (never quit) | last `Enter`, `Esc`, `Ctrl-c` |

In Insert mode printable keys are text and only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.

Paste (terminal paste, e.g. `Ctrl-Shift-V`) fills the active Insert field, and the Command palette, in every form step. Terminal.Gui delivers a bracketed paste as one `Application.Paste` event (not as keys), which the shell turns into a `TextPasted` command; terminals without bracketed paste send plain key events and already work as typing. In a single-line field only the first non-empty line is used and control characters are dropped, so a trailing newline never submits a step; the comment editor keeps all lines. The token stays masked and is never printed or logged.

## Hints

Two helps, each switched off with `:set hints off` / `:set whichkey off` (and back with `on`; `:set` alone lists every setting with value and default, `:set <name>` shows one with its help). The choice is saved in `settings.json` in the configuration directory (only the changed value is written; comments are kept, see [settings.md](settings.md)) and both are on by default.

- **Status bar hints** (right side of the status line): the main keys of the active screen (list, columns, issue detail, source picker, field form) plus the global ones (`? help`, `: command`, `q quit`). Each hint names an `ActionId` and a short label; the key shown comes from the keymap, and a hint whose action is unbound is left out. Advanced actions stay out of the hints (`ux-keymap.md`, rule 4). Off hides the whole right side.
- **Which-key popup**: while a multi-key binding is half typed in Normal mode (`g`, `<space>`, `<space> i`), a box at the bottom of the body lists the keys that continue it: the key, and the action title from `ActionCatalog`, or `+group (n)` for a longer chain. It appears at once (no delay), disappears when the sequence completes or breaks, and is generated from the keymap, never written by hand.
- **Planned (#372, from Gitunia):** the popup opens 250 ms after the pending prefix instead of at once, lists plain actions in clusters of related actions and then the `+more` groups named by noun, and `:set hints` gets `full`, `minimal` (only `? help  : commands  q quit`) and `off`.

## Focus model and key style

- Keys are spelled out everywhere: `Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`; never `^d` or `c-d`.
- Exactly one place has focus: the overlay when one is open, otherwise the body. An overlay takes every key; nothing falls through to the body.
- `Esc` backs out one level: it clears a half-typed sequence or ends the status message (`<space>x` does too), closes an overlay or cancels Command and Confirm mode. In a tab it also pops the issue stack (issue detail back to the view it was opened from). In Normal mode with nothing to back out of, `Esc` does nothing; it never quits.
- `Ctrl-c` quits from Normal, Help and Command mode (never from Confirm).
- `q` quits only in Normal mode. In Help it closes the overlay; in Command mode it is text; in Confirm mode it does nothing.
- Every quit (`q`, `Ctrl-c`, `:q`) asks first while writes are running (`1 write is still running; its result will not be shown. Quit anyway?`, `y`/`n`/`Esc`); after the `y` the app waits up to 5 s for them to finish before it exits; so do `:profile switch` and removing the active profile.
- Multi-key bindings (`g g`) show the typed prefix in the status line; any key that does not continue the binding drops it.
- A key that no mode binds is ignored silently.

## Confirm mode

Destructive or hard-to-undo actions (delete, bulk edits, transitions that may fire workflow side effects (a move to a done status, or a screen with nothing to fill in; other single transitions do not ask; see `architecture.md`, "Write pipeline", Confirm flow), changing assignee or sprint for many issues) ask first. The dialog says exactly what will change and that Jira cannot undo it, and answers only to `y`, `n` and `Esc`; `Enter` never confirms. The reducer receives a `ConfirmRequested` command with the question and the action to run on `y`.

The dialog (`ConfirmOverlay`, sized by `ConfirmDialog` in Core) is a box centred over the body: the title `Confirm`, the question wrapped to the box (it names issue keys and counts), a blank row and the warning (the sentences that say nothing can be undone, in the warning colour, bold in the no-colour tier), a blank row, then `y  <name of the action>` and `n  Cancel`. The wording of each question is the one the request carries (`ConfirmRequest`); the label of the `y` row is its `Label` (`Delete issue`, `Remove link`, ...). The box is at most 64 columns wide and needs 4 columns of margin.

- Only `y` runs the action, after the dialog has closed; `n` and `Esc` cancel; every other key is swallowed and nothing behind the dialog moves.
- Mouse: the `y` and `n` rows are clickable (same as the keys); a click anywhere outside the box, the status line included, cancels like `n`; a click inside the box but off the rows does nothing (`ux-mouse.md`).
- What the action does is fixed when the dialog opens (the request carries the write), not when `y` is pressed. The input mode before the dialog is restored when it closes (the comment editor gets its text back).
- If the dialog does not fit the terminal (under 28 columns, or fewer rows between the tab bar and banner and the status line than the wrapped text needs, 8 for a short question) it is not shown and the action does not start: the status line says `Terminal too small to confirm. Nothing was done; enlarge it and try again.` An answer nobody can read is no confirmation. A dialog that stops fitting after a resize is closed unanswered, with a warning in the status line, and nothing runs. Until the first size is reported (the first frame) the check is skipped and the status line keeps the whole question.

## Profiles

A profile is a Jira site plus an account (`Core/Profiles`); one is active. The palette manages them:

- `:profile add` opens the Insert-mode form, one field per step: name, site URL (https; a bare host gets `https://`), account email, API token. The token is masked (`*` per character) and never printed, logged or kept in a printable value (`SecretText`). `Enter` validates the field (empty name, an existing name, a non-https URL, a malformed email, an empty token stay on the step with the reason in the status line); the last `Enter` stores the token in the OS secret store under the profile's credential key and saves `profiles.json`. The first profile becomes active.
- No secret store (Linux without `secret-tool`, macOS without `security`, or an unknown system): the profile is still saved and the status line says to set `GEERATYA_TOKEN`. A store that fails (locked keyring) reports the error and saves nothing.
- `GEERATYA_TOKEN` is one token for the whole process, so it is used only while exactly one profile exists. With several profiles a profile without a stored token does not connect and the status line says to store a token for it (`:profile remove` and `:profile add` again; without a secret store several profiles are not usable); the variable is never sent to another profile's host. A profile already connected through the variable stays connected after a second profile is added; a switch back then needs a stored token.
- `:profile list` (or `:profile`) opens the profile list overlay (Insert mode): one row per profile with account and site, the active one marked; `Down`/`Up` choose, `Enter` switches (the same switch as `:profile switch`), `Ctrl-x` asks to remove (the same question as `:profile remove`), `Esc` closes; a click on a row switches and a right-click asks to remove. With no profile the status line says to add one. A switch or removal is refused with "Still saving" while a profile change is being saved.
- `:profile switch <name>` makes another profile active and saves the choice. The connection is replaced: views and the memory cache belong to the active profile and are dropped with it (the disk cache of a profile is kept until the profile is removed). `AppState.Session` holds the tabs (the cache is a host object dropped by the same effect, `docs/architecture.md`, "Cache and refresh"); when `ProfilesChanged` changes the active profile (including removal of the active one) the reducer replaces it and emits `DropSiteCacheEffect` for the old profile. The host (`SiteConnections`) then closes that connection and builds the new one from the profile's site, account and token; a failure reply for a profile that is no longer active is ignored.
- Connection check: at start, after `:profile add` and a switch, and on `:profile check`, the shell asks `/myself` (off the UI thread, after the first frame). `SiteSession.Connection` is `Checking`, `Connected(display name)` or a failure with its kind and hint (no token, 401 token or email wrong, 403, cannot reach the host, timeout, host refused, 429). The start screen and, when no notice or pending keys occupy it, the status line show `Connected as <name> on <host>` or the hint; the token and the email are never repeated in them. A reply for a profile that is no longer active is ignored.
- `:profile remove <name>` asks in Confirm mode, naming the profile, site and account and saying the removal cannot be undone, then deletes the profile and its token (the token stays while another profile uses the same site and account).
- A `profiles.json` that cannot be parsed is ignored, a copy is kept as `profiles.json.corrupt`, and the status line says so at start. A start without a home directory (`HOME` unset and no absolute `XDG_*` directory) prints the reason to standard error and exits with code 1.

The start screen shows the active profile, or `:profile add` when there is none. Mouse paths for these commands are in [ux-mouse.md](ux-mouse.md) (part O, the profile list rows).

## Command palette

`:` opens a one-line prompt. It understands `quit` (or `q`), `help` (or `h`), `profile ...` (see Profiles), `tab ...`, `history clear`, `theme [name]` (switch the colour theme, see "Colour tiers"), `keys reload` (read the user's `keymap.json` again) and the view commands `jql [query]`, `filter [name]`, `board [name]`, `backlog [name]`, which open a new tab (see [ux-screens.md](ux-screens.md), "Palette commands"; the list is generated in [keymap.md](keymap.md)). Anything else shows "Unknown command: ..." in the status line.

**Planned (#368, from Gitunia):** `:` and `Ctrl-p` open a fuzzy list of every action (title or action id, with its keys, grouped by `ActionCatalog.GroupOf`, the level filter of #196 applied); `Enter` or a click runs the action as its key would. Typed commands with arguments (`jql <query>`, `tab ...`, `profile ...`, `set`) keep working as a command line when the first word is a command name followed by a space.

## Help

`?` opens an overlay generated from the keymap and the action catalog, grouped by mode; it is never hand-written.

**Planned (#371, from Gitunia):** pages for the active layout, then Global, All actions, Commands and (only with a `keymap.json`) Custom, with a `/` filter; the completeness test of #196 stays the gate.

## Status bar

The bottom line is a bar of exactly the terminal width. It starts at column 0 with a mode badge (` NORMAL `, ` HELP `, ` COMMAND `, ` CONFIRM `, ` INSERT `), followed by the mode's text: the pending keys or message in Normal mode, `:text` in Command mode, `Confirm  (y/n)` in Confirm mode (the question is in the dialog; the status line keeps the question only when no dialog is drawn), the step, field and typed text (the token masked) in Insert mode. Short key hints are right-aligned: the screen hints of the active tab first, then the global `? help`, `: command`, `q quit`. The screen hints are the main keys of the layout, from the keymap: list `j/k move`, `Enter open`, `Space o browser` (plus `Space l columns` on a board); columns `h/l column`, `j/k card`, `Enter open`, `Space l list`, `Space o browser`; issue detail `j/k scroll`, `Enter open`, `Esc back`, `Space o browser`, `Space p parent`. Each hint names its action id (a pair like `j/k` names two) and a test checks that the default keymap binds it; a remapped key shows up. The global hints are kept as long as they fit; the screen hints are dropped one by one from the end first (as in Gitunia). In Normal mode with no notice or pending keys the text starts with the network word of the active tab (`offline`, `rate limited 25s`, `stale`, `error`, `loading`), then the connection line. When the line is too narrow the hints are dropped first, then the text is cut (wide characters are never split).

**Status messages (#369, from Gitunia).** Every notice is a message with a severity, a lifetime counted from the injected `Tick` (or the key press that carries a time; the reducer reads no clock) and a place in the history. Info lasts 5 s, a warning 15 s, an error 30 s. Warnings and errors start with `Warning: ` or `Error: ` (never colour alone) and are drawn in the warning or error role over the bar; info keeps the bar's colours. Failed writes, a failed profile or secret-store step, an unusable keymap or cache write and refused browser opens are warnings or errors; the rest is info. A key press no longer clears a message; it ends by expiry, by `Esc` in Normal mode when nothing else claims it (no chord, count or rank pickup; it comes before the issue stack's back-out), by `<space>x` or by being replaced. The same text while it still shows is not posted again and does not renew the timer. The last 50 messages are kept, newest first; `<space>n` (or a click on the message) opens them in an overlay on the help screen's mode (badge `MESSAGES`; `j`/`k` scroll, `Esc`/`q` close). Reducers post by setting `AppState.Message` (and `MessageSeverity`, or `StatusMessages.Warn`/`Fail`); `AppReducer` settles the result into `AppState.Status` and the history, so a reducer that clears `Message` changes nothing. A write's "started" text (`Saving ...`) is a message too and is replaced by its result.

## Colour tiers

Colours are decided by roles (`PaletteRole`) in `PaletteTable`, from the colours of the active theme ("Themes" below); views never name a colour. Three tiers, picked from `ConsoleCapabilities`: 24-bit (`COLORTERM=truecolor` or `24bit`), 16-colour (forces Terminal.Gui's 16-colour output) and no colour (`NO_COLOR` set, `TERM=dumb`, `TERM=vt100` and relatives, redirected output); classic consoles (Linux `TERM=linux`, Windows console host) are capped at 16 colours even when `COLORTERM` claims more, and use the ASCII glyph set. In the no-colour tier meaning rests on text attributes (the status bar is reversed, error and network roles are bold or underlined), so nothing relies on colour alone. Text roles must reach contrast 4.5 against their background, the divider role 3.0, in both colour tiers, for every built-in theme (tested; the 16-colour check uses typical xterm values on a dark terminal and a table with darker normal colours on a light one). Network-state roles (error, loading, offline, rate limited, stale) tag the network banner and its badge (`TextRole.Offline`, `RateLimited`, `Stale`, `Error`); see [ux-screens.md](ux-screens.md), "Connection check and network states".

**Themes (#198, shaped by Gitunia).** A theme is a JSON file (comments and trailing commas allowed). Built-in themes are the same files, embedded in the binary: `auto` (default, see below), `catppuccin-mocha`, `catppuccin-macchiato`, `catppuccin-frappe`, `catppuccin-latte`, `high-contrast` (text at 7:1 or better at 24 bits), `tokyo-night` (and `-storm`, `-moon`, `-day`), `gruvbox-dark-hard|medium|soft` and `gruvbox-light-hard|medium|soft`, `dracula`, `nord`, `solarized-dark`, `solarized-light`, `rose-pine` (and `-moon`, `-dawn`). Published palettes (all MIT) with only the colours that must pass contrast nudged. `dark` and `light` are accepted names for `catppuccin-mocha` and `catppuccin-latte` (there is no separate generic palette); `auto` (the default) picks `catppuccin-latte` on a light terminal background and `catppuccin-mocha` otherwise (see below). The colour modes stay 24-bit, 16 colours and none; there is no 256-colour tier. `:theme [name]` switches live (no name: lists the theme families and any files that were not used, because the status line is one row; `:theme tokyo` lists the names that contain the word when it is not a theme itself) and saves the name in `settings.json` (`theme`). A saved theme that cannot be used starts as `catppuccin-mocha` with a status line notice.

`auto` (#375): before the UI starts (whatever theme is saved, so `:theme auto` works later), the terminal is asked for its background with OSC 11 (followed by a DA1 query, whose reply ends the wait early; 50 ms at most for a terminal that stays silent). No answer falls back to `COLORFGBG`, then to dark. The query is skipped when not interactive, at `NO_COLOR`, for `TERM` unset, `dumb`, `linux`, `ansi`, `cons25` or the `vt100` family, and on Windows (unsupported: only `COLORFGBG` is read there); Linux and macOS ask. The answer is kept: `:theme auto` later in the session uses it (the terminal cannot be asked once the UI owns the input) and saves `auto`. A reply still under way when the 50 ms end (or any reply over SSH) gets a second window of 250 ms, so a late reply never reaches the UI as keystrokes. Detection happens once per start, so a terminal switched between light and dark needs a restart.

Your own themes are `themes/<name>.json` in the configuration directory; the lower-case file name is the theme name and a user file wins over a built-in of the same name (`dark`, `light` and `auto` and names with a space cannot be used). The folder is read once at startup, so edits show after a restart; a file that cannot be read or is invalid is not offered and the reasons show on the status line at startup and in `:theme`. User themes are not checked for contrast.

```jsonc
{
  "version": 1,            // optional; the only format there is
  "variant": "dark",       // required: dark or light, the side of the terminal the theme is made for
  "colors": {              // all required, "#rrggbb" only
    "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7", "warn": "#f9e2af",
    "info": "#89dceb", "muted": "#979db5", "border": "#6c7086", "barBackground": "#181825",
    "selection": "#45475a", "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e"
  },
  "ansi": { "accent": "brightBlue" }   // optional 16-colour overrides, see below
}
```

`PaletteTable` builds every role from these colours (headings, links and in-progress statuses use `accent`; done, stories and code `good`; errors and bugs `bad`; epics and stale `remote`; keys, editable fields, rate limits and the `Warning` role (the Confirm dialog's warning line, bold) `warn`; loading, field labels and sub-tasks `info`; the status bar sits on `barBackground`, the selected row on `selection`). An unknown key is ignored; a missing or malformed colour, an unknown `variant` or `version` or a bad `ansi` entry refuses the file, one reason each.

**The 16-colour rule.** At 16 colours the terminal's own palette decides what the named colours look like, so a theme cannot carry its hex values there. The `variant` picks the built-in 16-colour table (dark: bright accents on black; light: normal-intensity accents on white; selection and status bar are white on blue in both) and the optional `ansi` object overrides single colours (`accent`, `good`, `bad`, `remote`, `warn`, `info`, `muted`, `border`) with one of `black blue green cyan red magenta yellow gray darkGray brightBlue brightGreen brightCyan brightRed brightMagenta brightYellow white` (case-insensitive). Selection stays reversed without colour in every theme.

Screens produce styled text, not colours: Core's `TextRole` (body, muted, heading, strong, emphasis, code, link, quote, selected, key, the three status categories, the five issue-type colours, error) tags each `StyledSpan`, and `PaletteTable.For` maps it to a `PaletteRole`. Selection is reversed and bold in the no-colour tier, so it never rests on colour alone; emphasis and quotes use italics there, links underline, strong and headings bold. The colour tiers apply the same bold, italic and underline as the no-colour tier (so emphasis shows everywhere) but never reverse, since a role there has its own background.

## Print frame

`geeratya --print-frame [--size WxH] [--mode normal|help|command|confirm|insert] [--help-scroll N]` prints the plain-text frame the shell would draw for that size (default 80x24, no colour, works without a terminal) and exits. It is the basis of the snapshot tests in `tests/Geeratya.Tui.Tests/Snapshots` at 120x36, 80x24 and 40x10; the frame builder is also tested at every size from 0x0 up so a resize never throws. The help overlay is longer than 36 rows, so besides `help-120x36` the snapshots `help-scroll20|40|60|80|88-120x36` (`--help-scroll N`, with `--mode help`; N is the first visible help line) together cover every mode section and the palette commands to the last line; a test checks that. When the help grows past the last of them, add a further scroll snapshot. To refresh a snapshot after an intended change, rerun the command into the matching file and review the diff.
