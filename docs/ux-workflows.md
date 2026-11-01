# UX: workflows

The flows of Milestone 1 (read your board), step by step. Screens and their states are in [ux-screens.md](ux-screens.md), keys in [keymap.md](keymap.md). Keys are spelled as in `ux-keymap.md`. All flows are read-only; writes arrive with M2. Every network step is asynchronous: the frame is drawn first and the result fills in later.

## 0. Start, connection check, first view

1. The shell draws its first frame before any network reply: tab bar, empty body with the hint line, status bar.
2. With an active profile the status line shows `Checking <host>…` and the profile's cached views (if any) are shown from the cache with the `STALE` banner. Without a profile the start screen says `:profile add`.
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
2. `j`/`k` move the cursor through the document; `]]` and `[[` jump between sections.
3. `Enter` on a sub-task, epic child or linked issue pushes that issue (so a chain of issues is a deeper stack); on a remote link or attachment it opens the browser (the issue page for an attachment, the link's URL otherwise).
4. `<space>p` pushes the parent; `<space>o` opens the current issue in the browser.
5. `Esc` pops one level. After the last pop the view is back with its selection and scroll as they were. `Esc` at the view does nothing (`q` quits, as everywhere).
6. Switching tab keeps each tab's stack; closing a tab drops its stack.

## 5. Tabs

`Tab`/`Shift-Tab` (or `g t`/`g T`) next and previous, wrapping around; `Alt-1` to `Alt-9` go to a tab by number (a missing number does nothing); `<space>c` closes (the right neighbour becomes active); `<space>t` opens a new one. State (view, layout, selection, scroll, issue stack) is per tab. Tabs are not saved across runs until the cache issue stores them.

## 6. Refresh, offline, rate limits

- `<space>r` reloads the active tab (or the open issue). Background refresh uses the same path and shows `STALE` only when it fails or the data is older than the refresh interval.
- Offline: the cached data stays, the banner shows the time of the data and the next retry (exponential back-off, capped at 60 s); `<space>r` retries at once. A reply ends the state.
- Rate limited: the banner counts the `Retry-After` seconds down; requests wait, nothing is dropped, and the keys keep working on the data already shown.
- A reply that is a 401 stops all automatic retries for the profile until the token is replaced (a repeating wrong token would lock the account); the banner stays.

## 7. Switch profile with views open

`:profile switch <name>` replaces the connection; every tab, stack and cache of the old profile is dropped (`ux.md` "Profiles"). The new profile starts with no tabs, and the check line runs again. A reply that arrives for the old profile is ignored.
