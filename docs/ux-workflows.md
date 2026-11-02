# UX: workflows

The flows of Milestone 1 (read your board), step by step. Screens and their states are in [ux-screens.md](ux-screens.md), keys in [keymap.md](keymap.md). Keys are spelled as in `ux-keymap.md`. All flows are read-only; writes arrive with M2. Every network step is asynchronous: the frame is drawn first and the result fills in later.

## 0. Start, connection check, first view

1. The shell draws its first frame before any network reply: tab bar, empty body with the hint line, status bar.
2. With an active profile the status line shows `Checking <host>…` and the body stays as it is (cached views are shown with the `STALE` banner once the cache issue stores them). Without a profile the start screen says `:profile add`.
3. The check is `GET /myself`. Success: `Connected as <name>`. 401: the banner explains the token was rejected (screens, "Errors"). Network failure: `OFFLINE`, cached views stay usable.
4. With no views yet, `<space>t` opens the source picker (workflow 1). The previous session's tabs are restored from the cache in M1's cache issue; until then the shell starts with no tab.

## 1. Open a view

By picker: `<space>t` (or `:tab new`), type to filter, `Up`/`Down` or `Ctrl-p`/`Ctrl-n` to choose, `Enter` opens it in a new tab, `Esc` closes. By palette: `:jql <query>`, `:filter [name]`, `:board [name]`, `:backlog [name]`, which open a new tab too. A new tab becomes active at once and shows `Loading…` (or its cache); a failure shows the error body in that tab only, other tabs are untouched.

A board opens in Columns, everything else in List. `<space>l` switches a board between Columns and List.

## 2. Browse a list

`j`/`k` move, `Ctrl-d`/`Ctrl-u` half pages, `g g`/`G` top and bottom. Reaching the end of the loaded rows loads the next page. `Enter` opens the issue (workflow 4). `<space>o` opens the selected issue in the browser. `<space>r` refreshes: rows stay, the selection is kept by key.

## 3. Browse a board

`h`/`l` change column, `0`/`$` jump to the first and last, `j`/`k` move between cards. Columns that do not fit scroll horizontally with the selection. `Enter` opens the selected card. There is no card movement in M1 (transitions arrive with M2); `<space>l` shows the same issues as a list.

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
- Creating, deleting and commenting wait for the reply; the form shows `Saving…` and stays open on an error, with each field's message at the field.
- A second write on an issue that is still being written is refused (`GCM-3 is busy`), not queued. Other issues are free. Refresh waits until the writes end; `<space>r` is remembered and runs after.
- If the answer is lost (timeout, network), the app says it is checking, re-reads the issue and then shows the true state; it never repeats a write by itself.
- Delete, bulk changes and assignee or sprint changes for several issues ask first (`y`/`n`/`Esc`): the text names what changes and says Jira cannot undo it.
- The same issue in other tabs changes too; a list tab whose query no longer matches keeps the row, marked stale, until its next reload.
- Closing a tab does not stop a write that was sent; quitting or switching profile with writes running asks first.

## 9. Edit with the field form

Built for the labels pilot; transition, create and the other edits open the same form (`ux-screens.md`, "Field form overlay").

1. `:labels` on a selected row (or the shown issue) opens the form at once with `Loading fields…`; the metadata (`editmeta`) is fetched, never at startup. A failure shows on the top line and `Esc` closes the form.
2. The labels field starts with the issue's current labels. Type a label and press `Space` or `Enter` to add it; `Backspace` on an empty buffer removes the last one. `Ctrl-s` saves (also from any field), and `Enter` on the empty buffer of the last field does too.
3. The row shows the new labels at once. If Jira refuses, the row goes back and the form stays open with the message at the field. If the answer is lost, the form stays locked while the issue is checked, as in workflow 8.
4. Typing in a labels, person or parent field opens the suggestion list under it (`ux-screens.md`, "Field form overlay"): `Down`/`Up` or `Ctrl-n`/`Ctrl-p` choose, `Tab` or `Enter` accept the highlighted row (the label is added, the person or issue becomes the value), `Esc` closes the list only. `Space` adds the typed label as it is; so does `Enter` on the `(new)` row.
5. `Esc` with changes asks `y`/`n`; without changes it closes. Closing while the save runs leaves the write running and its result goes to the status line.
