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
- `keys` is one key or a space-separated sequence (`g c`). A key is a single character (`j`, `?`, `G`) or a name (`Esc`, `Enter`, `Backspace`, `Tab`, `Shift-Tab`, `Space`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `Delete`), optionally prefixed `Ctrl-` or `Alt-`. Letters are the character itself (`G`, not `Shift-g`). The leader chords are written with `Space`: `Space o`.
- `action` is an action id. The ids and their titles are listed in the source of `ActionCatalog`; the key reference shows the title next to each default key, and the ids are in `src/Geeratya.Core/Keymap/keymap.default.json`.
- Comments (`//`) and trailing commas are allowed.

Your rows come first, so the hints in the status bar and the help overlay show the key you chose for an action.

## What is checked

When the file is read, every row is checked. A row that is wrong is skipped, the rest still applies, and the first problem is shown in the status line (with a count of the others):

- an unknown action id or a bad key spelling;
- the same keys bound twice in one mode;
- a sequence that would hide another one (`g` while `g g` is bound). Unbind the other one first, in the same file;
- a printable key in Command or Insert mode (those keys are text);
- a dialog-only action (confirm, form, palette submit) bound in Normal mode;
- an `unbind` row that matches no default.

A file that is not valid JSON (or has an unknown `mode`) is ignored as a whole and the defaults stay. Geeratya never crashes on this file.

Keep the vim rule in mind ([ux-keymap.md](ux-keymap.md), "Key rule"): the defaults leave bare letters alone for vim's sake, but your file may bind whatever you like. The terminal decides which `Ctrl-` and `Alt-` combinations arrive at all (`Ctrl-i` is `Tab`, `Ctrl-m` is `Enter`, and a terminal without "Option as Meta" sends no `Alt-` keys).

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
