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
| `Space` | move right | leader for Geeratya actions: `<space>t` new tab, `<space>c` close tab, `<space>r` refresh, `<space>l` layout, `<space>o` open in the browser, `<space>p` parent issue, `<space>g` jump to an issue by key |
| `Alt-1` to `Alt-9` | none | go to tab 1 to 9 |
| `v` (planned, M3) | Visual mode | Visual mode over rows |
| `Alt-h` `Alt-j` `Alt-k` `Alt-l` (planned, M2) | none | move the selected card or row (status or rank) |

## Layout for writes and Scrum (planned, M2 to M4)

Decided in #152 so that M2 and M3 bind into a plan. Nothing here is in `keymap.default.json` yet: each issue adds its rows (and runs `scripts/gen-keymap-doc.sh`) when its feature lands. Source: Gitunia's keymap redesign (one noun one prefix, no unrelated case pairs, command levels), adapted to Jira.

### Layout rules

1. **One noun, one `<space>` group.** `i` issue, `m` comment, `s` sprint, `k` rank, `v` view options (M4). A verb is the second key: `<space>is` changes the status of the issue. The M1 leaves `t c r l o p` stay leaves, and a leaf is never also a prefix (the conflict test enforces it), so no group may start with them. That is why comment is `m` (`c` closes a tab) and rank is `k` (`r` refreshes).
2. **No bare letters.** Writes never take a bare vim key (see "Key rule"). Two families only: `<space>` chords and `Alt-` chords. `Alt-h/j/k/l` move the selected card or row (status or rank); they mirror the vim motions, so nothing is learned twice. Every `Alt-` action also has a palette command (rule 6), which covers terminals that send no Alt (macOS Terminal.app without "Option as Meta"); `Ctrl-s` in editors needs the same check in #167 (flow control) with a fallback.
3. **No case pairs with unrelated meanings.** A capital is allowed only for the same verb with a narrower or wider target (`ic` create, `iC` create a sub-task of the selected issue).
4. **Everyday and advanced.** Each action has a level; M5 implements the setting and the filtering (#196). Everyday is what a normal working day needs, advanced is rare or risky. The level never changes a key: an advanced action stays bound and runs from the palette, it is only left out of hints and which-key.
5. **Same keys in every view.** A write acts on the selected issue of the active tab in list, columns, backlog and issue detail alike (in detail: the shown issue, or the comment under the cursor for `m` actions). A key that does not apply (a sprint key on a Kanban board, `Alt-h` in a list) shows a status note naming the reason and does nothing else.
6. **Every action has an id and a palette command** (`issue.create` is `:create`), so keys can be remapped later (M5) and the mouse path runs the same action.
7. **Stray keys never write.** Printable keys act in Normal mode only; text fields, pickers and forms are Insert mode. Destructive actions go through Confirm mode (`y`, `n`, `Esc`; `Enter` never confirms) and say what changes and that Jira cannot undo it.

### Planned bindings

The `<space>v` rows (M4) are provisional; nothing else depends on them. Level: E everyday, A advanced. Mouse: the equivalent once the mouse part exists (right-click opens a context menu with the same actions; the other hit areas are named). Milestone: the issue that binds the key.

| Keys | Action id | What | Level | Mouse | Bound by |
|---|---|---|---|---|---|
| `<space>is` | `issue.transition` | Change status: picker of the available transitions, then required fields | E | click the status badge | M2 #159 |
| `Alt-l` / `Alt-h` | `issue.transition-next` / `issue.transition-previous` | Columns layout: move the card to the next or previous column (a form opens when fields are required) | E | drag the card to another column | M2 #159 |
| `<space>ic` | `issue.create` | Create an issue (type picker, then the form) | E | `+` in the view header | M2 #162 |
| `<space>iC` | `issue.create-subtask` | Create a sub-task of the selected issue | E | context menu | M2 #162 |
| `<space>ie` | `issue.edit` | Edit a field: field picker (summary, description, priority, labels, components, estimate, other editable fields), then its editor | E | click the field in the detail | M2 #164, M3 #183 |
| `<space>ia` | `issue.assign` | Assign: people picker | E | click the assignee | M2 #164 |
| `<space>im` | `issue.assign-me` | Assign to me | E | context menu | M2 #164 |
| `<space>g` | `issue.jump` | Jump to an issue by key (prompt with completion from the tab and recent keys; read-only) | E | none yet | M5 #194 (bound), #300 |
| `<space>id` | `issue.delete` | Delete the issue (Confirm names the key and its sub-tasks; no undo) | A | context menu | M2 #165 (bound) |
| `<space>il` | `issue.link` | Link to another issue (type picker, then issue picker) | A | context menu | M4 #190 |
| `<space>iu` | `issue.unlink` | Remove the link under the cursor (Confirm) | A | context menu on the link row | M4 #190 |
| `<space>ma` | `comment.add` | Add a comment (multi-line editor) | E | `+` in the comments section | M2 #167 |
| `<space>me` | `comment.edit` | Edit the comment under the cursor (your own; others are refused with the reason) | E | click the comment | M2 #167 |
| `<space>md` | `comment.delete` | Delete the comment under the cursor (Confirm; no undo) | A | context menu on the comment | M2 #167 |
| `Alt-k` / `Alt-j` | `rank.up` / `rank.down` | Rank the selected issue one place up or down (a marked set moves as a block once #181 adds marks) | E | drag the row or card | M2 #169 |
| `<space>kt` / `<space>kb` | `rank.top` / `rank.bottom` | Rank to the top or bottom of the list or column | A | context menu | M2 #169 |
| `<space>kk` | `rank.to` | Rank before or after an issue chosen in a picker, also across sprint sections | A | drag across sections | M2 #169, M3 #182 |
| `<space>sv` | `sprint.switch` | Switch the sprint shown (active, future, closed) | E | click the sprint name | M3 #177 |
| `<space>sm` | `sprint.move-issues` | Move the issue, the marked issues or the Visual range to a sprint (picker) | E | drag onto a sprint section | M3 #181 |
| `<space>sb` | `sprint.move-to-backlog` | Move the same set to the backlog | E | drag onto the backlog section | M3 #181 |
| `<space>sn` | `sprint.create` | Create a sprint (name, goal, dates) | E | `+` in the backlog header | M3 #185 |
| `<space>se` | `sprint.edit` | Edit the sprint shown (name, goal, dates) | A | click the sprint header | M3 #185 |
| `<space>ss` | `sprint.start` | Start the sprint (Confirm: dates, issue count) | E | button in the sprint header | M3 #186 |
| `<space>sc` | `sprint.complete` | Complete the sprint (Confirm: says where the N open issues go) | E | button in the sprint header | M3 #186 |
| `<space>vf` | `view.quick-filter` | Toggle board quick filters | E | click a filter chip | M4 #192 |
| `<space>vs` | `view.swimlane` | Choose the swimlane grouping | A | click the swimlane header | M4 #192 |
| `<space>vv` / `<space>vw` | `view.saved-open` / `view.saved-save` | Open or save a saved view | E | tab picker | M4 #191 |
| `v` | `selection.visual` | Start or end Visual mode (a contiguous range of rows) | E | drag over rows | M3 #181 |
| `Alt-m` | `selection.mark` | Mark or unmark the selected row and move down (marks may be scattered) | E | `Ctrl`-click | M3 #181 |

The estimate has no key of its own: the `<space>ie` field picker offers the board's estimation field (#183). Deleting a sprint is not planned (sprints are completed; the web UI does the rest).

Checks against the M1 keys (the generated conflict test is the proof when rows are added): `v`, `Alt-h/j/k/l` and `Alt-m` are new; `Alt-1` to `Alt-9` are untouched; `<space>i m s k v` are free and none of the planned sequences is a prefix of an M1 leaf or of another binding. `v` keeps vim's Visual meaning. The `Alt-` board keys are Normal mode only.

### Selection and counts

- **Target of a write.** One set, decided in this order: the marked rows (`Alt-m`), else the Visual range, else the selected row. Marks and the range belong to the tab, survive scrolling and refresh by issue key, and `Esc` clears them, but only at the view root: inside an issue the stack pops first (#181 updates the `ux.md` Esc paragraph). In Visual mode the first `Esc` leaves the range, the next clears the marks. The status bar shows `N selected` while a set is active.
- **Which writes accept a set.** Only the sprint moves (`sm`, `sb`) and the rank steps (`Alt-j`, `Alt-k`, which move the set as one block, order kept). Every other write is single-issue: with a set active it refuses (`5 issues selected; clear the selection first`) instead of acting on the cursor row or on all of them. A key never widens or narrows its target silently.
- **Confirming a set.** A write on more than one issue always asks in Confirm mode, e.g. `Move 5 issues to sprint "Sprint 12"? Jira has no undo. (y/n)`; the text names the count, the target and the first keys. Failures are reported per issue after the run (`4 of 5 moved; GCS-7 failed: <reason>`) and the failed ones stay marked, so a retry is one key.
- **Counts.** A count prefix (`5j`) repeats a motion or a step: `3 Alt-j` ranks three places. Any other write with a count is refused; a count never selects issues. The count prefix is added to the keymap with the first feature that uses it (#169), once for all; `0` is a count digit only after another digit (as in vim), so a lone `0` stays "first column".
- **Visual mode** joins the mode table in `ux.md` when #181 lands: `v` enters, the motions (`j k Ctrl-d Ctrl-u g g G`) extend the range, `Esc` leaves, the write keys above work in it. It exists in list and backlog layouts; in the columns layout marks are the selection.

### Text input

- **Editors** (comment, description, other multi-line fields) are Insert mode: `Enter` is a new line, `Ctrl-s` saves, `Esc` cancels and asks first when the text changed. Single-line fields: `Enter` accepts.
- **Forms** (transition, create, edit) show their fields in order; `Tab` and `Shift-Tab` move between fields (inside a form they do not change tab), `Ctrl-s` submits, `Esc` cancels (asks when changed). Required fields are marked and checked before the request. Built (#158): the three keys are Insert-mode rows (`form.field-next`, `form.field-previous`, `form.submit`); `Enter` accepts a field and, on the last one, submits, which is also the way out where a terminal swallows `Ctrl-s`. `Ctrl-o` (`form.open-browser`, #279) opens the issue page in the browser, for fields the form cannot edit; the form stays as it is. The key shows in the form footer only when a field is read-only. In vim `Ctrl-o` jumps back in the jump list; Geeratya has no jump list, so the key is free here. Its mouse path (click the address) comes with #200.
- **Suggestions** (owner's decision, #213 to #215): `@` (people) and `#` plus a project key (issues) open the popup inside text editors; picker fields (labels, components, assignee, selects) suggest by context as you type. No extra key opens a picker field's list. `Up`/`Down` and `Ctrl-p`/`Ctrl-n` choose, `Enter` or `Tab` accepts, `Esc` closes the popup only.
- **Pickers** reuse the source picker's keys (`ux.md`, Insert mode).

### Not planned

A bare-letter shortcut for any write, an undo key (Jira has none), bulk delete, bulk field edit, bulk status change.
