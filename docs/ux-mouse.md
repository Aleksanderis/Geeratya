# UX: mouse

Part of the UX spec; start at [ux.md](ux.md). Model: the mouse note of the owner's other terminal client (a table of gesture to action id), rewritten for Geeratya.

## Principle

Mouse input is optional and never the only way: everything doable by mouse is doable by keyboard and the reverse. A gesture on an area becomes the **action id** a key would produce, and the same reducer handler runs it (the `ActionRequested` command, which a wheel notch repeats 3 times like `3j`). There is no second code path for the mouse.

## Layers

1. The terminal driver reports SGR mouse events (Terminal.Gui enables them; no setting is needed).
2. `MouseFlagTranslator` (Tui, `Input/`) turns the toolkit's flags into a `MouseGesture` (click, double-click, right-click, Ctrl-click, wheel up or down). Presses, releases, moves, other buttons and a sideways wheel are ignored.
3. `MouseHitTester` (Core, pure) turns a cell and a `MouseContext` (size, header rows, overlay open, issue open) into a `MouseTarget`: the tab bar row, the banner row, the body (lists or detail) and the status line.
4. `DefaultMouseMap` (Core) gives the row for the target and gesture; `MouseResolver` makes the `ActionRequested` command from it.

While an overlay, prompt, picker, form, editor, help or confirm is open, only the tab bar and banner rows count, as the `Backdrop` (a click there closes the overlay like `Esc`); the overlay's own rectangle is not known to the hit test, so clicks in the body then do nothing instead of closing what the user clicked on. Wheel events do nothing while an overlay is open.

## Parts

The table is delivered in three parts of issue #200: **A** the input layer and this table (wheel, status line, backdrop; the other rows exist in the table but are not wired yet: `MouseResolver.WiredParts`), **B** view interaction (select a row by click, tab and banner clicks, context menu, the finer targets inside rows), **C** writes and drags (status badge, assignee, `+` buttons, comments, drag between columns, rank). A row moves to the wired set when its part lands.

## Table

A test parses this table and checks it against `DefaultMouseMap`; add a row here in the same change that adds a path in code. `ContextMenu` rows are the entries of the right-click menu: each action that has no other path is listed there, so every action a key can run has a mouse path or an entry under "Without a path".

| Target | Gesture | Action | Part | Effect |
|---|---|---|---|---|
| Lists | Wheel up | `nav.up` | A | Moves the selection up 3 rows (like `3k`) |
| Lists | Wheel down | `nav.down` | A | Moves the selection down 3 rows (like `3j`) |
| Detail | Wheel up | `nav.up` | A | Scrolls the opened issue up 3 lines (like `3k`) |
| Detail | Wheel down | `nav.down` | A | Scrolls the opened issue down 3 lines (like `3j`) |
| Status | Click | `palette.open` | A | Opens the command palette, same as `:` |
| Backdrop | Click | `mode.cancel` | A | While an overlay or prompt is open, a click on the tab bar or banner rows closes it, same as `Esc` (the editor asks first when its text changed) |
| Lists | Double-click | `item.open` | B | Opens the issue under the pointer, same as `Enter` (the first click selects the row) |
| Lists | Right-click | `palette.open` | B | Opens the context menu of the row (a command palette limited to the actions of the row) |
| Detail | Right-click | `palette.open` | B | Opens the context menu of the line under the pointer |
| Tabs | Click | `tab.next` | B | Switches to the clicked tab (same as `Alt-N` with its number) |
| NewTabButton | Click | `tab.new` | B | Opens the source picker, same as `<space>t` |
| Banner | Click | `view.refresh` | B | Retries or refreshes, same as `<space>r` (error and offline banners) |
| StatusBadge | Click | `issue.transition` | C | Opens the transition picker for the issue |
| Assignee | Click | `issue.assign` | C | Opens the people picker |
| NewIssueButton | Click | `issue.create` | C | Starts the create flow |
| CommentsAdd | Click | `comment.add` | C | Opens the comment editor |
| Comment | Click | `comment.edit` | C | Edits the comment under the pointer |
| Lists | Drag | `issue.transition-next` | C | Columns layout: drag a card to another column to change its status (`Alt-l` / `Alt-h` per column; a form opens when fields are required) |
| Lists | Ctrl-click | `rank.to` | C | Rank: pick up the row, then `Ctrl`-click the place to drop it (`<space>kk` twice) |
| ContextMenu | Click | `view.layout-toggle` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.open-in-browser` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.open-parent` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.edit` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.assign-me` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.transition-previous` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.delete` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.create-subtask` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.link` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.unlink` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `issue.jump` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `rank.up` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `rank.down` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `rank.top` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `rank.bottom` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `comment.delete` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `tab.close` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `tab.previous` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `help.toggle` | C | Item of the context menu: same as its key |
| ContextMenu | Click | `app.quit` | C | Item of the context menu: same as its key |

## Without a path

Actions with no row of their own, each with the reason (a test checks this list against `DefaultMouseMap.Exclusions`): the motions `nav.left`, `nav.right`, `nav.top`, `nav.bottom`, `nav.page-down`, `nav.page-up`, `nav.first-column` and `nav.last-column` (the pointer places the cursor and the wheel scrolls), `nav.up` and `nav.down` (the wheel rows), `detail.section-next` and `detail.section-previous` (a click on a section title), and `tab.go-to.1` to `tab.go-to.9` (a click on tab N).

## Writes

Destructive actions reached by mouse (context menu `issue.delete`, `comment.delete`, `issue.unlink`, drags that change status or rank) go through the same Confirm mode as their keys and say what will change; a drag never writes on release without the same checks as its key.
