# UX: mouse

Part of the UX spec; start at [ux.md](ux.md). Model: the mouse note of the owner's other terminal client (a table of gesture to action id), rewritten for Geeratya.

## Principle

Mouse input is optional and never the only way: everything doable by mouse is doable by keyboard and the reverse. A gesture on an area becomes the **action id** a key would produce, and the same reducer handler runs it (the `ActionRequested` command, which a wheel notch repeats 3 times like `3j`). There is no second code path for the mouse; the only commands of its own are `PointerPlaced` (a click places the cursor, see below) and `TabActivated`, and both drop a half-typed chord and count like an action does.

## Layers

1. The terminal driver reports SGR mouse events (Terminal.Gui enables them; no setting is needed).
2. `MouseFlagTranslator` (Tui, `Input/`) turns the toolkit's flags into a `MouseGesture` (click, double-click, right-click, Ctrl-click, wheel up or down). Presses, releases, moves, other buttons and a sideways wheel are ignored.
3. `MouseHitTester` (Core, pure) turns a cell and a `MouseContext` (size, header rows, overlay open, issue open, the cells of each tab, of the `+` button and of the `[jump]` chip, and whether the banner shows a line) into a `MouseTarget`: the tab bar row, the banner row, the body (lists or detail) and the status line.
4. `DefaultMouseMap` (Core) gives the row for the target and gesture; `MouseResolver` makes the commands from it (a list: an `ActionRequested`, or a `PointerPlaced` that carries the action to run after placing).

