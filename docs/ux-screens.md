# UX: M1 screens

The screens of Milestone 1 (read your board), drawn at 120x36 and 40x10. The behaviour that crosses screens (opening views, following links, tabs, going offline) is in [ux-workflows.md](ux-workflows.md); keys are in [keymap.md](keymap.md) and follow the rules in [ux-keymap.md](ux-keymap.md). The sketches are plain-text frames like `--print-frame` prints; colour roles are named in the text, never in the sketch. Rows below the last drawn row are blank. `>` marks the selected row and is never the only cue: the selected row is also reversed (bold in the no-colour tier).

Every screen shares the shell frame:

| Row | Content |
|---|---|
| 0 | Tab bar (one line) |
| 1 (optional) | Network banner, present only while a state needs one |
| next | View header: title and count |
| body | The view's layout |
| last | Status bar (mode badge, text, right-aligned hints), see `ux.md` |

The body height is the terminal height minus these rows. All widths go through `TextWidth`: a wide character is never split, a cut cell is padded with a space, and truncated text ends with `…`.

## View model

A view is a source plus a layout (`CLAUDE.md`). M1 sources and their default layouts:

| Source | Opens in | Layouts available |
|---|---|---|
| Board | Columns | Columns, List |
| Backlog | List | List |
| JQL | List | List |
| Saved filter | List | List |

`<space>l` (`view.layout-toggle`, or `:layout list|columns`) switches the layout of the current tab; on a source with no Columns layout the status line says "This view has no columns layout". Selection and scroll are kept per layout. A new board kind is a new source or layout, not new loading code. The layout and the selection belong to the tab (nothing is global).

## List layout

Rows: selection mark, key, type marker, summary, status, assignee.

```text
 1 Board: GCM board  2 Backlog: GCM board  [3 JQL: assignee = currentUser() AND resolution = Unresolved]  4 Filter: Mine
My open issues · 10 of about 340
  Key      T Summary                                                                     Status         Assignee
  GCM-12   T Fix login redirect after token refresh                                      In Progress    Jane Doe
  GCM-14   B Crash when the board has no columns                                         To Do          Ola Nordmann
> GCM-15   S As a user I can filter by label 日本語ラベル                                To Do          -
  GCM-17   E Reporting overhaul                                                          In Progress    Jane Doe
  GCM-18   s Write the CSV exporter                                                      Done           Ola Nordmann
  GCM-21   T Upgrade the build image                                                     Backlog        -
  GCM-22   B Tooltip overlaps the header on small screens                                To Do          Jane Doe
  GCM-23   T Document the release checklist                                              Done           Ola Nordmann
  GCM-25   S As an admin I can see who changed a setting                                 In Progress    Jane Doe
  GCM-26   T Remove the legacy export flag                                               To Do          -
  Loading more…





















 NORMAL  j/k move  Enter open  <space>r refresh  ? help
```

