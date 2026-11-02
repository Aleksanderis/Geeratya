# UX: workflows

The flows of Milestone 1 (read your board), step by step. Screens and their states are in [ux-screens.md](ux-screens.md), keys in [keymap.md](keymap.md). Keys are spelled as in `ux-keymap.md`. All flows are read-only; writes arrive with M2. Every network step is asynchronous: the frame is drawn first and the result fills in later.

## 0. Start, connection check, first view

1. The shell draws its first frame before any network reply: tab bar, empty body with the hint line, status bar.
2. With an active profile the status line shows `Checking <host>…` and the body stays as it is (cached views are shown with the `STALE` banner once the cache issue stores them). Without a profile the start screen says `:profile add`.
3. The check is `GET /myself`. Success: `Connected as <name>`. 401: the banner explains the token was rejected (screens, "Errors"). Network failure: `OFFLINE`, cached views stay usable.
4. With no views yet, `<space>t` opens the source picker (workflow 1). The previous session's tabs are restored from the cache in M1's cache issue; until then the shell starts with no tab.

## 1. Open a view

By picker: `<space>t` (or `:tab new`), type to filter, `Up`/`Down` or `Ctrl-p`/`Ctrl-n` to choose, `Enter` opens it in a new tab, `Esc` closes. The picker also lists the latest 10 recent JQL queries of the profile (group `Recent`, filtered by the typed text) before the typed query row. By palette: `:jql <query>`, `:filter [name]`, `:board [name]`, `:backlog [name]`, which open a new tab too. A new tab becomes active at once and shows `Loading…` (or its cache); a failure shows the error body in that tab only, other tabs are untouched.

A board opens in Columns, everything else in List. `<space>l` switches a board between Columns and List.

## 2. Browse a list

`j`/`k` move, `Ctrl-d`/`Ctrl-u` half pages, `g g`/`G` top and bottom. Reaching the end of the loaded rows loads the next page. `Enter` opens the issue (workflow 4). `<space>o` opens the selected issue in the browser. `<space>r` refreshes: rows stay, the selection is kept by key.

## 3. Browse a board

`h`/`l` change column, `0`/`$` jump to the first and last, `j`/`k` move between cards. Columns that do not fit scroll horizontally with the selection. `Enter` opens the selected card. `Alt-l` / `Alt-h` move the selected card to the next or previous column (workflow 10); `<space>l` shows the same issues as a list.

## 4. Open an issue, follow links, go back

