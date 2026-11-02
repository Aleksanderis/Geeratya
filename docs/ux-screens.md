# UX: M1 screens

The screens of Milestone 1 (read your board), drawn at 120x36 and 40x10. The behaviour that crosses screens (opening views, following links, tabs, going offline) is in [ux-workflows.md](ux-workflows.md); keys are in [keymap.md](keymap.md) and follow the rules in [ux-keymap.md](ux-keymap.md). The sketches are plain-text frames like `--print-frame` prints; colour roles are named in the text, never in the sketch. Rows below the last drawn row are blank. `>` marks the selected row and is never the only cue: the selected row is also reversed (bold in the no-colour tier).

Every screen shares the shell frame:

| Row | Content |
|---|---|
| 0 | Tab bar (one line) |
| 1 | Network banner of the active tab: blank while no state needs one (the row is always reserved, so the body never changes height when a state starts or ends) |
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

`<space>l` (`view.layout-toggle`, or `:layout list|columns`) switches the layout of the current tab; on a source with no Columns layout the status line says "This view has no columns layout". The selected issue is kept when the layout changes; the scroll positions of the columns restart. A new board kind is a new source or layout, not new loading code. The layout and the selection belong to the tab (nothing is global).

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

- **Columns.** Key (width of the longest loaded key, at most 12), one-cell type marker, summary (takes the remaining width), status (14), assignee (16). The status text uses the colour role of its category: `StatusTodo`, `StatusProgress`, `StatusDone` (new roles, added with the list; the no-colour tier relies on the text itself, and the 40-column glyphs are `○`, `◐`, `●`, ASCII `.`, `~`, `x`). The glyph set is chosen once at start (`ConsoleCapabilities.Glyphs`): ASCII when `TERM=dumb` or the locale (`LC_ALL`, `LC_CTYPE`, `LANG`, first one set) is not UTF-8, an unset locale counting as UTF-8. In ASCII the list header separator is ` - `, the loading marker is `...`, and the status glyphs are `.`, `~`, `x`; the Unicode set is `·`, `…`, `○`, `◐`, `●`. Only the list draws them so far (tab bar, banner, status bar and overlays still use `·`/`…`/box lines). The type marker is `E` epic, `S` story, `T` task, `B` bug, `s` sub-task, `?` any other type; `E` and `s` come from the hierarchy level and the sub-task flag; level-0 types (Story, Task, Bug, identical in both project kinds) are told apart by name, anything else falls back to `?` (jira-layer.md "Hierarchy"). The marker may also carry a type colour role.
- **Header.** Column titles in the `Muted` role, then the view title and the count: `N issues` when every page is loaded, `N of about M` when the approximate count is known (`/search/approximate-count`, JQL sources only; "about" is never dropped), `N+` while the total is unknown. A board or backlog list knows its total (offset paged) and shows `N of M`.
- **Selection and scroll.** `j`/`k`/`Up`/`Down` move by one row, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` by half the viewport (at least one row), `g g`/`G`/`Home`/`End` jump. The selection keeps three rows of context (`scrolloff=3`, fewer in a very short viewport) and the scroll never leaves blank rows above the last. Selection survives a refresh or a page append by issue key; when the issue is gone the nearest row is selected. The motion is `Core/Lists/ListMotion` (pure and clamped; it takes a repeat count, but the keymap has no count prefix yet); the keys send `SelectionMoved`. The viewport height the reducer works with excludes the list's two header rows (title with count, column titles). Only the rows in the viewport are rendered, never the loaded 10 000.
- **Loading more.** Pages are fetched when the selection comes within one screen of the last loaded row. The note `Loading more…` (role `Loading`) shows in a row under the last row while the page is in flight; when the rows fill the screen it shows at the end of the header line instead. If the page fails the note becomes `Could not load more: <reason>` and the loaded rows stay; moving does not retry (a held key never hammers a failing Jira), an explicit retry does: `<space>r` retries the failed page (the loaded rows and the position stay). `Enter` is the open key, so it does not retry. The count is `N+` while more pages exist and `N issues` once all are loaded (`N of about M` arrives with the approximate count).
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
 [1 Board: GTM board]

GTM board · 16 issues
┌─ To Do 5 ─────────┬─ In Progress 4 ───┬─ Backlog 4 ───────┬─ Done 3 ─────────┐
│ ┏━━━━━━━━━━━━━━━┓ │ ┌───────────────┐ │ ┌───────────────┐ │ ┌──────────────┐ │
│ ┃ Epic GTM-9    ┃ │ │ Epic GTM-10   │ │ │ Task GTM-21   │ │ │ Story GTM-13 │ │
│ ┃ Seed:         ┃ │ │ Seed:         │ │ │ Backlog:      │ │ │ Export       │ │
│ ┃ Onboarding f… ┃ │ │ Reporting     │ │ │ dark mode     │ │ │ report as C… │ │
│ ┗━━━━━━━━━━━━━━━┛ │ └───────────────┘ │ └───────────────┘ │ └──────────────┘ │
│ ┌───────────────┐ │ ┌───────────────┐ │ ┌───────────────┐ │ ┌──────────────┐ │
│ │ Story GTM-12  │ │ │ Story GTM-11  │ │ │ Story GTM-22  │ │ │ Task GTM-16  │ │
│ │ Welcome       │ │ │ Sign up with  │ │ │ Backlog:      │ │ │ Write the    │ │
│ │ screen        │ │ │ email         │ │ │ keyboard sho… │ │ │ setup guide  │ │
│ └───────────────┘ │ └───────────────┘ │ └───────────────┘ │ └──────────────┘ │
│ ┌───────────────┐ │ ┌───────────────┐ │ ┌───────────────┐ │ ┌──────────────┐ │
│ │ Task GTM-14   │ │ │ Task GTM-15   │ │ │ Bug GTM-23    │ │ │ Subt… GTM-19 │ │
│ │ Review        │ │ │ Pick a chart  │ │ │ Backlog:      │ │ │ Build the    │ │
│ │ onboarding c… │ │ │ library       │ │ │ tooltip clip… │ │ │ form         │ │
│ └───────────────┘ │ └───────────────┘ │ └───────────────┘ │ └──────────────┘ │
│                   │                   │                   │                  │
│                   │                   │                   │                  │
│                   │                   │                   │                  │
└───────────────────┴───────────────────┴───────────────────┴──────────────────┘
 NORMAL                                                ? help  : command  q quit
```

