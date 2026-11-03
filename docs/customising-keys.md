# Customising keys

Geeratya reads an optional `keymap.json` that changes the default keys. The defaults and the design rules stay as they are; your file only adds, replaces or removes rows. The full list of what is bound is the generated [key reference](keymap.md); the rules the defaults follow are in [ux-keymap.md](ux-keymap.md).

## Where the file lives

It sits next to `profiles.json`, in the configuration directory:

| System | Path |
|---|---|
| Linux | `$XDG_CONFIG_HOME/geeratya/keymap.json`, else `~/.config/geeratya/keymap.json` |
| macOS | the same as Linux (`~/.config/geeratya/keymap.json`) |
| Windows | `%APPDATA%\Geeratya\keymap.json` |

The file is optional. Geeratya never writes it.

## Format

```json
{
  "bindings": [
    { "mode": "normal", "keys": "Ctrl-j", "action": "nav.down" },
    { "mode": "normal", "keys": "Ctrl-k", "action": "nav.up" },
    { "mode": "normal", "keys": "g c", "action": "view.layout-toggle" }
  ],
  "unbind": [
    { "mode": "normal", "keys": "q" }
  ]
}
```

- `bindings`: rows of `mode`, `keys`, `action`. A row replaces the default row with the same mode and keys. Default keys for the same action stay (so `Ctrl-j` above is an extra key for moving down). To move an action to another key, add the new row and unbind the old keys.
- `unbind`: default rows to remove, named by `mode` and `keys`.
- `mode` is one of `normal`, `command`, `help`, `confirm`, `insert`.
- `keys` is one key or a space-separated sequence (`g c`). A key is a single character (`j`, `?`, `G`) or a name (`Esc`, `Enter`, `Backspace`, `Tab`, `Shift-Tab`, `Space`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `Delete`), optionally prefixed `Ctrl-` or `Alt-`. Letters are the character itself (`G`, not `Shift-g`). In the config file the leader is the key `Space` (`Space o`); the UI and docs show it as `<space>o` (`gg`, `Ctrl-w c`, `<space><Tab>`).
- `action` is an action id. The generated key reference ([keymap.md](keymap.md)) lists the action id next to the title of every default key; help (`?`, the `All actions` and `Custom` pages) and the palette show the ids too.
- Comments (`//`) and trailing commas are allowed.

Your rows come first, so the hints in the status bar and the help overlay show the key you chose for an action.

## What is checked

When the file is read, every row is checked. A row that is wrong is skipped, the rest still applies, and the first problem is shown in the status line (with a count of the others, `(+N more, <space>n)`, shared with the settings and theme problems found at the same start) and every problem is kept in the Messages overlay (`<space>n`):

- an unknown action id or a bad key spelling;
- the same keys bound twice in one mode;
- a sequence that would hide another one (`g` while `g g` is bound). Unbind the other one first, in the same file;
- a printable key in Command or Insert mode (those keys are text);
- a dialog-only action (confirm, form, palette submit) bound in Normal mode;
- an `unbind` row that matches no default.

To see what was applied, open help (`?`) and press `Tab` to the `Custom` page. It appears only when the file changed something and lists each added or changed binding (key, action title and action id, with the mode when it is not Normal) and `Unbound: <keys>` for each removed default. Rows that were skipped are not listed there (the status line names the first problem; the Messages overlay, `<space>n`, keeps every one). The filter (`/`) works on this page too.

A file that is not valid JSON (or has an unknown `mode`) is ignored as a whole and the defaults stay. Geeratya never crashes on this file.

Keep the vim rule in mind ([ux-keymap.md](ux-keymap.md), "Key rule"): the defaults leave bare letters alone for vim's sake, but your file may bind whatever you like. The terminal decides which `Ctrl-` and `Alt-` combinations arrive at all (`Ctrl-i` is `Tab`, `Ctrl-m` is `Enter`, and a terminal without "Option as Meta" sends no `Alt-` keys).

A row with no `mode` (or no `keys` or `action`) is reported and skipped; it never falls back to Normal mode.

## If you lock yourself out

`Ctrl-c` quits before the keymap is consulted (except in Insert mode, where it cancels the edit), so no `keymap.json` can remove it: an `unbind` or a binding for `Ctrl-c` is skipped with a message. Unbinding `q`, `Esc` and `:` still leaves `Ctrl-c`. To undo a bad file, edit or delete `keymap.json` from outside the app and start again.

## Applying changes

Geeratya reads the file at start. After editing it, run `:keys reload` in the command palette to apply it without a restart; the status line says `Keymap reloaded` or names the first problem.

## Examples

Quit on `Ctrl-q` and not on `q`:

```json
{
  "bindings": [ { "mode": "normal", "keys": "Ctrl-q", "action": "app.quit" } ],
  "unbind": [ { "mode": "normal", "keys": "q" } ]
}
```

Move with the arrow-like keys of a left-handed layout (extra keys beside `h j k l`):

```json
{
  "bindings": [
    { "mode": "normal", "keys": "Alt-j", "action": "nav.down" },
    { "mode": "normal", "keys": "Alt-k", "action": "nav.up" }
  ]
}
```