1. `Enter` on a row or card pushes the issue onto the tab's stack. The cached copy shows at once, the fresh one replaces it.
2. `j`/`k` move the cursor (`>`) line by line through the document, skipping the panel edges, and the window follows it; `] ]` and `[ [` jump to the start of the next section, or of the current one and then the previous (nothing moves at either end).
3. `Enter` on a sub-task, epic child or linked issue pushes that issue (so a chain of issues is a deeper stack); on a remote link or attachment it opens the browser (the issue page for an attachment, the link's URL for a remote link; only `https` URLs, anything else is refused with a note in the status line).
4. `<space>p` pushes the parent (a status note when there is none); `<space>o` opens the cursor row in the browser (remote link, attachment or related issue) and the shown issue from any other line.
5. `Esc` pops one level. After the last pop the view is back with its selection and scroll as they were. `Esc` at the view does nothing (`q` quits, as everywhere).
6. Switching tab keeps each tab's stack; closing a tab drops its stack.

## 5. Tabs

`Tab`/`Shift-Tab` (or `g t`/`g T`) next and previous, wrapping around; `Alt-1` to `Alt-9` go to a tab by number (a missing number does nothing); `<space>c` closes (the right neighbour becomes active); `<space>t` opens a new one. State (view, layout, selection, scroll, issue stack) is per tab. Tabs are not saved across runs until the cache issue stores them.

## 6. Refresh, offline, rate limits

- `<space>r` reloads the active tab (or the open issue). Background refresh uses the same path and shows `STALE` only when it fails or the data is older than the refresh interval.
- Offline: the cached data stays, the banner shows the age of the data and the next retry (exponential back-off, 60 s doubling to 300 s, `architecture.md`); `<space>r` retries at once. A reply ends the state. An offline start shows the cached rows marked stale, or without a cache the error body.
- Rate limited: the banner counts the `Retry-After` seconds down; requests wait, nothing is dropped, and the keys keep working on the data already shown.
- A reply that is a 401 stops all automatic retries for the profile until the token is replaced (a repeating wrong token would lock the account); the banner stays.

## 7. Switch profile with views open

`:profile switch <name>` replaces the connection; every tab, stack and cache of the old profile is dropped (`ux.md` "Profiles"). The new profile starts with no tabs, and the check line runs again. A reply that arrives for the old profile is ignored.

## 8. Write something (M2)

Design in `architecture.md`, "Write pipeline". What the user sees:

- A change that a row shows (move a card, rank, assignee, a field) appears at once with a `…` marker and a status line saying what is being done; it settles when Jira answers. On failure the row goes back and the status line says why and what was not done (no permission, not found, rate limited with the wait, Jira's own text for a refused transition).
- Rank (#169, `Alt-j`/`Alt-k`, `<space>kt`/`kb`/`kk`): works in a board or backlog view (a JQL or filter list is ordered by its query, not by rank, and refuses). The issue under the cursor moves next to a neighbour the view shows: the next row, or in the columns layout the next card of its column (a card with sub-tasks shown under it moves with them). The row moves at once and carries the `…` marker until Jira answers; `Alt-j` on the last row says `already at the bottom`. In the columns layout, cards of other columns that lie between the two in the board order are named in the status line (`Ranking GCM-1 below GCM-4 (2 cards of other columns lie between)`). Rank cannot be read back from one issue, so a lost reply is not resolved to done or not done: the order goes back, the status line says to reload, and the view reloads. After a rank the view reloads (the server order is the truth). `<space>kk` picks up the issue, then drops it where the cursor stands.
- Creating, deleting and commenting wait for the reply; the form shows `Saving…` and stays open on an error, with each field's message at the field. Comments (built, #167): `<space>m a` opens the editor in the issue detail, `Ctrl-s` sends, `Esc` asks before throwing text away; `<space>m e` edits and `<space>m d` deletes (after a `y`/`n` that says Jira cannot undo it) the comment under the cursor; the comment list changes from the reply (`ux-screens.md`, "Comment editor").
- A second write on an issue that is still being written is refused (`GCM-3 is busy`), not queued. Other issues are free. Refresh waits until the writes end; `<space>r` is remembered and runs after.
- If the answer is lost (timeout, network), the app says it is checking, re-reads the issue and then shows the true state; it never repeats a write by itself.
- Delete, bulk changes and assignee or sprint changes for several issues ask first (`y`/`n`/`Esc`): the text names what changes and says Jira cannot undo it.
- Delete (#165, #316): the confirm counts the visible sub-tasks and, when there are any, adds `(and any hidden from you)`, because the delete is sent with `deleteSubtasks=true`, which removes sub-tasks the user cannot browse too, and Jira gives no count of them. An issue with no visible sub-tasks is sent with `deleteSubtasks=false` and Jira refuses it if hidden ones exist. Jira answers `404` alike for a deleted issue and for one the user cannot see, so when the delete reply is lost and the check reads `404`, the status line says `probably deleted, or hidden from you` and the row goes.
- The same issue in other tabs changes too; a list tab whose query no longer matches keeps the row, marked stale, until its next reload.
- Closing a tab does not stop a write that was sent; quitting or switching profile with writes running asks first.

## 9. Edit with the field form

The edit form (`<space>ie`, or `:edit`, `:priority`, `:labels` for one field) and the people picker (`<space>ia`); transition and create open the same form (`ux-screens.md`, "Field form overlay").

1. `<space>ie` (or `:labels`) on a selected row (or the shown issue) opens the form at once with `Loading fields…`; the metadata (`editmeta`) is fetched, never at startup. A failure shows on the top line and `Esc` closes the form.
   After editmeta the form reads the issue once with exactly the ids of the other editable fields (due date, components, versions, estimate, custom fields) so each starts with its current value: date text, the chosen options or the rich text. A user or parent field is not offered yet; if the read fails only the fields the view already holds are offered.
2. The labels field starts with the issue's current labels. Type a label and press `Space` to add it as typed, or `Enter` to take the highlighted suggestion (an exact match ranks first); `Backspace` on an empty buffer removes the last one. `Ctrl-s` saves (also from any field), and `Enter` on the empty buffer of the last field does too.
3. The row shows the new labels at once. If Jira refuses, the row goes back and the form stays open with the message at the field. If the answer is lost, the form stays locked while the issue is checked, as in workflow 8.
4. Typing in a labels, person or parent field opens the suggestion list under it (`ux-screens.md`, "Field form overlay"): `Down`/`Up` or `Ctrl-n`/`Ctrl-p` choose, `Tab` or `Enter` accept the highlighted row (the label is added, the person or issue becomes the value), `Esc` closes the list only. `Space` adds the typed label as it is; so does `Enter` on the `(new)` row.
5. `Esc` with changes asks `y`/`n`; without changes it closes. Closing while the save runs leaves the write running and its result goes to the status line.
6. Summary, labels and priority change the row at once (priority by its name, as the allowed value is called); the other fields are verified when the issue is read again. Jira has no optimistic locking on edits (last write wins, a concurrent change is not reported), so a summary edit from a stale row overwrites the newer text; the row is read again after every write, which shows the true state. Only if Jira does answer a conflict, the row goes back, the form says so and the issue is read again.

Assign: `<space>ia` opens the people picker (`ux-screens.md`), type a name, `Enter` takes the highlighted person and `Enter` again assigns (the row shows the person at once, `Backspace` on an empty field clears it). `<space>im` (or `:assign-me`) assigns the shown or selected issue to your own account without a picker; neither asks to confirm, a single assignment can be repeated.

## 10. Change status (M2)

Built in #159. Works from the list, the columns layout and the issue detail on the selected or shown issue.

1. `<space>is` (or `:status`) opens a picker with the transitions Jira offers for this issue now (`Loading fields…` while they are read; nothing is cached or stored, transition ids differ per project). Each entry is the target status, with the transition name when it differs (`Start progress → In Progress`). Typing filters, `Up`/`Down` choose, `Enter` picks, `Esc` closes.
2. A transition without fields is sent at once: the picker closes, the card shows the new status with the `…` marker and the status line says `Moving GCM-3 to In Progress`. A transition that asks for fields (a required field without a default, or a screen with fields) opens a second form with those fields, required ones first (taken from the same transitions read as the picker, so a move reads them once); `Ctrl-s` (or `Enter` on the last field) sends the transition with them, and Jira's message for a refused field shows at the field.
3. Columns layout: `Alt-l` / `Alt-h` (or `:status next` / `:status prev`, for terminals that send no Alt; plain `:status` is the picker) look up the transitions that end in the next or previous column. Exactly one: the card moves at once, no form shows, and the selection follows the card into its new column (the cursor is kept: only its column and card change, every column keeps its scroll; a rollback moves it back). Two or more: the picker lists only those. None: the picker lists every transition and its label says so. First or last column, a card in no board column, and the list layout answer with a status line note and send nothing. A column move is not offered from the issue detail.
4. A move that may fire workflow side effects asks first (`y`/`n`/`Esc`, text `Move GCM-3 to Done? Jira cannot undo this; the workflow may do more than change the status`): a transition to a status in the done category, or a screen with nothing to fill in. A transition with fields is not asked again: submitting its form is the confirmation. Declining sends nothing.
5. If Jira refuses (permission, a validator, a field) the card goes back and the status line says why; a lost reply is checked by reading the issue, as in workflow 8.

## 11. Link two issues, remove a link

Built in #190; both work on the selected row or the shown issue and the same way in company-managed and team-managed projects.

1. `<space>il` opens the field form (workflow 9) titled `Link GCM-3`, with `Loading fields…` while the link types are fetched (`GET /issueLinkType`, never at startup). The first field, `Link type`, lists each side of each type as its own choice (`blocks`, `is blocked by`, `clones`, ...; a type that reads the same both ways, `relates to`, is one choice), so type and direction are one pick, and the choice reads as "this issue <choice> the other". `Enter` picks and moves on; typing filters the list.
2. The second field, `Issue`, takes the other issue's key (lower case is accepted). `Ctrl-s` saves. A key that is not `PROJ-123`, or the issue itself, is refused at the field before anything is sent; an issue Jira does not find or may not link comes back as the status line text and the form stays open.
3. The write is not optimistic (no row shows links). When Jira confirms, the form closes, the status line says `Linked GCM-3 blocks GCM-5`, and the open detail of either issue reloads. A lost reply is checked by reading the issue, as in workflow 8.
4. In the issue detail, put the cursor on a row of `Linked issues` and press `<space>iu`: Confirm mode asks `Remove the link GCM-3 blocks GCM-5? Jira cannot undo this.` and only `y` removes it (`n` or `Esc` leaves it). Anywhere else the key says `The cursor is not on a linked issue.`
5. Only one write per issue at a time (`GCM-5 is busy (assigning)`): a link touches both issues, so both must be free.

## 12. Create an issue

1. Select an issue (or open one): the project comes from it. `<space>ic` (or `:create`) creates an issue; if the selection is an epic it becomes the parent of the new issue. `<space>iC` (or `:create subtask`) creates a sub-task of the selected issue. With nothing selected the status line says where the project comes from; a sub-task or an epic cannot be the parent of a sub-task.
2. The box opens at once with `Loading fields…`, then lists the issue types the project allows (sub-task types only for `<space>iC`). `Down`/`Up` and `Enter` choose; `Esc` closes. The types come from the project, so company-managed and team-managed projects offer their own.
3. The form lists the fields of that type, required ones first (`*`); the summary is required. The description is Markdown-like text, `Enter` adds a line. Fields the form cannot edit are listed read-only with the project link; the parent starts as the selected issue and is changed through the suggestion popup. `Ctrl-g` turns "open after" on, then the new issue opens in the detail when it is created.
4. `Ctrl-s` (or `Enter` on the last field) creates. A missing required field is marked at the field and nothing is sent; a refusal by Jira (a parent that does not fit the type, a required custom field) stays open with the message at that field. `Esc` with changes asks `y`/`n`; nothing is created by cancelling.
5. On success the view reloads, the status line says `Created GCM-12` and the form closes. If the reply is lost the app searches for the issue and says whether it was created; if that cannot be told it says so and never creates it a second time. The form stays open and unlocked, so check the view before saving again.
