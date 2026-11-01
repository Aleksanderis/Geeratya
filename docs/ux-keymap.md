# Key design

Rules for the keymap. The reference of what is bound is generated from `src/Geeratya.Core/Keymap/keymap.default.json` into [keymap.md](keymap.md) by `scripts/gen-keymap-doc.sh`; a test fails when it is stale or the file has conflicts.

## File format

Rows of `{ "mode", "keys", "action" }`. `keys` is one key or a space-separated sequence (`g g`). A key is a single character (`j`, `?`, `G`) or a name (`Esc`, `Enter`, `Backspace`, `Tab`, `Shift-Tab`, `Space`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `Delete`), optionally prefixed `Ctrl-` or `Alt-`. Uppercase letters are the character itself (`G`, not `Shift-g`). `action` is an id from `ActionCatalog`, which carries the title shown in help and the reference.

Checks (`KeymapTableTests`): no two rows with the same keys in one mode, no sequence shadowed by a shorter binding (`g` and `g g`), every action known, every key spelled correctly, every catalog action bound. Actions bound ahead of their feature are listed in `ActionCatalog.Unimplemented` (a test checks that pressing them changes nothing); actions that exist only in a modal mode are listed in `ActionCatalog.ModalOnly` (`IsModalOnly`; a test checks they are never bound in Normal mode and all others are). Status bar hints and the start-screen hint carry a short label and an `ActionId`; the key shown is the first one the keymap binds to that action in the mode, so a remapped key shows up in the hints.

## Key rule

- Standard vim and neovim keys keep their vim meaning (`h j k l`, `g g`, `G`, `Ctrl-d`, `Ctrl-u`, `/`, `n`, `N`, `:`, `q`).
- A Geeratya-specific action never takes a bare vim normal-mode command. Vim's bare keys include `s u c a x r D M E` and every other letter that vim binds; Geeratya actions use a `<space>` leader chord or a `Ctrl-`/`Alt-` combination instead. Check a new key against vim before binding it.
- Everything doable by keyboard is doable by mouse and the reverse (the mouse table comes with the mouse part of the shell).
- Arrow keys, `Home`, `End`, `PageUp` and `PageDown` mirror the vim motions.
- Printable keys are text in Command mode and in any text field; only `Esc`, `Enter`, `Backspace`, `Ctrl-` and `Alt-` combinations act there.

## Vim clash list

Keys with a vim meaning that Geeratya keeps or deliberately does not reuse for its own actions:

| Key | Vim meaning | Geeratya |
|---|---|---|
| `h j k l` | motions | navigation |
| `g g`, `G` | top, bottom | top, bottom |
| `Ctrl-d`, `Ctrl-u` | half page down, up | page down, up |
| `:` | command line | command palette |
| `q` | record a macro | quit (Normal mode only; there are no macros) |
| `?` | search backwards | help (search arrives with `/` and `n`/`N` in a later issue) |
| `s u c a x r D M E` and other bare letters | edit commands | not bound by Geeratya actions |
| `0` `$` | line start, line end | first and last column of the board |
| `] ]` `[ [` | next and previous section | next and previous section of the issue |
| `g t` `g T` | next and previous tab | next and previous tab |
| `Tab` `Shift-Tab` | `Tab` jumps forward in the jump list (`Ctrl-i`); `Shift-Tab` has no meaning | next and previous tab (the jump list does not exist) |
| `Enter` | next line | open the selected item |
| `Space` | move right | leader for Geeratya actions: `<space>t` new tab, `<space>c` close tab, `<space>r` refresh, `<space>l` layout, `<space>o` open in the browser, `<space>p` parent issue |
| `Alt-1` to `Alt-9` | none | go to tab 1 to 9 |