- **Columns.** Key (width of the longest loaded key, at most 12), one-cell type marker, summary (takes the remaining width), status (14), assignee (16). The status text uses the colour role of its category: `StatusTodo`, `StatusProgress`, `StatusDone` (new roles, added with the list; the no-colour tier relies on the text itself, and the 40-column glyphs are `○`, `◐`, `●`, ASCII `.`, `~`, `x`). The type marker is `E` epic, `S` story, `T` task, `B` bug, `s` sub-task, `?` any other type; `E` and `s` come from the hierarchy level and the sub-task flag; level-0 types (Story, Task, Bug, identical in both project kinds) are told apart by name, anything else falls back to `?` (jira-layer.md "Hierarchy"). The marker may also carry a type colour role.
- **Header.** Column titles in the `Muted` role, then the view title and the count: `N issues` when every page is loaded, `N of about M` when the approximate count is known (`/search/approximate-count`, JQL sources only; "about" is never dropped), `N+` while the total is unknown. A board or backlog list knows its total (offset paged) and shows `N of M`.
- **Selection and scroll.** `j`/`k`/`Up`/`Down` move by one row, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` by half the viewport (at least one row), `g g`/`G`/`Home`/`End` jump. The selection keeps three rows of context (`scrolloff=3`, fewer in a very short viewport) and the scroll never leaves blank rows above the last. Selection survives a refresh or a page append by issue key; when the issue is gone the nearest row is selected. The motion is `Core/Lists/ListMotion` (pure and clamped; it takes a repeat count, but the keymap has no count prefix yet); the keys send `SelectionMoved`. The viewport height the reducer works with excludes the list's two header rows (title with count, column titles). Only the rows in the viewport are rendered, never the loaded 10 000.
- **Loading more.** Pages are fetched when the selection comes within one screen of the last loaded row. The note `Loading more…` (role `Loading`) shows in a row under the last row while the page is in flight; when the rows fill the screen it shows at the end of the header line instead. If the page fails the note becomes `Could not load more: <reason>` and the loaded rows stay; moving does not retry (a held key never hammers a failing Jira), an explicit retry does (wired with refresh and retry, #89). The count is `N+` while more pages exist and `N issues` once all are loaded (`N of about M` arrives with the approximate count).
- **Empty.** The body shows `No issues match.` centred on the first line, and for a JQL source the query below it in `Muted`. A board with no issues says `The board has no issues.`
- **Error body** (first load failed, nothing cached): the error text (see "Errors" below) and `<space>r to retry`. If a cache exists the old rows stay and the banner carries the error instead.
- **Narrow terminals.** Columns drop in this order as the width shrinks: assignee (below 100 columns), then status shrinks to the category glyph (below 56), then the key never drops. At 80 columns:

```text
 1 Board: GCM board  2 Backlog  [3 JQL: assignee = currentUser() AND resolution…
My open issues · 10 of about 340
  Key      T Summary                                              Status
> GCM-12   T Fix login redirect after token refresh               In Progress
  GCM-14   B Crash when the board has no columns                  To Do
  GCM-15   S As a user I can filter by label 日本語ラベル         To Do
  GCM-17   E Reporting overhaul                                   In Progress
  GCM-18   s Write the CSV exporter                               Done
  GCM-21   T Upgrade the build image                              Backlog
  GCM-22   B Tooltip overlaps the header on small screens         To Do
  GCM-23   T Document the release checklist                       Done


 NORMAL  j/k move  Enter open  <space>r refresh  ? help
```

  At 40 columns (and 10 rows):

```text
 ‹2 [3 JQL: assignee = curr…] 4 Filter……
My open issues · 10+
  GCM-12   T Fix login redirect after… ◐
> GCM-14   B Crash when the board has… ○
  GCM-15   S As a user I can filter b… ○
  GCM-17   E Reporting overhaul        ◐
  GCM-18   s Write the CSV exporter    ●
  GCM-21   T Upgrade the build image   ○
  GCM-22   B Tooltip overlaps the hea… ○
 NORMAL  j/k Enter <space>r ?
```

  Below 40 columns the type marker drops too. The column-title row drops below 12 rows (as in this 40x10 sketch), so at 10 rows seven issues show.

## Columns layout (Kanban)

One column per board column, in the order of the board configuration. Never assume the first is the backlog: on the team-managed dev board `Backlog` is third (jira-layer.md "Boards"). The Backlog column is shown like any other column.

```text
 [1 Board: GCM board]  2 Backlog: GCM board  3 JQL: assignee = currentUser() AND resolution = Unresolved  4 Filter: Mine
GCM board · 11 issues
 To Do 3                [In Progress 3]          Backlog 2               Done 2                  Unmapped 1
 B GCM-14               >T GCM-12                T GCM-21                s GCM-18                T GCM-30
 Crash when the board    Fix login redirect      Upgrade the build       Write the CSV exporter  Status removed from
 has no columns          after token refresh     image                                           the workflow

 S GCM-15                E GCM-17                T GCM-26                T GCM-23
 As a user I can filter  Reporting overhaul      Remove the legacy       Document the release
 by label 日本語ラベル                           export flag             checklist

 B GCM-22                S GCM-25
 Tooltip overlaps the    As an admin I can see
 header on small…        who changed a setting





















 NORMAL  h/l column  j/k card  Enter open  <space>l list  ? help
```

- **Header.** `Name count`; the selected column is in brackets (and bold). The count is the number of loaded cards; while pages are still loading it reads `Name n+`. The view title line shows `N issues` once all pages are loaded.
- **Cards.** Three lines: type marker and key, then the summary wrapped to two lines (ellipsis on the second), then one blank line. When the body is shorter than 12 rows cards collapse to one line `key summary` (second sketch below). The selected card carries `>`.
- **Movement.** `h`/`l`/`Left`/`Right` change column and keep the row index, clamped to the last card of the target column (an empty column is selectable and shows `no issues` in `Muted`). `0` and `$` jump to the first and last column. `j`/`k`, `g g`, `G`, `Ctrl-d`, `Ctrl-u` act inside the column; each column scrolls on its own, remembering its selection.
- **More columns than width.** Each column is at least 20 cells wide. When the columns do not fit, only the columns that fit are drawn, centred on the selected one, and the header shows `‹` or `›` with the number of columns hidden on that side. At 40 columns exactly one column is drawn at full width, with the neighbouring names in the header (below).
- **Issues mapped to no column.** A status the board configuration does not map to a column would be invisible in Jira's own board. Geeratya adds a last column `Unmapped` only when at least one loaded issue has such a status, so no issue is hidden (shown in the sketch; the dev site never produced an unmapped status, so the implementing issue verifies what the board endpoint returns for one).
- **Sub-tasks and epics** are cards like any other issue (the board endpoint returns them in their status column). The type marker tells them apart. No swimlanes in M1.
- **Order and limits.** Cards come in the order the board endpoint returns (rank). Pages load in the background until the total is reached or 500 issues are loaded; past the cap the banner says `Showing the first 500 of N issues. Narrow the board filter or use :jql.`

At 40x10 (body of five rows, one-line cards, one column at a time):

```text
 [1 Board: GCM board] 2 Backlog 3 JQL… ›
GCM board · 11 issues
‹ To Do 3   [In Progress 3]  Backlog ›
>GCM-12 Fix login redirect after token …
 GCM-17 Reporting overhaul
 GCM-25 As an admin I can see who chang…



 NORMAL  h/l Enter <space>l ?
```

## Issue detail

Enter on a list row or card opens the issue in the same tab; the tab's view is kept underneath as a stack (`Esc` pops, see workflow 4). A breadcrumb replaces the view title: `GCM board › GCM-12`.

```text
 [1 Board: GCM board]  2 Backlog: GCM board  3 JQL: assignee = currentUser() AND resolution = Unres…
GCM board › GCM-12   Task   Fix login redirect after token refresh
Status: In Progress   Priority: High   Assignee: Jane Doe   Reporter: Ola Nordmann
Labels: auth, bug-bash   Parent: GCM-17 Reporting overhaul   Updated: 2 h ago
────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Description
  After the token refresh the user lands on /login instead of the page they came from.
  - reproduce: let the session expire, then open a deep link
  - expected: the deep link opens after the refresh
  [image] screenshot.png

Sub-tasks (2)
> s GCM-18 Write the CSV exporter                                      Done          Ola Nordmann
  s GCM-19 Cover the redirect with a test                              To Do         -
Linked issues (1)
  blocks    B GCM-14 Crash when the board has no columns                To Do
Remote links (1)
  Confluence  Login flow design  (opens in the browser)
Attachments (1)
  screenshot.png  image/png  48 KB  (opens the issue page in the browser)
Comments (3 of 7)
  Load 4 older comments
  Ola Nordmann · 3 d ago
    Can you check the refresh path?
  Jane Doe · 1 d ago
    Found it, the redirect target is dropped.









 NORMAL  j/k scroll  ] ] section  Enter open  Esc back  <space>o browser  ? help
