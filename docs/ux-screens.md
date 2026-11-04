# UX: M1 screens

The screens of Milestone 1 (read your board), drawn at 120x36 and 40x10. The behaviour that crosses screens (opening views, following links, tabs, going offline) is in [ux-workflows.md](ux-workflows.md); keys are in [keymap.md](keymap.md) and follow the rules in [ux-keymap.md](ux-keymap.md). The sketches are plain-text frames like `--print-frame` prints; colour roles are named in the text, never in the sketch. Rows below the last drawn row are blank. **The sketches below predate the shared frame (#524, [ux-look.md](ux-look.md) section 2):** bodies are drawn as one rounded frame with title chips in the top edge, the title and count of a list or backlog is the left chip of that edge (the right chip carries the count, options and loading note), the board's title line and the detail's breadcrumb and ruler are in the status bar. The text of each section says what changed; the sketches themselves are refreshed from the snapshots in a later pass. `>` marks the selected row and is never the only cue: the selected row is also reversed (bold in the no-colour tier).

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





















 NORMAL  j/k move  Enter open  <space>o browser  ? help  : commands  q quit
```

- **Columns.** Key (width of the longest loaded key, at most 12), one-cell type marker, summary (takes the remaining width), status (14), assignee (16). The status text uses the colour role of its category: `StatusTodo`, `StatusProgress`, `StatusDone` (new roles, added with the list; the no-colour tier relies on the text itself, and the 40-column glyphs are `○`, `◐`, `●`, ASCII `.`, `~`, `x`). The glyph set is chosen once at start (`ConsoleCapabilities.Glyphs`): ASCII on a classic console (Linux `TERM=linux`, Windows console host; `docs/architecture.md`, "Cross-platform"), when `TERM=dumb` or the locale (`LC_ALL`, `LC_CTYPE`, `LANG`, first one set) is not UTF-8, an unset locale counting as UTF-8. In ASCII the list header separator is ` - `, the loading marker is `...`, and the status glyphs are `.`, `~`, `x`; the Unicode set is `·`, `…`, `○`, `◐`, `●`. The list and the detail panel borders draw them (with the ASCII set the finished frame is passed through `Glyphs.ToAscii`, which swaps every shell-drawn character one cell for one cell: box lines to `+ - |`, `‹ › ↑ ▾ … ·` to `< > ^ v . -`, so the tab bar, banner, status bar, overlays and board are covered and widths do not change; the same characters inside issue text are swapped too, and a cut text ends with `.` instead of `…` to keep one cell; in the detail, the `›` of the breadcrumb becomes `>`). The type marker is `E` epic, `S` story, `T` task, `B` bug, `s` sub-task, `?` any other type; `E` and `s` come from the hierarchy level and the sub-task flag; level-0 types (Story, Task, Bug, identical in both project kinds) are told apart by name, anything else falls back to `?` (jira-layer.md "Hierarchy"). The marker may also carry a type colour role.
- **Header.** The top edge of the frame holds the view title as a chip and, as a right-hand chip, the count; the column titles in the `Muted` role are the first row inside. The count reads: `N issues` when every page is loaded, `N of about M` when the approximate count is known (`/search/approximate-count`, JQL sources only; "about" is never dropped), `N+` while the total is unknown. A board or backlog list knows its total (offset paged) and shows `N of M`.
- **Selection and scroll.** `j`/`k`/`Up`/`Down` move by one row, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` by half the viewport (at least one row), `g g`/`G`/`Home`/`End` jump. The selection keeps three rows of context (`scrolloff=3`, fewer in a very short viewport) and the scroll never leaves blank rows above the last. Selection survives a refresh or a page append by issue key; when the issue is gone the nearest row is selected. The motion is `Core/Lists/ListMotion` (pure and clamped; it takes a repeat count, but the keymap has no count prefix yet); the keys send `SelectionMoved`. The viewport height the reducer works with excludes the list's two header rows (title with count, column titles). Only the rows in the viewport are rendered, never the loaded 10 000.
- **Loading more.** Pages are fetched when the selection comes within one screen of the last loaded row. The note `Loading more…` (role `Loading`) shows in a row under the last row while the page is in flight; when the rows fill the screen it shows at the end of the header line instead. If the page fails the note becomes `Could not load more: <reason>` and the loaded rows stay; moving does not retry (a held key never hammers a failing Jira), an explicit retry does: `<space>r` retries the failed page (the loaded rows and the position stay). `Enter` is the open key, so it does not retry. The count is `N+` while more pages exist and `N issues` once all are loaded; with a known total it reads `N of about M` (JQL sources: the approximate count is asked after each first-page load, refreshes included, when more pages exist; a failed call leaves `N+`, as does a count below the loaded rows) or `N of M` (board and backlog, from the paged reply; it shows while pages are still loading, then `N issues`). A list cut at the row cap keeps the `N of M` / `N+` form.
- **Empty.** The body shows `No issues match.` padded one column and left aligned on the first line (loading, empty and error bodies share the `StateBody` look, #579), and for a JQL source the query below it in `Muted`. A board with no issues says `The board has no issues.`; a Scrum board (which lists only the open sprints) says `No open sprint on this board.` with a muted hint naming the backlog (`:backlog`) and the Sprint view (#435).
- **Error body** (first load failed, nothing cached): the error text (see "Errors" below) and `<space>r to retry`. If a cache exists the old rows stay and the banner carries the error instead.
- **Optional columns (#179).** After the assignee the list adds `Sprint` (16, the current sprint: active, else future, else last closed), `Pts` (5, the estimate, right-aligned, whole numbers without decimals) and `Epic` (14, the epic key), in that order of priority. A column shows only while a row in view has a value for it, and only while the summary keeps 30 cells, so a Kanban list and narrow terminals look as before. There is no per-view switch yet (view options are M6).
- **Narrow terminals.** Columns drop in this order as the width shrinks: assignee (below 100 columns), then status shrinks to the category glyph (below 56), then the key never drops. At 80 columns:

```text
 1 JQL My open issues … x   +                                     <space>g jump

╭─ My open issues ─────────────────────────────────────────────────────── 10+ ─╮
│  Key    T Summary                                              Status        │
│  GCM-12 T Fix login redirect after token refresh               In Progress   │
│  GCM-14 B Crash when the board has no columns                  To Do         │
│> GCM-15 S As a user I can filter by label 日本語ラベル         To Do         │
│  GCM-17 E Reporting overhaul with a very long summary that nev In Progress   │
│  GCM-18 s Write the CSV exporter 😀 é                          Done          │
│  GCM-21 ? Upgrade the build image                              Backlog       │
│  GCM-22 B Tooltip overlaps the header on small screens         To Do         │
│  GCM-23 T Document the release checklist                       Done          │
│  GCM-25 S As an admin I can see who changed a setting          In Progress   │
│  GCM-26 T Remove the legacy export flag                        To Do         │
│Loading more…                                                                 │
│                                                                              │
│                                                                              │
╰──────────────────────────────────────────────────────────────────────────────╯
 NORMAL  loading                j/k move  Enter open  ? help  : commands  q quit
```

  At 40 columns (and 10 rows):

```text
 1 JQL My open issues …   +

╭─ My open issues ─ 10+ · Loading mo… ─╮
│  Key    T Summary                   S│
│  GCM-12 T Fix login redirect after  ◐│
│  GCM-14 B Crash when the board has  ○│
│> GCM-15 S As a user I can filter by ○│
│  GCM-17 E Reporting overhaul with a ◐│
╰──────────────────────────────────────╯
 NORMAL  loading
```

  Below 40 columns the type marker drops too. The column-title row drops below 12 rows (as in this 40x10 sketch), so at 10 rows seven issues show.

## Scrum backlog (sections)

The backlog of a Scrum board (`scrum` boards from the picker and `:backlog`; a team-managed board reports `simple`; opening its backlog runs the sprint probe and a Scrum answer turns the tab into the sections, a Kanban answer keeps the plain backlog; a failed probe is retried on the next refresh and the tab swaps to the sections then) is one list in sections: the active sprints, the future sprints by id, then the backlog (`ViewSourceKind.ScrumBacklog`, layout List; closed sprints are never loaded). Each section has a header that is the shared edge of its pane, `├─ Sprint 1 · active ── 2 Nov - 16 Nov · 8 issues · 21 pts ─┤` (a collapsed section marks its chip with `▸`; ASCII `>`); the top edge of the frame carries the source and the count and the column titles follow in the first content row (`ux-look.md` section 2 shows the first sprint's chip there instead; here it is the source, so the geometry of the list and the backlog is the same). The header carries: name, `active` for the running sprint, the dates (`no dates` for a sprint never given any), the issues loaded and the story points (the sum of the estimates, omitted when no issue has one). An expanded sprint with a goal shows `Goal: ...` under the header; a sprint without issues shows `No issues in this sprint.` so it can still be seen. While more pages are coming the counts read `N+`.

```
 1 Backlog Scrum board x   +                                      <space>g jump

╭─ Scrum board ───────────────────────────────────────────────────── 5 issues ─╮
│  Key   T Summary                                         Status         Pts  │
├─ Sprint 1 · active ────────────────────── 2 Nov - 16 Nov · 2 issues · 8 pts ─┤
│    Goal: Ship the login 日本語                                               │
│> GCS-3 S Fix login redirect after token refresh          To Do              5│
│  GCS-4 S As a user I can filter by label 日本語ラベル    To Do              3│
├─ Sprint 2 ───────────────────────────────────── no dates · 2 issues · 3 pts ─┤
│  GCS-2 S Reporting overhaul with a very long summary tha To Do              3│
│  GCS-6 S Crash when the board has no columns             To Do               │
├─ Sprint 3 ───────────────────────────────────────────── no dates · 0 issues ─┤
│    No issues in this sprint.                                                 │
├─ Backlog ──────────────────────────────────────────────── 1 issue · 1.5 pts ─┤
│  GCS-5 S Tidy the release notes                          To Do            1.5│
│                                                                              │
│                                                                              │
╰──────────────────────────────────────────────────────────────────────────────╯
 NORMAL       j/k move  Enter open  <space>o browser  ? help  : commands  q quit
```

- **Rows.** The tab keeps its rows in section order (rank order inside a section), so everything that acts on the row under the cursor (open, edit, marks, Visual) works as in any list. The sprint of a row is the one its `Sprint` field says is current: active, else future, else the backlog; the sprint list (`GET board/{id}/sprint?state=active,future`, read once per open or manual refresh) supplies dates, goal and sprints with no issue yet. When it fails the sections take their names from the issues.
- **Cursor.** `j`/`k` and the other motions step over the rows of all sections; headers are not stops. `] ]` and `[ [` jump to the first row of the next section, or of this one and then the previous (sections with no issues are skipped).
- **Collapse.** `z a` (`section.toggle`, also in the right-click menu) collapses the section under the cursor or expands it. A collapsed section shows its header only, and its first row stands in for it: the cursor stops on the header (`> + Sprint 1`), `Enter` there expands the section instead of opening an issue, and `z a` does too. Hidden rows are not reachable by the cursor but still belong to the tab; collapsing is refused in Visual mode, and a Visual range skips the hidden rows. The collapsed sections are kept per tab across reloads.
- **Scroll.** The window scrolls by display lines (headers, goal lines and notes count), and the header comes into view with the first row of its section. A click on a header or goal line places the cursor on the section's first row.
- **Limits.** The tab holds at most 10 000 rows (roadmap, open question 5); the nav key through the reducer and a frame with 10 000 rows in 21 sections takes a median 0.41 ms (p95 0.83 ms, `HotPathMeasurements`, JIT, not AOT); the sections are derived per key and per frame in one pass over the rows. The points and epic columns of the list (#179) show as everywhere, for the whole backlog so they do not jump while scrolling; the sprint column is left out because the header says it. Issues move between sections with `<space>sm` and `<space>sb` (#181; the rows regroup at once and the tab reloads); rank keys work here too (#182, see "Rank across sections" below). A Visual range or marks never act on the hidden rows of a collapsed section (a range skips them; marks name what the user marked).

**Rank across sections (#182).** `Alt-j`, `Alt-k`, `<space>kt`/`kb` and `<space>kk` work on the rows of the whole tab. When the neighbour the issue lands next to is in another section, the issue also changes section: it takes the sprint of that row (the backlog when the row is a backlog one). The status line says so (`Moving A-4 to sprint "Sprint 2", ranked below A-2`), and a set of more than one issue asks first (`... Two calls, Jira has no undo.`). It is one command and two calls: move into the sprint (`POST sprint/{id}/issue`, or `POST backlog/issue`), then rank (`PUT issue/rank`). A move that Jira refuses outright stops there and nothing changed; a move that took only some issues skips the rank, the issues that moved leave the marks and the rest stay marked. A move that worked followed by a failed rank leaves the issues in the new section; the error kind of the rank call is kept (a lost reply stays an unknown outcome, a 401 still rejects the token) and the failure text says `Moved to sprint "..." but not ranked: <reason>` and the tab reloads to show the truth. A row inside a collapsed section is never a neighbour the cursor can name: ranking on a collapsed header says to expand first. An empty sprint has no row to land next to; use `<space>sm` for it.

## Columns layout (Kanban)

A Sprint view (Scrum, #176) uses the same columns; its first line is the sprint header instead of the tab title: name, dates, days left (active sprints), issue count, points done/total (done = status category done; left out when no issue has an estimate), then the goal cut to the remaining width. Cards show their points as `5p` before the assignee initials. "The active sprint" loads the issues of the open sprints that are not future ones; with parallel active sprints the header names the first and the counts cover all. A board with no active sprint shows "No active sprint on this board." and the `:backlog` hint. No key reaches this view today (`:board` on a Scrum board without an active sprint shows the board-level "No open sprint on this board.", and the switcher lists only real sprints), so it has a unit test but no screenshot scenario (#496); it becomes reachable only if something opens an `active` Sprint source (nothing does today). The code stays for that case. `<space>sg` (`sprint.goal`, also in the view menu when the sprint has a goal) shows the full goal in a notice box titled `Sprint goal` (the sprint name, a blank line, the goal wrapped, newlines kept) that closes with `y`, `n`, `Esc` or a click; it is the Confirm dialog without the Cancel row and with nothing to run. A sprint without goal, or a tab without sprint, answers with a note instead.

**Tab list (#547).** `<space>T`, a click on the `‹n` or `+n ›` chip of a scrolled tab bar, or the palette row `tab.list` opens the same picker box titled `Open tabs`: one row per open tab (`2 JQL project = A`), the active one highlighted, the typed text filters by substring; `Enter` or a click switches to the row, `Esc` closes. No network call and no write; there is deliberately no `:tab list` word (the palette row and the key cover it).

**Sprint switcher (#177).** `<space>sv` (or the view menu) opens the source picker in its sprint scope for the board of the active tab; a tab without a board (JQL, filter) answers with a note, a Kanban board with the error "This is a Kanban board: it has no sprints." Rows are grouped Active, Future (Jira's order) and Closed (newest completion first, 10 at first; the row `N more closed sprints` adds 10 more without a request). Every sprint is loaded once per open (`board/{id}/sprint`, all states, at most 1000 sprints) and the typed text filters all groups by name. Each row shows the dates, closed ones also `closed 2 Nov`. `Enter` replaces the sprint of the tab when it is a Sprint view and otherwise opens a new Sprint tab; `Ctrl-t` always opens a new tab. A closed sprint is read only: the header shows `closed <date>` and the write actions (status, edit, assign, delete, create, rank) answer `Closed sprint: read only.`; comments stay allowed (Jira allows them).

One column per board column, in the order of the board configuration. Never assume the first is the backlog: on the team-managed dev board `Backlog` is third (jira-layer.md "Boards"). On a company-managed Kanban board whose first column is `Backlog` that column is hidden, as Jira does (those issues are in the `:backlog` view; `BoardColumns.HidesBacklogColumn`). Moving a card to the previous column stops at the first shown column. A team-managed board shows its `Backlog` column like any other.

```text
 1 Board GTM board x   +                                          <space>g jump

╭─ To Do 5 ─────────┬─ In Progress 4 ───┬─ Backlog 4 ───────┬─ Done 3 ─────────╮
│ ┏━━━━━━━━━━━━━━━┓ │ ┌───────────────┐ │ ┌───────────────┐ │ ┌─ ↑ GTM-10 … ─┐ │
│ ┃ Epic GTM-9    ┃ │ │ Epic GTM-10   │ │ │ Task GTM-21   │ │ │ Story GTM-13 │ │
│ ┃ Seed:         ┃ │ │ Seed:         │ │ │ Backlog:      │ │ │ Export       │ │
│ ┃ Onboarding f… ┃ │ │ Reporting     │ │ │ dark mode     │ │ │ report as C… │ │
│ ┣━━━━━━━━━━━━━━━┫ │ ├───────────────┤ │ └───────────────┘ │ └──────────────┘ │
│ │   Sto… GTM-12 │ │ │   Task GTM-15 │ │ ┌───────────────┐ │ ┌──────────────┐ │
│ │   Welcome     │ │ │   Pick a      │ │ │ Story GTM-22  │ │ │ Task GTM-16  │ │
│ │   screen      │ │ │   chart libr… │ │ │ Backlog:      │ │ │ Write the    │ │
│ │               │ │ │               │ │ │ keyboard sho… │ │ │ setup guide  │ │
│ ├───────────────┤ │ └───────────────┘ │ └───────────────┘ │ └──────────────┘ │
│ │   Task GTM-14 │ │ ┌─ ↑ GTM-9 Se… ─┐ │ ┌───────────────┐ │ ┌─ ↑ GTM-11 … ─┐ │
│ │   Review      │ │ │ Story GTM-11  │ │ │ Bug GTM-23    │ │ │ Subt… GTM-19 │ │
│ │   onboarding… │ │ │ Sign up with  │ │ │ Backlog:      │ │ │ Build the    │ │
│ │               │ │ │ email         │ │ │ tooltip clip… │ │ │ form         │ │
│ └───────────────┘ │ └───────────────┘ │ └───────────────┘ │ └──────────────┘ │
│                   │                   │                   │                  │
│                   │                   │                   │                  │
│                   │                   │                   │                  │
│                   │                   │                   │                  │
╰───────────────────┴───────────────────┴───────────────────┴──────────────────╯
 NORMAL  GTM board · 16 iss…    h/l column  j/k card  ? help  : commands  q quit
```

The sketch is the 80x24 snapshot of the team-managed dev board (`Backlog` third); the cursor is on the first card.

- **Frames.** The body is one rounded frame (`FrameGrid`, `ux-look.md` section 2: `╭ ╮ ╰ ╯` at the corners, `┬ ┴` where a column edge meets the frame, `│ ─`; the focused column's whole box is `FrameFocus`, the others `Border`, and chips follow `ux-look.md` section 3) divided into the columns, which share their edges, so the columns read as columns even when empty. There is no title line above the frame: the first body row is the top edge. The name and count of each column sit in it as a chip (` Name n `); the last row of the body closes the frames. The title line's content (title and count, quick filter and lane, a sprint's name, dates, days left and points and its goal last, the loading note) is the left part of the status bar now, cut to what the key hints leave (`ColumnsBodyRenderer.StatusText`). Below 20 columns or 5 rows there is no frame: the selected column is drawn as one-line cards under its title. The selected column's title is in `Heading` (bold), the others in `Muted`. The count is the number of loaded cards; while pages are still loading it reads `Name n+`. With the ASCII glyph set the frame, cards and `‹ ›` marks are drawn with `+ - |` and `< >` (`Glyphs.ToAscii`).
- **Cards.** Each card is a bordered box with a one-cell margin inside its column, five rows: top border, the issue type's name and the key (`Story GTM-9`) with the assignee's initials (first letters of the first and last name) at the right edge, the name cut with `…` when the line is too narrow; the name wins over the initials, which go first, and when 4 cells of name do not fit the one-cell marker (as in the list layout) replaces it, the summary wrapped to two lines (ellipsis on the second), bottom border. Cards touch (no blank row). The selected card has a heavy border (`┏ ━ ┓ ┃ ┗ ┛`) and the `Selected` role on all its lines, so it stands out without colour; only the selected column shows a selected card. When the card area is shorter than 10 rows (a body under 12 rows) cards collapse to one line `marker key summary`, without a box, the selected one prefixed with `>` (second sketch below); a column without focus shows no mark at all (#673). A column too narrow for a box (under 8 cells inside the margin) shows no card text. Cards are drawn only for the rows in the viewport, never for all loaded issues (`Core/Boards/BoardCards` holds the measures); rows under the last whole card stay empty rather than showing half a card.
- **Movement.** `h`/`l`/`Left`/`Right` change column and keep the row index, clamped to the last card of the target column; the index aimed at is the one last chosen with `j`/`k`, so passing a short or empty column does not lose the row (an empty column is selectable and shows `no issues` in `Muted`). `0` and `$` jump to the first and last column. `j`/`k`, `g g`, `G`, `Ctrl-d`, `Ctrl-u` act inside the column; each column scrolls on its own and keeps three cards of context (`scrolloff`). `Enter` opens the selected card in the issue detail (#84) and does nothing on an empty column. After a refresh the selection stays on its issue key inside its column. The motion is `Core/Boards/BoardMotion` (pure).
- **More columns than width.** Each column is at least 20 cells wide, and below 60 columns exactly one column is drawn. When the columns do not fit, only the columns that fit are drawn, centred on the selected one, and the top edge ends with the number of hidden columns as a chip: `‹2 1›` (columns hidden on the left, then on the right). With one column drawn the header also names the neighbours, with `‹` and `›` where more lie beyond (the 40-column sketch below).
- **Issues mapped to no column.** Verified on the dev site (#138): the board endpoint does not return issues whose status the board configuration maps to no column, on a company-managed and a team-managed board alike (`boards/board-gcm-*-unmapped.json`, `boards/board-gtm-*-unmapped.json`: `To Do` left out of every column, 5 such issues per project found by JQL, none in the board's issue list), so a board view never loads them, same as Jira's own board; a JQL view shows them. Geeratya still adds a last column `Unmapped` when a *loaded* issue has such a status (a stale configuration, or a status changed by a write before the reload), so a loaded card is never hidden or unreachable.
- **Sub-task row (#520).** A card whose issue lists sub-tasks (the `subtasks` field, requested on every board source) shows, as its second summary line, `▸ Sub-tasks 1/3 ━━────`: the fold mark, done over total (status category `done`) and a six-cell bar (`Sub-tasks` and then the bar go first when the card is narrow); the summary keeps one line. Jira's board card layout (the Subtask summary card field) is not readable through the API (`jira-layer.md`, "Boards"), so the row shows whenever a card has sub-tasks. `z a` (`section.toggle`, titled `Expand or collapse sub-tasks of a card, or fold a backlog section` in the palette and help; also in the right-click menu) expands the card under the cursor: below it, nested in the parent's frame like grouped children, one card for each sub-task that is not itself a card of the source, with the type and key and the status at the right edge instead of the initials (the stub carries no assignee). The status is a bracketed chip, `[In Progress]`, in the colour of its status category (`StatusToDo`, `StatusInProgress`, `StatusDone`; readable without colour too, #645). The status key `<space>is` (`issue.transition`) works on such a row like on a card: the picker opens for the sub-task, the confirmation rules are the same, and a move shows in the chip at once (the parent's `subtasks` stub is patched, since the sub-task is not a row of the tab) and goes back if Jira refuses. The fold mark turns to `▾`. Those rows are selectable: `j`/`k` reach them, `Enter` opens the sub-task, and the other issue actions work on it. They are not counted in the column title. `z a` on one of them (or on the parent) collapses the list and puts the cursor on the parent. A sub-task that is a card of the source (the board filter includes it) keeps its own card and is not repeated in the list; a board filter that leaves sub-tasks out has no sub-task cards, and the list still shows them (from the parent's `subtasks`, no extra request). A click on the sub-task row (the fold mark or anywhere on it) does the same (mouse table, `SubtaskRow`, #558); while the cursor is on a card that lists sub-tasks, or on one of them, the status bar shows the hint `za sub-tasks` (first of the columns hints, so it survives on an 80-column line). `Enter` keeps opening the issue. A card without sub-tasks says so in the status bar. One-line cards (short terminals) show no row. The expanded cards are kept per tab.
- **Sub-tasks and epics** are cards like any other issue (the board endpoint returns them in their status column); the type tells them apart. Epics are neither hidden nor added by Geeratya: the board endpoint already applies the board's own rule (company-managed boards leave epics out, team-managed ones return them as cards, `jira-layer.md`, "Boards"), so the columns show what Jira's board shows. The epic swimlane (below) is the way to see them grouped.
- **Quick filters (#192).** `<space>vf` (`view.quick-filter`) switches the board's own quick filters (`GET board/{id}/quickfilter`, read the first time a filter key is used on the tab, so opening a board costs no request) one at a time: the first press turns the first filter on, each next press the next, and after the last all are off. `:quickfilter` lists them with numbers, `:quickfilter <name or number>` toggles one (several can be on; their clauses are joined with `AND`, each in parentheses), `:quickfilter off` clears them. The filters narrow the source: the board request carries their JQL (`(updatedDate >= -1d) AND (...)`, added to a Scrum board's own sprint clause) and the first page loads again, so every column, count and page is the filtered board; refresh and later pages keep them, and a filtered load neither reads nor writes the view cache. The title line names them (`Filter: Assigned to me`). Works on the board, backlog and sprint sources of both project kinds; a board with none says so. The filters belong to the tab.
- **Swimlanes (#192).** The agile API exposes no swimlane definition of a board (Jira keeps them in a private endpoint), so the lanes are Geeratya's own grouping of the loaded cards: `<space>vs` (`view.swimlane`) cycles none, assignee, epic (the epic is the parent whose type is an epic; sub-tasks and issues without one fall in `No epic`). The layout shows one lane at a time, all its columns, and `]]` / `[[` (the section keys) step to the next or previous lane, wrapping around; the title line says `Lane 2/5: Name`. Lanes come in order of first appearance, `Unassigned` or `No epic` last, and a lane that disappears after a refresh falls back to the first. The columns, counts and cursor are those of the current lane only; the selection moves to the first card of the lane, so a write never targets a hidden card. Grouping is available in the columns layout only. Lanes the board defines in Jira (by query) are not read; the status line says which grouping is active.
- **Grouping under the parent.** Any issue with a `parent` is a child (sub-tasks, and stories under an epic). Within a column a child whose parent card is in the same column is moved to just after the parent (after the children already there), and the parent and its children share one outer frame and the children are indented by two cells inside it: the parent is the top of the frame (top border, key line, two summary lines, then a divider `├───┤`), each child follows with its key line, two summary lines, a blank row and a divider, and the last child's divider is the bottom of the frame (`└───┘`). The cards keep the five-row pitch, so scrolling and navigation are unchanged. The parent's summary text is `Strong` (bold; the type name keeps its type colour) so it stands out from its children. A selected member has heavy sides (`┃`), a heavy divider or bottom edge under it (`┣━┫`, `┗━┛`) and the `Selected` role on its text; the selected parent also has a heavy top border, and when a child is selected the divider above it is heavy too, so the selected card is closed on all four sides. The rest of the frame stays `Muted`. The other cards keep the rank order. A child whose parent is in another column (or not loaded) stays in its place as a box of its own and carries the parent in its top border: `┌─ ↑ GTM-9 Onboarding flow ─┐` (key and summary cut with `…`; heavy border when selected). Compact one-line cards cannot hold a frame: a grouped child starts with `├` (`└` for the last) after the cursor cell and an ungrouped one shows `↑GTM-9` after the key. The frame uses box lines like the rest of the board (not part of the ASCII glyph set); a column too narrow for a box shows no card text. A parent that has children but itself has a parent in another column carries that parent in the frame's top border. When the column is scrolled so that a child is the first visible card, the frame's top edge is off screen. Navigation follows the displayed order. `Core/Boards/BoardColumns.Arrange` does the ordering (pure).
- **Type colours.** The type name (or marker) on the key line is coloured by type, as Jira does: epic purple, story green, bug red, task blue, sub-task teal (grey in the 16-colour tier). The colours are Geeratya's own: the Jira API gives no colour for an issue type (only an icon image, which is never drawn; the status category has a colour name, used for statuses) (roles `TypeEpic`, `TypeStory`, `TypeBug`, `TypeTask`, `TypeSubtask`; any other type stays `Body`). The no-colour tier relies on the name itself. The selected card keeps the single `Selected` role (reversed), so its type is not coloured. One-line cards colour the marker the same way. The list layout is unchanged.
- **Order and limits.** Cards come in the order the board endpoint returns (rank). Pages load in the background until the total is reached or the per-tab row cap (`ViewPaging.MaxRowsPerTab`, 10 000; `architecture.md`) is hit.

At 40x10 (body of six rows, one-line cards, one column at a time, the neighbours named in the frame):

```text
 [1 Board: GCM board]

GCM board · 24 issues                 3›
┌─Backlog 12  To Do 5 ›────────────────┐
│>T GCM-1 Survey task one              │
│ └s GCM-6 Survey4 subtask             │
│ T GCM-4 Survey task four             │
│ S GCM-3 Survey story three with unic…│
└──────────────────────────────────────┘
 NORMAL        ? help  : commands  q quit
```

## Issue detail

Enter on a list row or card opens the issue in the same tab; the tab's view is kept underneath as a stack (`Esc` pops, see workflow 4). The breadcrumb `GCM board › GCM-12` (with `Loading…` or `Some parts failed to load` and the `Top`/`NN%`/`Bot` ruler) is in the status bar. The screen is one rounded frame divided into panes (header, description, relations, comments) that share their edges (`ux-look.md` section 2) in one scrolling column, so it reads as a form with parts rather than as a text dump. The mockup is the 80x24 snapshot of an epic with every relation pane: the relation panels (sub-tasks, children, links, remote links, attachments) are built (#88) and the cursor row (`>`) with section jumps and openable rows (#216). The border turning bright on the focused panel is not built; the `>` marker carries the focus.

Survey that shaped it (what comparable tools do): lazygit and k9s draw every part in a bordered box with the title set into the top edge and mark the focused box by border colour; `gh dash` and jira-cli show a field table next to or under the title and keep long text in its own titled block; the Jira web page groups the same parts into cards with headings. Taken over: titled rounded panels, a label column for the fields, long text in panels of its own, a muted border that turns into a bright one on the focused panel (not built: the cursor row marker is enough, see "Cursor and scrolling"). Not taken over: side-by-side panes (the detail needs the width for prose, and one scrolling column works in the 40-column case).

```text
 1 JQL My open issues x   +                                       <space>g jump

╭─ GCM-12 · Epic ──────────────────────────────────────────────── In Progress ─╮
│ Updated   2 h ago                                                            │
├─ Description ────────────────────────────────────────────────────────────────┤
│ No description.                                                              │
├─ Sub-tasks 1 ────────────────────────────────────────────────────────────────┤
│ GCM-18 Write the migration · To Do                                           │
├─ Epic children 2 ────────────────────────────────────────────────────────────┤
│ GCM-21 Child story · In Progress · Task                                      │
│ GCM-22 Another child · In Progress · Task                                    │
├─ Linked issues 2 ────────────────────────────────────────────────────────────┤
│ blocks GCM-30 Launch · To Do                                                 │
│ is blocked by GCM-9 Legal review · Done                                      │
├─ Remote links 1 ─────────────────────────────────────────────────────────────┤
│ Design spec · Confluence                                                     │
│ https://example.com/spec                                                     │
├─ Attachments 2 ──────────────────────────────────────────────────────────────┤
│ 日本語のノート.txt · text/plain · 47 B · 2 d ago                             │
│ screenshot.png · image/png · 3.3 MiB · 5 h ago                               │
├─ Comments ───────────────────────────────────────────────────────────────────┤
│>No comments.                                                                 │
╰──────────────────────────────────────────────────────────────────────────────╯
 NORMAL  My open issues › GCM-12 · Bot    j/k scroll  ? help  : commands  q quit
```

- **Panels.** A rounded frame (`╭ ╮ ╰ ╯ ─ │`, `+ - |` on terminals without Unicode, one cell for one cell) with one column of padding inside the sides and the title in the top edge. Borders are `Border` (`FrameFocus` on the pane that holds the cursor), titles are chips (`ChipFocus` filled for that pane, dim otherwise; the header panel's title is the key and the type). Panels follow each other without a gap. Below 12 columns the frames are dropped and the titles stay as plain lines. Content never touches the border: it wraps at the inner width, and a title that does not fit is cut with `…`.
- **Header.** The panel title is `KEY · Type`; inside, the summary (wrapped, bold), a blank line and the fields as a table with a 10-cell label column in `FieldLabel` (steel blue, apart from the values, which are `Strong` or the status colour; `StatusToDo` is bright, not muted, so a To Do value is never mistaken for a label): status (category role), priority, assignee, reporter, labels, parent (`KEY summary`, opens with `<space>p`), the parent line is labelled `Epic` instead of `Parent` when the parent is an epic, sprint (the current sprint with its state, then `; earlier: ` the other sprints the issue was carried through), estimate (the number; the board's field name is not carried yet, #439), updated time. Sprint, estimate and epic are left out when absent (#179). From 60 inner columns two fields that fit half the panel share a row; a field that does not fit (a long parent, many labels) takes a row of its own and wraps under its value. Below 24 inner columns the label and value run together as `Label: value`. Missing values are omitted, not shown as empty. Fields that can be changed (status, priority, assignee, labels) end with a ` ▾` marker (`v` on terminals without Unicode) in the `Editable` role (amber); read-only fields (reporter, parent, updated) have none. The editing keys arrive with the write issues; the marker only shows what is editable.
- **Description.** In the `Description` panel: the ADF rendered as formatted text (`docs/jira-layer.md` "ADF in real data"). Media nodes show as `[image] name` or `[image]` when no name can be matched; nothing is rendered as an image. A missing description shows `No description.` in `Muted`.
- **Sections, in order:** Sub-tasks (from the issue's `subtasks`), Epic children (`parent = KEY`, see below), Linked issues, Remote links, Attachments, Comments. Each is its own panel with `Name n` in the title chip and is drawn only when it has entries; with none of them the document goes from Description to Comments.
  - A sub-task or child row reads `KEY summary · Status` (the status in its category colour; children also end with `· Type`).
  - A link row starts with the link type's wording for this issue's side, `blocks GCM-30 Launch · To Do` or `is blocked by GCM-9 Legal review · Done`; the direction comes from which side Jira sent (jira-layer.md "Issue links"). `<space>iu` on such a row removes the link after a Confirm (workflow 11, #190); `<space>il` adds one.
  - A remote link is its title (`Strong`) and the application (`Muted`, Confluence and similar) on one line and the URL (`Link`) on the next, wrapped by character.
  - An attachment is `name · type · size · age`: the file name, the media type (`unknown type` when Jira sent none), the size in binary units (`47 B`, `1.5 KiB`, `3.3 MiB`) and the relative time. Nothing is fetched or drawn from the file; the row opens the issue page in the browser (the API download URL needs the auth header, jira-layer.md "Links").
  - Epic children are loaded when the issue arrives and only for an issue type with hierarchy level 1 or above (an epic, in both project kinds), by `parent = KEY ORDER BY Rank`, the first 50. A longer list is marked `Epic children 50+` and `Showing the first children only.`; one level only, a grandchild is not shown. Level 0 issues (Story, Task, Bug) show their `subtasks` instead and make no search. The legacy Epic Link field is ignored (`parent` is the link in both kinds).
  - While remote links or children load, their panel shows `Loading…`; if the load fails the panel shows the error text and the other panels stay. A loaded empty list draws no panel.
- **Comments.** The `Comments` panel; oldest first, newest last, 50 per page. A thread with more comments ends with `Load more comments (Enter)`; `Enter` loads the next page below. The title reads `Comments n`, or `Comments n of total` while pages are missing; a thread is cut at 1000 comments with `Showing the first comments only.` A comment shows author, relative time (against the time of the last reply, so it does not tick on its own), `(edited)` when it changed more than a minute after writing, and the rendered ADF body, a blank line between comments. A failed page shows its error with `(Enter to retry)`. Every line of a comment (byline and body) belongs to that comment: the comment keys act on the comment under the cursor, `Enter` on it does nothing more than on a plain line, and `<space>o` opens the issue.
- **Comment keys (#167).** `<space>m a` adds a comment to the shown issue (the cursor may be anywhere), `<space>m e` edits the comment under the cursor, `<space>m d` deletes it; without a comment under the cursor the status line says `Put the cursor on a comment first.`, with no issue open `Open an issue first.`. Delete asks in Confirm mode (`y`/`n`/`Esc`, never `Enter`): `Delete the GCM-3 comment? Jira cannot undo this. By Eve: "first words"`; the comment goes when Jira accepts (no optimistic removal), a refusal leaves it in place with the reason in the status line. The three writes never touch the list rows or the issue header: they do not mark a list stale and do not reload the issue.
- **Comment editor (#167).** A box over the issue (`Comment on GCM-3` or `Edit comment on GCM-3` in its top edge), Insert mode: printable keys and paste are text (a paste keeps its line breaks, a tab becomes four spaces), `Enter` is a new line, `Backspace`/`Delete` remove a character, `Left`/`Right`/`Up`/`Down`/`Home`/`End` move the cursor (by character, never inside an emoji or combining sequence; up and down by visual row, keeping the display column; `PageUp`/`PageDown` move half a screen of rows, `Ctrl-Left`/`Ctrl-Right` move by word, `Home`/`End` go to the start and end of the row), `Ctrl-s` (or `Alt-Enter`, for a terminal that swallows `Ctrl-s`) saves, `Esc` cancels. The text is Markdown-like (`AdfMarkdownWriter` and `AdfMarkdownParser`: bold, italic, code, links, lists, quotes, code blocks, tasks, mentions), the same as the description editor will use. The cursor is a `Selected` cell; a long line wraps after its last space (a word longer than the row wraps by character), the last column of a row is kept free for the cursor, and the box scrolls so the cursor row stays visible. Under the text the box shows, in order: `Saving…`, the error of the last save (`Error`), a warning (`Stale` role): `This comment has formatting the editor cannot show; saving replaces it with the text below.` for a comment whose ADF does not survive the text round trip (a table, panel, media, colour, date, status or unknown node; `AdfRoundTrip`), and `Not your comment: Jira refuses unless you may edit all comments.` when the author is not the signed-in account (Jira lets project administrators edit others' comments, so the editor opens and Jira decides). The footer shows `Ctrl-s send` (`save` when editing), `Enter new line`, `Esc cancel`. Inline suggestions (#215): typing `@` (at the start of a word) opens the people who can see the issue and narrows with the name typed after it; `#` and a project key (`#GCM`, then `#GCM-12`) opens issues. Up to five rows show under the text (`>` marks the chosen one, then a `searching…`, `lookup failed` or `No match` line) and the footer reads `Down/Up choose   Tab/Enter accept   Esc close list`. `Down`/`Up` (`Ctrl-n`/`Ctrl-p`) choose, `Tab` or `Enter` replace the trigger with a mention (`@[Name](mention:id)`, sent as a Jira mention) or a link to the issue (`[KEY](site/browse/KEY)`; the `#` is dropped), `Esc` closes only the list (the same trigger does not reopen it). With no rows `Enter` is a new line. A mention or issue link is one unit for `Left`/`Right`/`Backspace`/`Delete`, and the box shows it in its short form (`@Olga Other`, `GCM-12`; #339): wrapping, the cursor column and `Up`/`Down` use that width (`EditorLayout.Cells`), the cursor on it selects the whole short form, and the saved text is unchanged. Only people who can see the issue are offered, so a new mention never goes to someone who cannot. Mentions already in a comment you edit can name someone who cannot see the issue: opening the editor asks Jira once per distinct person (`viewissue/search` by the name in the token, #340) and a `Stale` line `May not see GCM-3, so the mention may do nothing: <names>.` lists those not found; it is a hint (a failed lookup or a full page of 20 matches flags nobody, a renamed person may be flagged wrongly), deleting the mention clears it, and saving is never blocked. Mouse: none yet; clicking a popup row arrives with the mouse part (#200).
  - **Saving waits for the reply.** `Ctrl-s` starts one write (it takes the issue like any write: a second write on it is refused with `A-1 is busy (...)` shown in the box) and the box shows `Saving…`; typing, saving and `Esc` do nothing until the answer. On success the box closes, the status line says `Added a comment to A-1` / `Saved the comment on A-1`, and the comment list changes from the reply: the new comment is appended (only when no further pages are unloaded; else the count grows and `Load more comments` brings it), an edited one is replaced in place, a deleted one is removed, in every tab that shows the issue. On a definite failure (403, 404, 400, 429, offline) the box stays open with the text and the reason (`No permission to edit the comment on A-1: <Jira's reason>`) and can be saved again. On a lost reply the box keeps `Saving…` while the comments are read (not the search index): the comment found means done, not found means `A-1: the comment was not added` in the box, and an unreadable thread means `could not check the result; reload to see the current state`. The write is never repeated by itself.
  - **Nothing is lost by accident.** `Esc` on unchanged text (an empty new comment, an edit without changes) closes at once; on changed text it asks `Discard the unsent comment text? It is not saved anywhere (y/n)`: `y` throws the text away, `n` or `Esc` goes back to the editor with the text as it was, `Enter` does nothing. Saving an empty or blank text is refused in the box (`A comment cannot be empty.`) and saving an unchanged edit closes with `No change to save.`; neither sends anything. Text beyond 30 000 characters is cut (Jira allows 32 767). The text of an existing comment is cleaned when the editor opens (line breaks become `\n`, a tab four spaces, other control characters are dropped), so another author cannot put escape sequences on the screen. After a lost reply the check of an add looks for a comment of the same author and text with an id above the highest one the issue showed; an identical earlier comment on a page that was not loaded can be mistaken for the new one (a narrow window: a lost reply plus the same text twice).
- **Comment editor suggestions, design notes (#215).** The popup is the shared `SuggestionState`, drawn as rows inside the box above the notes, not floating at the cursor, because the box already lays out by rows and a floating popup would need clipping at the terminal edge. A mention is `@[Name](mention:accountId)` and an issue is a link mark `[KEY](site/browse/KEY)`, not an inline card: a link mark renders as plain text in every Jira surface and in this app's renderer, while a smart card depends on the viewer resolving the link. Both stay atoms in the editor (`AdfMarkdownTokens`): one cursor step, one Backspace or Delete.
- **Cursor and scrolling.** The whole body (header included) is one document between the top and bottom edge of the frame (two rows); the ruler `Top`/`NN%`/`Bot` shows in the status bar when it does not fit. The cursor is a line of that document, drawn with `>` in the `Selected` role in the padding cell inside the left border (the whole line in `Selected` on a screen too narrow for frames). `j`/`k`/`Down`/`Up` move it a line, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` half a window, `g g`/`G` to the first and last line; the cursor skips the shared panel edges (the first pane has none: its title is in the top edge of the frame) and the window scrolls only when the cursor would leave it. `] ]` and `[ [` (`detail.section-next`, `detail.section-previous`) put the cursor on the first line of the next section (a panel), or of the current one and then the previous; the window then starts at that section's first line, under the top edge of the frame that carries its title. At the last or first section nothing moves. The document is laid out once per change of data or width (`IssueDocumentBuilder`), together with what each line opens and where each section starts, and the screen draws a window of it. When data arrives (a panel appears or grows), a cursor on an openable row stays on that row (same target, same offset inside a wrapped row), so Enter opens what the user was looking at; a cursor on a plain line keeps its line number.
- **Openable rows.** A sub-task, epic child or linked issue row opens that issue (`Enter`, pushed on the stack, `Esc` pops; only a well-formed key `PROJ-123` is openable). A remote link (title and URL lines) opens its URL and an attachment opens the issue page in the browser, with `<space>o` or `Enter`; the file is never fetched or drawn. Remote link URLs must be `https` without credentials or control characters, anything else is refused with a status note.
- **Keys.** `Esc` goes back one level (pops the stack, after clearing a status notice or typed prefix; at the view it does nothing more). `Enter` opens the cursor row (see above); on any other line it loads the next comment page or retries a failed one. `<space>o` opens the cursor row in the browser, or the shown issue (the one on top of the stack, not the list row under it) from a plain line. `<space>p` pushes the parent of the shown issue (from the issue, or from the list row while it loads); with none, or with no issue open, the status line says so. `<space>r` reloads the issue.
- **Loading.** The header shows the fields of the list row at once and `Loading…` in the Description panel until the issue arrives (the breadcrumb also says `Loading…`). A cached issue (if any) shows at once with the `STALE` banner until the network reply replaces it (#221), and `<space>r` reload with refresh (#89). Related data (comments, remote links, epic children) loads separately, each section showing `Loading…` until it arrives or an error line.
- **Not loaded.** A failed or missing issue shows its error in an `Error` panel under the header panel instead of the Description, relation and Comments panels.
- **Partial failures.** Each of the loads (issue, comments, remote links, epic children) fails on its own: the issue stays readable, the failed part shows its error, and the breadcrumb says `Some parts failed to load`. A network error on the issue itself shows its message with `Esc` to go back; the list stays underneath.
- **Not found.** 404 and 403 give the same text, `Not found or no access: GCM-99`, with `Esc` to go back.

At 40x10 (the frames stay, the content wraps inside them, fields are one per row):

```text
 [1 Board: GCM board] 2 Backlog 3 JQL…›

GCM board › GCM-12 · Top
╭─ GCM-12 · Task ──────────────────────╮
│ Fix login redirect after token       │
│ refresh                              │
│                                      │
│ Status    In Progress ▾              │
 NORMAL  ? help  : commands  q quit
```

## Tab bar

> The tab rows in the sketches of this document (`[3 JQL: ...]`, a colon after the kind, a bracketed active tab) predate the chip look; the rule is the paragraphs below and [ux-look.md](ux-look.md), section 5.

One line at the top on the bar background (look: [ux-look.md](ux-look.md), section 5). Each tab is a padded chip ` <number> <kind> <name> ` (`Alt-1` to `Alt-9` go there), chips separated by one bar cell. The active chip is filled with the accent and bold (bold without fill in the no-colour tier, where the bar itself is reversed); inactive chips are muted text on the bar. Kinds: `Board`, `Backlog`, `Sprint`, `Filter`, `JQL` (no colon); a label (`<kind> <name>`) is cut to 24 cells, inactive ones shrink towards 8 when the line is short. A chip shows `…` after its label while loading and `!` when its last load failed. An issue opened inside a tab does not rename the tab. At 80 columns and more the active chip ends with ` x` (click closes the tab, `docs/ux-mouse.md`). In the ASCII glyph set the padding cells of every chip become brackets (`[1 Board GCM]`, same width).

Overflow: when the chips do not fit, the window of tabs slides so the active one is fully visible, `‹n` (tabs hidden on the left) and `+n ›` (right) mark the cut ends as dim chips, and inactive labels shrink towards 8 cells before any is hidden. Under 40 columns only the active chip and the `+` remain (no marks; `Alt-N`, `Tab` and `Shift-Tab` still switch). At 120 columns:

```text
 ‹3  6 JQL project = GCM AND la… x   7 Backlog GCM board   8 Filter Mine   9 Board GTM board  +2 › 
```

A ` + ` button (clickable, `tab.new`) follows the last chip when the line has a spare cell and no tab is hidden on the right; the `‹n` and `+n ›` marks are clickable too and open the tab list in the picker (`tab.list`, `docs/ux-mouse.md`). The window grows around the active tab, right neighbour first, as long as the line fits. The bar always takes the first row, full width on the bar background and blank when no tab is open, so the body height and the list viewport do not change when the first tab opens or the last one closes. Loading and failed marks follow the tab's last request. Next and previous wrap around.

A dim chip with the keys of `issue.jump` (` <space>g jump `, clickable) is right-aligned in the line when at least one cell remains after the last chip or the `+`; it is not drawn when no tab is open. From 44 columns a dim ` : commands ` chip (`palette.open`, clickable) sits left of it; the tabs always leave its cells free (so the `+` and the jump chip give way first), and the jump chip is dropped when it would not fit beside it. The commands chip also shows with no tab open.

Keys: `Tab`/`Shift-Tab` (also `g t`/`g T`) next and previous, `Alt-1` to `Alt-9` go to a tab, `<space>t` opens the source picker for a new tab, `<space>c` closes the tab (the right neighbour becomes active, the left one if it was last). Closing the last tab leaves the empty shell with its hint line. There is no tab limit; the numbered keys reach the first nine.

## Connection check and network states

Implemented (#83): the check line is shown on the start screen under the profile line and, unless the connection is healthy (#646), in the status line whenever no notice or pending keys occupy it (text only, no `Loading` role yet); `:profile check` runs it again. Connection check line: while the first request of a profile is in flight the text reads `Checking example.atlassian.net...`; on success `Connected as <display name> on <host>`, and the body shows what is cached. The first frame is drawn before any reply. On failure the banner states the reason (below).

States, as a one-line banner under the tab bar (highest priority wins: error, offline, rate limited, stale):

```text
 OFFLINE  Cannot reach example.atlassian.net. Retrying in 8 s, <space>r retries now. Data is 23 min old.
 RATE LIMITED  Waiting for the Jira rate limit. Retrying in 25 s.
 STALE  Data is 23 min old. Refreshing…
 ERROR  Jira rejected the token (401). Replace it: :profile edit work and type the token again.
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
| 401 | `Jira rejected the token for <email> at <host> (401). Replace it: :profile edit <name> and type the token again.` (a wrong email and a wrong token look the same) |
| 403, 404 | `Not found or no access: <what>` (Jira does not tell them apart) |
| 400 on a JQL source | `Query error: <Jira's message>` with the line and character; the JQL stays in the tab so it can be fixed with `:jql` |
| 429 | rate-limited state, not an error |
| 5xx | `Jira returned an error (<status>). <space>r tries again.` |
| network, timeout | `Cannot reach <host>.` / `<host> did not answer in time.` (offline state) |

## Source picker overlay

`<space>t`, `Ctrl-t` (from the list, the board and the detail) and `:tab new` open a centred box over the tab. It is a filter prompt in Insert mode: printable keys are text, `Up`/`Down`, `Ctrl-p`/`Ctrl-n` or `Ctrl-j`/`Ctrl-k` choose, `Enter` or `Ctrl-l` opens the choice in a new tab, `Esc` closes. Rows come in groups: Boards (two rows per board, `Board` and `Backlog`), Filters (saved filters: favourites and the caller's), and Query, the last row, which offers the typed text as JQL and is always present when the text is not empty; text that is not a valid JQL clause (no operator, like `gcm`) is sent as `text ~ "gcm"`, anything else as typed. Typing filters boards and filters by case-insensitive substring; the first matching row is highlighted, or the Query row when nothing matches. Boards load in pages up to 20 pages (1000 boards), more are not offered; narrow with `:jql`. The lists load when the picker first opens and are cached, so reopening it does not reload (the lists live in memory for the session, not on disk; after a cold start offline the groups show the error); an empty cache shows `Loading…` in each group.

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

## Jump to an issue

`<space>g` (`issue.jump`, #194) opens a one-line prompt in Insert mode, drawn as a centred box like the source picker (`Core/Jump`, `JumpOverlay`). It is read-only: it never writes to Jira.

- **What is typed.** A key in any case (`gcm-12` is read as `GCM-12`), or only the number (`12`), which takes the project of the issue shown, else of the selected row, else of the last opened key. With no project known a bare number is refused with a notice asking for the full key. Text that is no key and completes nothing (`xyz`, `GCM-1/2`) is refused with a notice in the status line and the prompt stays open; text that is part of a known key (`gcm`) is a filter, and `Enter` opens the highlighted completion. Only a key that passes `IssueUrls.IsValidKey` after upper-casing is ever requested.
- **Rows.** The typed key first (with its title when the tab knows it), then completions that contain the text: the issues opened on the tab's stack (newest first), the tab's rows, then keys opened earlier (`recent`: those of this session, up to 20, then the profile's remembered issue keys from `SiteHistory.Issues`, newest first, no repeats; the history is read from disk when the prompt opens if not yet loaded, and is the profile's, so a profile switch shows the other one's). With an empty prompt the recent keys come first, then the tab's keys; typing filters all of them. `Up`/`Down`, `Ctrl-p`/`Ctrl-n` and `Ctrl-j`/`Ctrl-k` choose, `Ctrl-l` accepts, at most 30 rows. A key is remembered when its issue loaded, so a key that was not found never becomes a completion.
- **Enter** opens the highlighted row on top of the active tab's issue stack (or in a new tab, see Target), so `Esc` returns to where the user was (the stack limit applies: with 50 issues open the status line says so). With no tab open, a tab for `key = KEY` is opened first. The issue loads as from any list (cache first, three requests); a missing or hidden issue shows `Not found or no access: KEY` in the detail, as for any other open.
- **Target.** `:set jump.target current|tab` (default `current`, see [settings.md](settings.md)). With `tab` every jump opens a new tab `key = KEY` with the issue on top, and `Esc` in it returns to that one-issue list; the tab you jumped from stays as it was.
- **Mouse.** A click on a prompt row and the menu item `issue.jump` already run this reducer, so they follow the target. A click on the jump chip (` <space>g jump `) at the right end of the tab bar row (drawn when the bar has room) opens the prompt, same as the key.

## Field form overlay

The edit form has a navigate and an edit state (`ux-keymap.md`, "Edit form: navigate and edit states"; the focused label shows `[navigate]` or `[editing]` and the footer lists the keys of the state). One form model serves transition, create and edit (`architecture.md`, "Where form state lives"). It is a centred box over the tab, opened in Insert mode on the active tab (`TabState.Form`); built so far are the edit form (`<space>ie`, or `:edit`; `:priority` and `:labels` limit it to one field) and the people picker (`<space>ia`, `:assign`), both on the shown or selected issue (#164), the create form (`<space>ic`, `<space>iC`, `:create`; workflow 12), which starts with a type picker (the same box with the types as a list, `Down`/`Up` and `Enter`) and offers the parent through the suggestion popup (prefilled from the selection), and the status forms (#159): a one-field picker `Change status of GCM-3` (field `Move to`, the transitions Jira offers) and, for a transition that asks for fields, a second form `Move GCM-3 to Done` with those fields. A column move (`Alt-l`/`Alt-h`) opens the picker invisibly and sends at once when one transition leads to the column. The field list, the kinds, the required marks and the options come only from the metadata Jira returns for the issue (`editmeta`; create and transition metadata map the same way), never from code.

```text
 [1 JQL: project = GEN]

project = GEN · 40 issues
          ┌─ Edit GEN-1 ─────────────────────────────────────────────────────────────────────────────────────┐
          │ ┌─ > Labels────────────────────────────────────────────────────────────────────────────────────┐ │
          │ │ [backend] [ui] _                                                                             │ │
          │ └──────────────────────────────────────────────────────────────────────────────────────────────┘ │
          │ ┌─ Summary * ──────────────────────────────────────────────────────────────────────────────────┐ │
          │ │ -                                                                                            │ │
          │ └──────────────────────────────────────────────────────────────────────────────────────────────┘ │
          │     ! Required                                                                                   │
          │ ┌─ Team  (not editable here)───────────────────────────────────────────────────────────────────┐ │
          │ │ https://work.atlassian.net/browse/GEN-1                                                      │ │
          │ └──────────────────────────────────────────────────────────────────────────────────────────────┘ │
          │ ──────────────────────────────────────────────────────────────────────────────────────────────── │
          │ Tab/Shift-Tab field   Ctrl-s save   Esc cancel                                                   │
          └──────────────────────────────────────────────────────────────────────────────────────────────────┘
 INSERT ! Summary: Required                              Tab/Shift-Tab field  Ctrl-s save  Esc cancel
```

- **Which fields the edit form offers.** The editor metadata carries no values and the form writes only changed fields, so a field is offered only when the view already holds its current value: summary, labels and priority from a row, plus the description from the opened issue. A description that does not survive the Markdown-like text (tables, panels, media, colours, ...; `AdfRoundTrip`) is listed read-only with `(edit in browser)`, because saving it here would lose content. Other editable fields (due date, components, versions, a number estimate, custom fields) get their value from one issue read with only those ids when the form loads (#283); a value of an unexpected shape is not offered. The assignee has its own picker, because Jira assigns through its own call.
- **People picker (`<space>ia`).** The same overlay with one person field, Assignee, opened at once (no metadata to fetch), showing the current assignee. While the field is empty the list offers two quick rows, `Me (name)` (the signed-in account, hidden when already the assignee) and `Unassigned` (only when someone is assigned), with nothing highlighted so `Enter` still saves the form as it stands; `Down` and `Enter` take one, `Esc` closes the form. Typing replaces them with the suggestion list (below) and emptying the field brings them back. The suggestion list (below) shows the people who can be assigned the issue; `Enter` or `Tab` takes the highlighted one, `Enter` again assigns, `Backspace` on an empty field clears the value (unassigned), `Esc` closes the list, then the form, sending nothing. There is no search of its own: the picker uses the same debounced lookup as every other person field. In the create form (#342) the assignee searches people assignable in the project, any other person field (reporter, custom user fields) people who can see the project's issues, and components and versions fields search the project's lists once a filter is typed (an empty filter still lists the field's allowed values).
- One labelled box per field (#651): the label in the top edge (`*` when required, `>` on the focused one, so focus never relies on colour; the focused box is accent, a read-only one muted), the value inside, then the suggestion rows and the field's error (`! message`, error colour) under the box. A top line holds what belongs to no field (a refused write, a failed metadata load) and the state: `Loading fields…`, `Saving…`, `Discard the changes? (y/n)`. The status line shows the same state, or the error of the focused field.
- Values by kind: text, number, date (`yyyy-MM-dd`), date-time (`yyyy-MM-dd HH:mm`, local time) show the text and a `_` caret on the focused field; a paragraph shows its last four lines while focused and the first line with `…` otherwise; labels show `[label]` tokens and the label being typed; a single or multiple choice shows its chosen values as `[Value]` chips and, while focused, the typing buffer and the suggestion popup below (its allowed values as the pool, ranked locally, no Jira call).
- A field the form cannot edit (an unknown schema type, a field with no operation) is listed last with `(not editable here)` and the issue or project page address; it is never dropped and never takes focus.
- Suggestion popup (#214): in a labels, user or parent field the list opens by itself with the first typed character and narrows as you type (fzf style, no extra key): up to five rows under the box, the highlighted row marked with `>`, ranked locally over what is loaded (prefix, then contained, then scattered matches) while Jira is asked after a 250 ms pause. A line under the rows says `searching…` when the lookup takes over 150 ms, `lookup failed, showing what is loaded: <reason>` when it failed, or `No match`. Labels end with a row `<text> (new)` for a label that does not exist yet. While the list has rows the footer reads `Down/Up choose   Tab/Enter accept   Esc close list`. A person or parent shows as `[Name]` (inactive users are marked) with the typing buffer after it; `Backspace` on an empty buffer clears it. Selects and multiple choices use the same popup (#293) with the field's allowed values as the pool and no Jira call: it is open whenever the field has the focus, listing every value (not yet chosen, for a multiple choice); typing ranks them, `(none)` comes first for a single choice that is not required. With an empty buffer nothing is highlighted, so `Enter` moves on, except in a required single choice without a value, where the first row is highlighted; `Down` highlights a row. A single choice takes the row and moves to the next field (`Enter` saves from the last one); a multiple choice adds a chip and stays, `Backspace` on an empty buffer removes the last chip. `Esc` with text typed clears the buffer; on an empty buffer it goes to the form.
- The focused block scrolls into view; the footer drops below 8 inner rows. Long labels are cut at the box edge, wide characters are never split.
- Keys (`ux-keymap.md`, "Text input"): `Tab`/`Shift-Tab` move between editable fields (they wrap; inside a form they do not change tab), `Ctrl-s` saves, `Ctrl-o` opens the issue page in the browser (the footer shows it when a field is read-only), `Esc` cancels. `Enter` accepts the field and moves on, and on the last field saves; in a paragraph it adds a line; in a multiple choice a highlighted value is added as a chip; in labels it takes the highlighted suggestion or, with none, adds the typed label (`Space` adds the typed text as it is). `Up`/`Down` or `Ctrl-p`/`Ctrl-n` move the highlight, typing filters the options, `Backspace` deletes (in empty labels it removes the last label).
- Save checks first: a required editable field left empty, a number or date that does not read: the error shows at the field, the focus jumps to the first one and nothing is sent. Only changed fields are written. While the write runs the form shows `Saving…` and ignores edits; `Esc` closes it and the result goes to the status line. A refusal from Jira keeps the form open with each message at its field (a field the form does not show goes to the top line) and the text typed is kept. `Esc` on a form with changes asks `y`/`n` first.

## Palette commands that open views

`:` opens the palette. All of these open a new tab, then load in the background:

| Command | Effect |
|---|---|
| `:jql [query]` | JQL list; the query is everything after the first space. Without a query the picker opens in its JQL scope: the recent queries of this profile (filtered by the typed text) and the typed text as a query row |
| `:history clear` | forgets the recent JQL queries and issue keys of the active profile (local file only; nothing in Jira changes); `:history` with anything else is an unknown command |
| `:browse [KEY]` | open the issue in the browser (`<space>o` handler): `KEY` is upper-cased and must be a valid issue key; without it, the selected row. Status line: `Opened KEY in the browser` or the launcher failure |
| `:filter [name]` | the saved filter whose name matches (exact, else unique prefix, case-insensitive) as a list; no name, or no unique match, opens the picker filtered to Filters with the text typed so far |
| `:board [name]` | the board as Columns; same matching and fallback to the picker (Boards) |
| `:backlog [name]` | the board's backlog as a list; same matching |
| `:view [name]` | the saved-view picker (saved views only), filtered by the text typed; `Enter` opens the view in a new tab. The same rows come first in the `<space>t` picker under "Saved views" (#191) |
| `:view save <name>` | saves the active tab's source and layout under the name (a name in quotes keeps its blanks); an existing name (any case) is replaced. Local file only (`saved-views.json` in the profile's folder of the state directory, user-only permissions, written atomically); no token, site or rows (#191) |
| `:view rename <old> <new>`, `:view delete <name>` | rename or delete a saved view. Local names only, so no confirmation: nothing in Jira changes and the view can be saved again (#191) |
| `:tab new` | the source picker; `:tab close`, `:tab next`, `:tab prev`, `:tab <n>` are aliases of the tab keys; `:tab rename [name]` names the active tab (empty restores the source label) |
| `:layout list`, `:layout columns` | switch the layout of the tab (alias of `<space>l`) |
| `:quickfilter [off\|name\|number]` | list the quick filters of the board, switch one on or off, or clear them (`<space>vf` cycles them one at a time); a board tab only |
| `:refresh` | alias of `<space>r` |
| `:browse` | alias of `<space>o` |

Without a board name and exactly one board on the site, `:board` and `:backlog` open it directly. Text that is neither a command line nor matches a palette row shows "Unknown command: ..." (`ux.md`, "Command palette"). Commands appear in the generated help once they exist; completion of names is not part of M1.

## Decisions for the screens (defaults)

- The Backlog column of a company-managed Kanban board (first column, named `Backlog`) is hidden in the columns layout, as in Jira; a team-managed board shows its Backlog column like any other.
- Attachments open the issue page in the browser (the download URL needs the auth header).
- The board opens in Columns; backlog, JQL and filters open in List; the layout can be switched per tab (boards only in M1).
- A loaded issue whose status is in no column gets a trailing `Unmapped` column rather than being hidden (the board endpoint itself never returns such issues, #138).
- Palette view commands and the picker open a new tab; they never replace the current tab's view.
- Sub-tasks and epics on a board are cards like any issue; the columns layout groups children under their parent (see "Columns layout").

## Actions menu

A right-click on a list row, card or line of an opened issue (or `<space>a`) opens the `Actions` box: a small centred box over the body in Insert mode with the actions of that one issue (open in browser, parent when it has one, edit, assign to me, previous column, sub-task, link, remove link on a link row, delete; rank on lists; delete comment on a comment), each with its key. `j`/`k` or `Down`/`Up` choose, `Enter` or a click runs it through the same handler as its key (delete and remove link still ask first), `Esc` closes. The command palette stays on `:` and the status line.