The sketch is the 80x24 snapshot of the team-managed dev board (`Backlog` third); the cursor is on the first card.

- **Frames.** Every column is a framed box: the board is drawn as one grid with box-drawing lines (`┌ ┬ ┐ │ └ ┴ ┘ ─`, `Muted`), so the columns read as columns even when empty. The name and count sit in the top border (` Name n `); the last row of the body closes the frames. The selected column's title is in `Heading` (bold), the others in `Muted`. The count is the number of loaded cards; while pages are still loading it reads `Name n+`. The view title line shows `N issues` once all pages are loaded. The frame uses the same box lines as the tab bar and overlays (not part of the ASCII glyph set).
- **Cards.** Each card is a bordered box with a one-cell margin inside its column, five rows: top border, the issue type's name and the key (`Story GTM-9`) with the assignee's initials (first letters of the first and last name) at the right edge, the name cut with `…` when the line is too narrow and replaced by the one-cell marker when even 4 cells of name do not fit (a legend would only repeat what the name says), the summary wrapped to two lines (ellipsis on the second), bottom border. Cards touch (no blank row). The selected card has a heavy border (`┏ ━ ┓ ┃ ┗ ┛`) and the `Selected` role on all its lines, so it stands out without colour; only the selected column shows a selected card. When the card area is shorter than 10 rows (a body under 12 rows) cards collapse to one line `marker key summary`, without a box, the selected one prefixed with `>` (second sketch below). A column too narrow for a box (under 8 cells inside the margin) shows no card text. Cards are drawn only for the rows in the viewport, never for all loaded issues (`Core/Boards/BoardCards` holds the measures); rows under the last whole card stay empty rather than showing half a card.
- **Movement.** `h`/`l`/`Left`/`Right` change column and keep the row index, clamped to the last card of the target column; the index aimed at is the one last chosen with `j`/`k`, so passing a short or empty column does not lose the row (an empty column is selectable and shows `no issues` in `Muted`). `0` and `$` jump to the first and last column. `j`/`k`, `g g`, `G`, `Ctrl-d`, `Ctrl-u` act inside the column; each column scrolls on its own and keeps three cards of context (`scrolloff`). `Enter` opens the selected card in the issue detail (#84) and does nothing on an empty column. After a refresh the selection stays on its issue key inside its column. The motion is `Core/Boards/BoardMotion` (pure).
- **More columns than width.** Each column is at least 20 cells wide, and below 60 columns exactly one column is drawn. When the columns do not fit, only the columns that fit are drawn, centred on the selected one, and the title line shows the number of hidden columns right-aligned: `‹2` on the left, `1›` on the right. With one column drawn the header also names the neighbours, with `‹` and `›` where more lie beyond (the 40-column sketch below).
- **Issues mapped to no column.** A status the board configuration does not map to a column would be invisible in Jira's own board. Geeratya adds a last column `Unmapped` only when at least one loaded issue has such a status, so no issue is hidden (the dev site never produced an unmapped status, so the implementing issue verifies what the board endpoint returns for one).
- **Sub-tasks and epics** are cards like any other issue (the board endpoint returns them in their status column). The type marker tells them apart. No swimlanes in M1.
- **Order and limits.** Cards come in the order the board endpoint returns (rank). Pages load in the background until the total is reached or the per-tab row cap (`ViewPaging.MaxRowsPerTab`, 10 000; `architecture.md`) is hit.

At 40x10 (body of six rows, one-line cards, one column at a time, the neighbours named in the frame):

```text
 [1 Board: GCM board]

GCM board · 24 issues                 3›
┌─Backlog 12  To Do 5 ›────────────────┐
│>T GCM-1 Survey task one              │
│ s GCM-6 Survey4 subtask              │
│ T GCM-4 Survey task four             │
│ S GCM-3 Survey story three with unic…│
└──────────────────────────────────────┘
 NORMAL        ? help  : command  q quit
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
 OFFLINE  Cannot reach example.atlassian.net. Retrying in 8 s, <space>r retries now. Data is 23 min old.
 RATE LIMITED  Waiting for the Jira rate limit. Retrying in 25 s.
 STALE  Data is 23 min old. Refreshing…
 ERROR  Jira rejected the token (401). Replace it: :profile remove work, then :profile add.
```

Ages are relative (`45 s`, `23 min`, `5 h`, `3 d`), not clock times, so the text does not depend on the time zone. The part to act on comes before the age, so a line that is cut loses the age first. A 401 is a site-wide state: every tab shows it. Other errors show Jira's own message (`ERROR  Not found, or you have no access to it.`, `ERROR  Jira returned an error (503). <space>r tries again.`).

At 40 columns:

```text
 OFFLINE  Data 23 min old
 RATE LIMITED  Retrying in 25 s
 STALE  23 min old, refreshing…
 ERROR  Token rejected. See :profile
```

| State | When | Banner and indicator | Role |
|---|---|---|---|
| Loading | a request of the user lane is in flight (a refresh by the clock is not announced) | no banner; the status bar `loading` | `Loading` |
| Offline | transport failure or timeout | `OFFLINE`, cached data kept, age of the data, next automatic retry | `Offline` |
| Rate limited | `429` with `Retry-After` | `RATE LIMITED`, wait in seconds counting down (honours the header, `RateLimitWait`) | `RateLimited` |
| Stale | cached rows shown while the refresh runs (or has not started) | `STALE`, age of the data, `Refreshing…` while a load runs | `Stale` |
| Error | the request got an error reply | `ERROR`, the message below | `Error` |

The status bar mirrors the state in its text (`offline`, `rate limited 25s`, `stale`, `error`, `loading`) before the connection line, so it stays visible when the banner is cut; a notice or pending keys still win over it. Wide terminals (60 columns and up) get the full wording, narrower ones the short one. The no-colour tier relies on the words and the bold or underline of the roles. Precedence when several apply: error (rejected token), rate limited, offline, other errors, stale, loading. The banner counts down from the clock the host passes in (the host redraws on the one-second tick only while a banner counts time), so a state needs no command of its own to tick. The tab bar marks a failed tab with `!`. Retry is `<space>r` (user lane); the clock retries by itself with the backoff of `architecture.md` ("Cache and refresh"); the first reply after a failure clears offline and rejected-token states.

### Errors

Wording by status (jira-layer.md "Errors"); the body of an HTML or plain-text reply is never shown raw:

| Reply | Text |
|---|---|
| 401 | `Jira rejected the token for <email> at <host> (401). Replace it: :profile remove <name>, then :profile add.` (a wrong email and a wrong token look the same) |
| 403, 404 | `Not found or no access: <what>` (Jira does not tell them apart) |
| 400 on a JQL source | `Query error: <Jira's message>` with the line and character; the JQL stays in the tab so it can be fixed with `:jql` |
| 429 | rate-limited state, not an error |
| 5xx | `Jira returned an error (<status>). <space>r tries again.` |
| network, timeout | `Cannot reach <host>.` / `<host> did not answer in time.` (offline state) |

## Source picker overlay

`<space>t` (and `:tab new`) opens a centred box over the tab. It is a filter prompt in Insert mode: printable keys are text, `Up`/`Down` or `Ctrl-p`/`Ctrl-n` choose, `Enter` opens the choice in a new tab, `Esc` closes. Rows come in groups: Boards (two rows per board, `Board` and `Backlog`), Filters (saved filters: favourites and the caller's), and Query, the last row, which offers the typed text as JQL and is always present when the text is not empty; text that is not a valid JQL clause (no operator, like `gcm`) is sent as `text ~ "gcm"`, anything else as typed. Typing filters boards and filters by case-insensitive substring; the first matching row is highlighted, or the Query row when nothing matches. Boards load in pages up to 20 pages (1000 boards), more are not offered; narrow with `:jql`. The lists load when the picker first opens and are cached, so reopening it does not reload (the lists live in memory for the session, not on disk; after a cold start offline the groups show the error); an empty cache shows `Loading…` in each group.

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
| `:browse [KEY]` | open the issue in the browser (`<space>o` handler): `KEY` is upper-cased and must be a valid issue key; without it, the selected row. Status line: `Opened KEY in the browser` or the launcher failure |
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