While an overlay, prompt, picker, form, editor or help is open, only the tab bar and banner rows count, as the `Backdrop` (a click there closes the overlay like `Esc`), and the clickable rows the overlay reports. The renderer places the overlay and hands the hit test its row rectangles (`OverlayRegion`, in `StyledFrame.Regions`), so the hit test never guesses the layout; a click anywhere else in the body does nothing instead of closing what the user clicked on. The Confirm dialog is the exception: its box is a region (`ConfirmBox`, with the `y` and `n` rows on top), and everything outside the box, the status line included, is the backdrop, because the dialog is the whole question and a click away from it is a no. A click on a row resolves to `OverlayRowClicked` (the row is chosen, then the key's handler runs); only the row targets resolve that way, and a click on a form field while it saves or asks to discard does nothing, as its keys do nothing. Wheel events do nothing while an overlay is open.

## Parts

The table is delivered in parts of issue #200: **A** the input layer and this table (wheel, status line, backdrop; the other rows exist in the table but are not wired yet: `MouseResolver.WiredParts`), **B** view interaction (select a row, card, column or line by click, double-click to open, right-click, tab and banner clicks), **O** overlays (this slice of the issue #200 part C: the rows of the source picker, jump prompt, profile list, field forms and suggestion popups; the editor text itself takes no clicks), **M** menu and drag (this slice of part C: the right-click menu of the issue under the pointer, `<space>a` as its key, drag of a card to a neighbouring column; the `+` tab button and the hidden-tab and neighbour-column marks belong to part B and are wired), **C** the rest of the writes (status badge, assignee, `+` buttons, comments, `Ctrl`-click rank). **V** the menu of the tab and the view (this slice of part C: right-click on a tab or the status line, `<space>A` as its key; `Esc` closes it). A row moves to the wired set when its part lands.

## Placing the cursor

A plain click on a list, a board or an opened issue has no action id: it does what motions do for a key and places the cursor, through the `PointerPlaced` command (the motions are listed under "Without a path"). On a list it selects the row under the pointer; on the columns layout the card under it, or the column of a clicked column header (that column keeps its own card; in a window of one column the neighbour names of the header, `‹ Name n` and `Name n ›`, select that neighbour); in an opened issue the line (the breadcrumb row and rows past the end do nothing; the view does not scroll). Rows without an issue, the view title and the area under the cards do nothing. Double-click and right-click place the cursor first, then run their action, so they act on the issue under the pointer (a right-click opens the menu of that issue: `ContextMenu` rows listed in the table, one `Actions` box; `j`/`k`, `Enter` and `Esc` work in it like in the pickers, and every item runs the handler of its key, so delete and unlink still ask first); on empty space (nothing placed) the action does not run. Cells come from the same measures the renderer draws with (`BoardHits`, `TabBar.Areas`), so a layout change moves the click areas with it.

## Table

A test parses this table and checks it against `DefaultMouseMap`; add a row here in the same change that adds a path in code. `ContextMenu` rows are the entries of the right-click menu: each action that has no other path is listed there, so every action a key can run has a mouse path or an entry under "Without a path".

| Target | Gesture | Action | Part | Effect |
|---|---|---|---|---|
| Lists | Wheel up | `nav.up` | A | Moves the selection up 3 rows (like `3k`) |
| Lists | Wheel down | `nav.down` | A | Moves the selection down 3 rows (like `3j`) |
| Detail | Wheel up | `nav.up` | A | Moves the cursor up 3 lines in the opened issue (like `3k`) |
| Detail | Wheel down | `nav.down` | A | Moves the cursor down 3 lines in the opened issue (like `3j`) |
| Status | Click | `palette.open` | A | Opens the command palette, same as `:` |
| StatusMessage | Click | `status.history` | A | A click on the message itself (not the rest of the line) opens the message history, same as `<space>n` |
| Backdrop | Click | `mode.cancel` | A | While an overlay or prompt is open, a click on the tab bar or banner rows closes it, same as `Esc` (the editor asks first when its text changed). With the Confirm dialog open the whole screen outside its box counts, the body and the status line too, and the click cancels like `n`; a click inside the box but not on a row does nothing |
| PickerRow | Click | `form.next` | O | Source picker: opens the clicked board, filter or query, same as `Enter` on it |
| JumpRow | Click | `form.next` | O | Jump prompt: opens the clicked issue, same as `Enter` on it |
| PaletteRow | Click | `form.next` | O | Command palette: runs the clicked action, same as `Enter` on it (the palette closes first, so a form or Confirm opens as usual); a click on a subheader or a gap does nothing |
| PaletteRow | Wheel up | `picker.previous` | O | Command palette: moves the highlight up 3 rows over the list (runs nothing) |
| PaletteRow | Wheel down | `picker.next` | O | Command palette: moves the highlight down 3 rows over the list (runs nothing) |
| ProfileRow | Click | `form.next` | O | Profile list: switches to the clicked profile, same as `Enter` (the switch asks first while writes run) |
| ProfileRow | Right-click | `profile.remove` | O | Profile list: asks to remove the clicked profile, same as `Ctrl-x` (Confirm mode, the token is deleted, no undo) |
| FormField | Click | `form.field-next` | O | Field form (also the create and edit forms): focuses the clicked editable field; an open suggestion popup closes without taking its row (`Tab` would take it) |
| FormChoice | Click | `form.next` | O | Row of the issue type list or of a field's suggestion popup: takes it, same as `Enter` on it |
| EditorChoice | Click | `form.next` | O | Row of the comment editor's mention popup: takes it, same as `Enter` on it |
| HelpPage | Click | `help.page-next` | O | Help popup: shows the clicked section (an entry of the menu on the left), from its first line; same as `Tab` or `Shift-Tab` until that section; the filter stays |
| ConfirmYes | Click | `confirm.yes` | O | Confirm dialog: the `y` row runs the action, same as `y` (the dialog closes first) |
| ConfirmNo | Click | `confirm.no` | O | Confirm dialog: the `n` row cancels, same as `n` |
| Lists | Double-click | `item.open` | B | Opens the issue under the pointer, same as `Enter` (the first click selects the row) |
| Detail | Double-click | `item.open` | B | Opens what the line under the pointer holds, same as `Enter` (the first click puts the cursor there) |
| Lists | Right-click | `menu.context` | M | Selects the row or card under the pointer and opens its menu (only the actions of that issue), same as `<space>a`; the command palette stays on `:` and the status line |
| Detail | Right-click | `menu.context` | M | Puts the cursor on the line under the pointer and opens the menu of the opened issue (with `comment.delete` on a comment) |
| Tabs | Click | `tab.next` | B | Switches to the clicked tab (the same switch as `Alt-N`, also for tabs past nine; the separators do nothing). The `‹n` and `+n ›` marks of a scrolled bar step to the neighbouring hidden tab |
| Tabs | Right-click | `menu.view` | V | Switches to the clicked tab and opens the menu of the tab and the view: close tab, previous tab (with several tabs), toggle layout, jump to an issue, help, quit; same as `<space>A` |
| Status | Right-click | `menu.view` | V | Opens the same menu of the tab and the view (the command palette stays on a click) |
| StatusMessage | Right-click | `menu.view` | V | Same as a right-click on the rest of the line |
| NewTabButton | Click | `tab.new` | B | Opens the source picker, same as `<space>t`; the `+` after the last tab, drawn when the bar has a spare cell and shows no hidden-tab mark |
| JumpChip | Click | `issue.jump` | B | Opens the jump prompt, same as `<space>g`; the `[jump]` chip at the right end of the tab bar row, drawn when the bar has room (at least two spare cells after the last tab or the `+`) |
| Banner | Click | `view.refresh` | B | Retries or refreshes, same as `<space>r`; only while the banner row shows a line (offline, rate limited, stale, error) |
| StatusBadge | Click | `issue.transition` | C | Opens the transition picker for the issue |
| Assignee | Click | `issue.assign` | C | Opens the people picker |
| NewIssueButton | Click | `issue.create` | C | Starts the create flow |
| CommentsAdd | Click | `comment.add` | C | Opens the comment editor |
| Comment | Click | `comment.edit` | C | Edits the comment under the pointer |
| Lists | Drag | `issue.transition-next` | M | Columns layout (a drag in the list or backlog layout is `Drag rows`): drag a card onto the next or previous column, same as `Alt-l` / `Alt-h` (a single matching transition is sent as the key does, otherwise the picker or form opens); further columns, the same column and a one-column window do nothing |
| Lists | Ctrl-click | `selection.mark` | B | List, backlog or columns layout: marks or unmarks the row or card under the pointer in place (the cursor moves there; unlike `Alt-m` it does not move down); marks may be scattered |
| Lists | Drag rows | `selection.visual` | B | List or backlog layout: drag over rows to select them, same as `v` and the motions (Visual mode from the row where the button went down to the row where it was released) |
| ContextMenu | Click | `view.layout-toggle` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `issue.open-in-browser` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.open-parent` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.edit` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.assign-me` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.transition-previous` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.delete` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.create-subtask` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.link` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.unlink` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `issue.jump` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `status.history` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `status.dismiss` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `rank.up` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `rank.down` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `rank.top` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `rank.bottom` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `comment.delete` | M | Item of the right-click menu on a list row, card or line of an opened issue: same as its key |
| ContextMenu | Click | `tab.close` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `tab.previous` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `help.toggle` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |
| ContextMenu | Click | `app.quit` | V | Item of the menu of the tab and the view (right-click on a tab or the status line, `<space>A`): same as its key |

## Without a path

Actions with no row of their own, each with the reason (a test checks this list against `DefaultMouseMap.Exclusions`): the motions `nav.left`, `nav.right`, `nav.top`, `nav.bottom`, `nav.page-down`, `nav.page-up`, `nav.first-column` and `nav.last-column` (the pointer places the cursor and the wheel scrolls), `nav.up` and `nav.down` (the wheel rows), `detail.section-next` and `detail.section-previous` (a click on a section title), `tab.go-to.1` to `tab.go-to.9` (a click on tab N), `tab.last-active` (a click on any tab reaches it directly), `help.page-previous` (a click on the section name), `help.scroll-down`, `help.scroll-up`, `help.half-page-down` and `help.half-page-up` (the wheel does nothing while help is open), `help.filter` (the filter is typed text; the section names are the mouse path) and `view.cycle-command-level` (its palette row: click the status line, then the row). `rank.to` has no pointer path yet: `Ctrl`-click now marks rows (`selection.mark`), and dragging a row to its place comes with #182.

## Checks and terminal selection

The table test only checks that the table, the code and the keymap agree (every key action has a row or a reason); it does not prove that a row's path works, so a part that wires rows adds its own tests (part O: the drawn frame of each overlay at three sizes gives the row rectangles, and a click on a rectangle runs the real resolver and reducer). Smoke check of part A (tmux, SGR sequences via `send-keys -l` against the real binary): a wheel event does nothing harmful on the start screen, a click on the status line opens the palette (status shows `COMMAND :`), a click on the tab row then closes it. Smoke check of the overlay paths (part O, `scripts/smoke-mouse-overlays.sh --binary <geeratya>`, fake Jira only, passes): a click on a source picker row opens that board; on a create form's type row takes it and a click on another field moves the focus to it; on a jump prompt row opens the issue; on a field form's suggestion row takes it; on the comment editor's mention row inserts it; in `:profile list` a right-click asks to remove (Esc keeps the profile) and a click switches. The script runs the profile list with two profiles and no usable token for the second (`GEERATYA_TOKEN` serves one profile only); the switch is proven by the start screen line changing to `second: second@example.com`. Nothing is removed: the remove confirm is cancelled with Esc (which closes the whole profile overlay) and the list is reopened. With the mouse enabled the terminal no longer selects text by dragging: hold `Shift` (most terminals) to select and copy.

## Writes

Destructive actions reached by mouse (context menu `issue.delete`, `comment.delete`, `issue.unlink`, drags that change status or rank) go through the same Confirm mode as their keys and say what will change; a drag never writes on release without the same checks as its key.

## Drag

The terminal reports the press and the release of the left button; `MouseDragTracker` pairs them into a drag when the release is on another cell (the click event the toolkit may send afterwards is dropped, so a drop never also selects). `MouseResolver.ResolveDrag` accepts it on the body only (never while an overlay is open). In the list and backlog layouts it becomes `RowsDragged`, which starts Visual mode on the row of the press and moves the cursor to the row of the release (the `Drag rows` row; no write happens). On the columns layout it becomes `CardDragged`, which the reducer treats as `Alt-l` or `Alt-h` for the card under the press: the card is selected, then the same transition rules run (one matching transition is sent at once, otherwise the picker or form opens and confirms). Only a drop on the next or previous column acts; two or more columns away says `Drop the card on the next or previous column.` and writes nothing, and a drop on the same column, outside the columns, or from a column header does nothing. A window of one column (narrow terminals) has no drop target: use `Alt-l` / `Alt-h`. Checked in a real terminal session (tmux, SGR, fake Jira, #364): the toolkit delivers `LeftButtonPressed` at the press, the motion of the held button as `LeftButtonPressed` plus `PositionReport` (the tracker ignores it, it is not a new press), `LeftButtonReleased` at the release cell and then a `LeftButtonClicked` at the release cell (swallowed). A drop sends a single matching transition at once, like the key, with no extra confirm (same as `Alt-l`, see #364; Jira has no undo for either).
