# Settings

Geeratya keeps a few switches in `settings.json` in the configuration directory (next to `profiles.json`; see [customising-keys.md](customising-keys.md) for the paths). The settings screen (`:settings` or `<space>,`) changes them visually; `:set` (and the command palette) does the same from the command line, and the file can be edited by hand too.

## The settings

This table is generated from the settings registry (`SettingsRegistry` in `Geeratya.Core/Settings`) and a test compares it with the registry, so a new setting is one registry entry and one row here.

| `:set` name | File key | Values | Default | What it does |
|---|---|---|---|---|
| `hints.statusBar` | `hints.statusBar` | full, minimal or off | `full` | How many key hints the right of the status bar shows: all of them, only help, commands and quit, or none. |
| `hints.whichKey` | `hints.whichKey` | on or off | `on` | Show the popup of the next keys while a multi-key binding is half typed. |
| `commandlevel` | `commandLevel` | everyday or advanced | `everyday` | Which actions help, the palette, which-key and the hints list: everyday ones only, or advanced too. |
| `theme` | `theme` | a theme name | `auto` | The colour theme: a built-in one or a file in the themes folder. |
| `jump.target` | `jump.target` | current or tab | `current` | Where the jump prompt (<space>g) opens an issue: on top of the current tab, or in a new tab. |
| `startup.restore` | `startup.restore` | on or off | `on` | Reopen the tabs of the last run on start; off starts on the board picker every time. |

## Settings screen

`:settings`, `<space>,` or the item of the tab and view menu (`<space>A`) opens a full-body screen in the shared frame (tab row shows one `Settings` chip). Categories are on the left, the rows of the selected category on the right, a help row (what the setting does, its default and values) and a hint row below. Under 60 columns the categories collapse into one list with a heading per category.

- The rows come from the registry: every setting, with its name, current value, default and choices. A setting added to `SettingsRegistry` shows up without a change to the screen; its `Category` picks the group (Appearance, Hints and keys, Startup, else Other). After the settings come the Profiles row (Enter opens the profile manager) and the read-only About rows: the path of `settings.json`, the path of `keymap.json` (edit it, then `:keys reload`) and the version.
- The screen runs in Normal mode with the Normal keys: `j`/`k` (and `gg`, `G`, `Ctrl-d`, `Ctrl-u`) move, `h`/`l` jump to the previous or next category, `Enter` flips a toggle or opens the choice list of a choice, `Alt-r` resets the row to its default, `Esc` closes the list, then the screen. `?`, `:` and `q` work as everywhere; any other key does nothing.
- A change applies at once and is saved through the same writer as `:set` (comments in the file survive); a value the setting refuses shows what it takes and nothing changes. A choice that is a free text without a list is edited with `:set name value`.
- The theme list previews: moving over a theme draws it without saving; `Enter` applies and saves it, `Esc` (or leaving the screen another way) draws the saved theme again.
- Mouse: click a category or a row (a row acts like `Enter`), right-click a row to reset it, click a word of an open list to take it, wheel over the rows to move ([ux-mouse.md](ux-mouse.md)).

## Command level

`commandLevel` hides the rare or risky actions (delete, unlink, rank to the top, bottom or a place, delete a comment) from the lists that teach keys: help, the palette's default list, the which-key popup and the status bar hints. It never disables anything: every key keeps working and the palette finds an advanced action as soon as you type part of its name. `<space>*` switches the level for the current session only and says which level is on; `:set commandlevel advanced` makes it permanent.

## `:set`

- `:set` opens the Messages overlay (`<space>n`) with every setting, one line each with value, default and help.
- `:set <name>` shows its value, default and accepted values on the status line (the help text is in the `:set` overlay); the message stays under 80 columns.
- `:set <name> <value>` changes it at once and saves it. `:set theme <name>` is the same as `:theme <name>`.

## The file

The file is JSON; comments and trailing commas are allowed. Example:

```json
{
  "hints": {
    // Hide the hints once I know the keys.
    "statusBar": "off",
    "whichKey": true,
  },
  "theme": "nord",
}
```

- A change replaces only the value of that key: comments, key order, indentation and trailing commas stay. A key the file lacks is added as its last member. A missing file is created with only that key.
- Settings are grouped: `hints.statusBar` is the member `statusBar` of the object `hints`. A change creates a missing parent object; a parent that is not an object (`"hints": 3`) is never overwritten: the status line says so and nothing is written.
- Older files with the flat names `statusHints` and `whichKey` still load (the grouped key wins when both exist). The flat key is not rewritten or removed; the next change to that setting writes the grouped key. The old `:set hints` and `:set whichkey` names still work.
- Keys the app does not know are left alone.
- A value that is not valid (wrong type or unknown value) is read as the default and the status line says so; changing that setting writes a valid value over it.
- A file that does not parse is never written: the status line names the problem and its line, and the defaults are used for the session.
- If the file changed on disk after the app read it, the app reads it again and asks you to repeat the change; nothing is overwritten blindly.
- The write goes through a temporary file in the same folder and a rename, so a crash never leaves half a file. A symbolic link is followed and stays a link.