```

- **Header.** Key, type, summary (wrapped, bold), then status (category role), priority, assignee, reporter, labels, parent (`KEY summary`, opens with `<space>p`), updated time. Missing values are omitted, not shown as empty. Below 80 columns the fields go one per line.
- **Description.** The ADF rendered as formatted text (`docs/jira-layer.md` "ADF in real data"). Media nodes show as `[image] name` or `[image]` when no name can be matched; nothing is rendered as an image. A missing description shows nothing under the heading except `No description.` in `Muted`.
- **Sections, in order:** Sub-tasks, Epic children (epics only, loaded with `parent = KEY` when the issue is opened), Linked issues (direction text from the link type, `blocks`, `is blocked by`, ...), Remote links (Confluence and others: application and title), Attachments (name, type, size; the row opens the issue page in the browser because the API download URL needs the auth header, jira-layer.md "Links"), Comments. A section with no entries is not drawn. Headings carry the count, `Name (n)`.
- **Comments.** Oldest of the loaded set first, newest last, so the end of the screen is the latest. The first fetch is the newest 20 (`orderBy=-created`, shown reversed); when older ones exist the first row of the section is `Load n older comments`, and `Enter` on it loads the next page above. (The implementing issue verifies `orderBy` on the dev site.) A comment shows author, relative time and the rendered ADF body.
- **Cursor and scrolling.** The body is a vertically scrolling document with a cursor row. `j`/`k`/`Ctrl-d`/`Ctrl-u`/`g g`/`G` move it. `] ]` and `[ [` jump to the next and previous section heading (vim's section motions). Rows that can be opened (a sub-task, child, linked issue, remote link, attachment, `Load n older comments`) are marked with the selection mark when the cursor is on them. `Enter` on an issue row pushes that issue on the stack; on a remote link or attachment it opens the browser (only `http` and `https` URLs are opened; any other scheme just shows the URL in the status line, since the URL is free text from Jira); on the load row it loads.
- **Keys.** `Esc` goes back one level (pops the stack; at the view it does nothing more). `<space>o` opens the issue in the browser. `<space>p` opens the parent. `<space>r` reloads the issue.
- **Loading.** The cached issue shows at once with the banner state `STALE` until the fresh copy arrives; with no cache the header shows the key from the list row and `Loading…` under it. Related data (children, comments, remote links) loads separately, each section showing `Loading…` until it arrives or an error line with `<space>r`.
- **Not found.** 404 and 403 give the same text, `Not found or no access: GCM-99`, with `Esc` to go back.

At 40x10:

```text
 [1 Board: GCM board] 2 Backlog 3 JQL…›
GCM board › GCM-12 Task
Fix login redirect after token…
In Progress · High
Jane Doe (reporter Ola Nord…)
Parent GCM-17
Description
  After the token refresh the…

 NORMAL  j/k Esc back <space>o ?
```

## Tab bar

One line at the top, titles separated by two spaces, each prefixed with its number (`Alt-1` to `Alt-9` go there). The active tab is in brackets and bold (reversed in the no-colour tier). Titles: `Board: <name>`, `Backlog: <name>`, `Filter: <name>`, `JQL: <query>`; an inactive title is cut to 24 cells, the active one to 40 (never below 8); the sketches in this document that show longer titles are abbreviated only for the width of the row, the rule is this one. A tab shows `…` after its title while loading and `!` when its last load failed. An issue opened inside a tab does not rename the tab.

Overflow: when the titles do not fit, the window of tabs slides so the active one is fully visible, `‹n` (tabs hidden on the left) and `+n ›` (right) mark the cut ends, and inactive titles shrink towards 8 cells before any is hidden. At 120 and 40 columns:

```text
 ‹3  [6 JQL: project = GCM AND labels = auth]  7 Backlog: GCM board  8 Filter: Mine  9 Board: GTM board  +2 ›
```

```text
 ‹3  [4 JQL: project = GCM A…]  +2 ›
```

The window grows around the active tab, right neighbour first, as long as the line fits; when only the active tab fits, its title is cut below 40 cells (never below 8). The bar always takes the first row, blank when no tab is open, so the body height and the list viewport do not change when the first tab opens or the last one closes. Loading and failed marks follow the tab's last request. Next and previous wrap around.

Keys: `Tab`/`Shift-Tab` (also `g t`/`g T`) next and previous, `Alt-1` to `Alt-9` go to a tab, `<space>t` opens the source picker for a new tab, `<space>c` closes the tab (the right neighbour becomes active, the left one if it was last). Closing the last tab leaves the empty shell with its hint line. There is no tab limit; the numbered keys reach the first nine.

## Connection check and network states

Implemented (#83): the check line is shown on the start screen under the profile line and in the status line whenever no notice or pending keys occupy it (text only, no `Loading` role yet); `:profile check` runs it again. Connection check line: while the first request of a profile is in flight the text reads `Checking example.atlassian.net...`; on success `Connected as <display name> on <host>`, and the body shows what is cached. The first frame is drawn before any reply. On failure the banner states the reason (below).

States, as a one-line banner under the tab bar (highest priority wins: error, offline, rate limited, stale):

```text
 OFFLINE  Cannot reach example.atlassian.net. Showing data from 12:03. Retrying in 8 s, <space>r retries now.
 RATE LIMITED  Jira asked us to wait. Retrying in 25 s.
 STALE  Showing data from 11:40 (23 min old). Refreshing…
 ERROR  Jira rejected the token for me@example.com (401). Replace it: :profile remove NAME, then :profile add.
```

At 40 columns:

```text
 OFFLINE  Showing data from 12:03
 RATE LIMITED  Retrying in 25 s
 STALE  23 min old, refreshing…
 ERROR  Token rejected (401). See…
```

| State | When | Banner and indicator | Role |
|---|---|---|---|
| Loading | a request is in flight | no banner; the tab shows `…`, the status bar `loading` | `Loading` |
| Offline | transport failure or timeout | `OFFLINE`, cached data kept, last good time, next automatic retry | `Offline` |
| Rate limited | `429` with `Retry-After` | `RATE LIMITED`, wait in seconds counting down (honours the header, `RateLimitWait`) | `RateLimited` |
| Stale | cached data older than the refresh interval, refresh pending or failed | `STALE`, age of the data | `Stale` |
| Error | the request got an error reply | `ERROR`, the message below | `Error` |

The status bar mirrors the state in its text (`offline`, `rate limited 25s`, `stale`, `error`) so it stays visible when the banner is cut. The no-colour tier relies on the words and the bold or underline of the roles.

### Errors

Wording by status (jira-layer.md "Errors"); the body of an HTML or plain-text reply is never shown raw:

| Reply | Text |
|---|---|
| 401 | `Jira rejected the token for <email> at <host> (401). Replace it: :profile remove <name>, then :profile add.` (a wrong email and a wrong token look the same) |
| 403, 404 | `Not found or no access: <what>` (Jira does not tell them apart) |
| 400 on a JQL source | `Query error: <Jira's message>` with the line and character; the JQL stays in the tab so it can be fixed with `:jql` |
| 429 | rate-limited state, not an error |
| 5xx | `Jira returned an error (<status>). <space>r tries again.` |
| network | `Cannot reach <host>.` (offline state) |

## Source picker overlay

`<space>t` (and `:tab new`) opens a centred box over the tab. It is a filter prompt in Insert mode: printable keys are text, `Up`/`Down` or `Ctrl-p`/`Ctrl-n` choose, `Enter` opens the choice in a new tab, `Esc` closes. Rows come in groups: Boards (two rows per board, `Board` and `Backlog`), Filters (saved filters: favourites and the caller's), and Query, the last row, which offers the typed text as JQL and is always present when the text is not empty; text that is not a valid JQL clause (no operator, like `gcm`) is sent as `text ~ "gcm"`, anything else as typed. Typing filters boards and filters by case-insensitive substring; the first matching row is highlighted, or the Query row when nothing matches. The lists load when the picker first opens and are cached, so the picker works offline from cache; an empty cache shows `Loading…` in each group.

```text
 1 Board: GCM board  2 Backlog: GCM board  [3 JQL: assignee = currentUser() AND resolution = Unresolved]  4 Filter: Mine
My open issues · 10 of about 340


      ┌ Open a view ─────────────────────────────────────────────────────────────────────────────────────┐
      │ > gcm_                                                                                           │
      │ Boards                                                                                           │
      │ > Board     GCM board (kanban, GCM)                                                              │
      │   Backlog   GCM board                                                                            │
      │   Board     GTM board (simple, GTM)                                                              │
      │   Backlog   GTM board                                                                            │
      │ Filters                                                                                          │
      │   Filter    Filter for GCM board                                                                 │
      │ Query                                                                                            │
      │   JQL       gcm                                                                                  │
      │ ─────────────────────────────────────────                                                        │
      │ Up/Down choose   Enter open in a new tab   Esc close                                             │
      └──────────────────────────────────────────────────────────────────────────────────────────────────┘

















 INSERT  open a view  Up/Down choose  Enter open  Esc close
```

At 40x10:

```text
 [1 My board]

┌ Open a view ─────────────────────┐
│ > gcm_                           │
│ > Board   GCM board              │
│   Backlog GCM board              │
│   Filter  Filter for GCM bo…     │
│   JQL     gcm                    │
└──────────────────────────────────┘
 INSERT  Enter open  Esc
```

The overlay takes every key (`ux.md` "Focus model and key style"); its mode text in the status bar names the keys.

## Palette commands that open views

`:` opens the palette. All of these open a new tab, then load in the background:

| Command | Effect |
|---|---|
| `:jql <query>` | JQL list; the query is everything after the first space |
| `:filter [name]` | the saved filter whose name matches (exact, else unique prefix, case-insensitive) as a list; no name, or no unique match, opens the picker filtered to Filters with the text typed so far |
| `:board [name]` | the board as Columns; same matching and fallback to the picker (Boards) |
| `:backlog [name]` | the board's backlog as a list; same matching |
| `:tab new` | the source picker; `:tab close`, `:tab next`, `:tab prev`, `:tab <n>` are aliases of the tab keys |
| `:layout list`, `:layout columns` | switch the layout of the tab (alias of `<space>l`) |
| `:refresh` | alias of `<space>r` |
| `:browse` | alias of `<space>o` |

Without a board name and exactly one board on the site, `:board` and `:backlog` open it directly. The palette stays the stub of `ux.md` for anything else ("Unknown command: ..."). Commands appear in the generated help once they exist; completion of names is not part of M1.

## Decisions for the screens (defaults)

- The Backlog column of a board is shown like any other column.
- Attachments open the issue page in the browser (the download URL needs the auth header).
- The board opens in Columns; backlog, JQL and filters open in List; the layout can be switched per tab (boards only in M1).
- Unmapped statuses get a trailing `Unmapped` column rather than being hidden.
- Palette view commands and the picker open a new tab; they never replace the current tab's view.
- Sub-tasks and epics on a board are cards like any issue.
