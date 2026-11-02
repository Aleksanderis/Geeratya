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

- **Columns.** Key (width of the longest loaded key, at most 12), one-cell type marker, summary (takes the remaining width), status (14), assignee (16). The status text uses the colour role of its category: `StatusTodo`, `StatusProgress`, `StatusDone` (new roles, added with the list; the no-colour tier relies on the text itself, and the 40-column glyphs are `○`, `◐`, `●`, ASCII `.`, `~`, `x`). The glyph set is chosen once at start (`ConsoleCapabilities.Glyphs`): ASCII when `TERM=dumb` or the locale (`LC_ALL`, `LC_CTYPE`, `LANG`, first one set) is not UTF-8, an unset locale counting as UTF-8. In ASCII the list header separator is ` - `, the loading marker is `...`, and the status glyphs are `.`, `~`, `x`; the Unicode set is `·`, `…`, `○`, `◐`, `●`. The list and the detail panel borders draw them (tab bar, banner, status bar and overlays still use `·`/`…`/box lines; in the detail, the `·` and `…` of titles and the `›` of the breadcrumb stay Unicode in ASCII mode). The type marker is `E` epic, `S` story, `T` task, `B` bug, `s` sub-task, `?` any other type; `E` and `s` come from the hierarchy level and the sub-task flag; level-0 types (Story, Task, Bug, identical in both project kinds) are told apart by name, anything else falls back to `?` (jira-layer.md "Hierarchy"). The marker may also carry a type colour role.
- **Header.** Column titles in the `Muted` role, then the view title and the count: `N issues` when every page is loaded, `N of about M` when the approximate count is known (`/search/approximate-count`, JQL sources only; "about" is never dropped), `N+` while the total is unknown. A board or backlog list knows its total (offset paged) and shows `N of M`.
- **Selection and scroll.** `j`/`k`/`Up`/`Down` move by one row, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` by half the viewport (at least one row), `g g`/`G`/`Home`/`End` jump. The selection keeps three rows of context (`scrolloff=3`, fewer in a very short viewport) and the scroll never leaves blank rows above the last. Selection survives a refresh or a page append by issue key; when the issue is gone the nearest row is selected. The motion is `Core/Lists/ListMotion` (pure and clamped; it takes a repeat count, but the keymap has no count prefix yet); the keys send `SelectionMoved`. The viewport height the reducer works with excludes the list's two header rows (title with count, column titles). Only the rows in the viewport are rendered, never the loaded 10 000.
- **Loading more.** Pages are fetched when the selection comes within one screen of the last loaded row. The note `Loading more…` (role `Loading`) shows in a row under the last row while the page is in flight; when the rows fill the screen it shows at the end of the header line instead. If the page fails the note becomes `Could not load more: <reason>` and the loaded rows stay; moving does not retry (a held key never hammers a failing Jira), an explicit retry does: `<space>r` retries the failed page (the loaded rows and the position stay). `Enter` is the open key, so it does not retry. The count is `N+` while more pages exist and `N issues` once all are loaded; with a known total it reads `N of about M` (JQL sources: the approximate count is asked after each first-page load, refreshes included, when more pages exist; a failed call leaves `N+`, as does a count below the loaded rows) or `N of M` (board and backlog, from the paged reply; it shows while pages are still loading, then `N issues`). A list cut at the row cap keeps the `N of M` / `N+` form.
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
└───────────────────┴───────────────────┴───────────────────┴──────────────────┘
 NORMAL                                                ? help  : command  q quit
```

The sketch is the 80x24 snapshot of the team-managed dev board (`Backlog` third); the cursor is on the first card.

- **Frames.** Every column is a framed box: the board is drawn as one grid with box-drawing lines (`┌ ┬ ┐ │ └ ┴ ┘ ─`, `Muted`), so the columns read as columns even when empty. The name and count sit in the top border (` Name n `); the last row of the body closes the frames. The selected column's title is in `Heading` (bold), the others in `Muted`. The count is the number of loaded cards; while pages are still loading it reads `Name n+`. The view title line shows `N issues` once all pages are loaded. The frame uses the same box lines as the tab bar and overlays (not part of the ASCII glyph set).
- **Cards.** Each card is a bordered box with a one-cell margin inside its column, five rows: top border, the issue type's name and the key (`Story GTM-9`) with the assignee's initials (first letters of the first and last name) at the right edge, the name cut with `…` when the line is too narrow; the name wins over the initials, which go first, and when 4 cells of name do not fit the one-cell marker (as in the list layout) replaces it, the summary wrapped to two lines (ellipsis on the second), bottom border. Cards touch (no blank row). The selected card has a heavy border (`┏ ━ ┓ ┃ ┗ ┛`) and the `Selected` role on all its lines, so it stands out without colour; only the selected column shows a selected card. When the card area is shorter than 10 rows (a body under 12 rows) cards collapse to one line `marker key summary`, without a box, the selected one prefixed with `>` (second sketch below). A column too narrow for a box (under 8 cells inside the margin) shows no card text. Cards are drawn only for the rows in the viewport, never for all loaded issues (`Core/Boards/BoardCards` holds the measures); rows under the last whole card stay empty rather than showing half a card.
- **Movement.** `h`/`l`/`Left`/`Right` change column and keep the row index, clamped to the last card of the target column; the index aimed at is the one last chosen with `j`/`k`, so passing a short or empty column does not lose the row (an empty column is selectable and shows `no issues` in `Muted`). `0` and `$` jump to the first and last column. `j`/`k`, `g g`, `G`, `Ctrl-d`, `Ctrl-u` act inside the column; each column scrolls on its own and keeps three cards of context (`scrolloff`). `Enter` opens the selected card in the issue detail (#84) and does nothing on an empty column. After a refresh the selection stays on its issue key inside its column. The motion is `Core/Boards/BoardMotion` (pure).
- **More columns than width.** Each column is at least 20 cells wide, and below 60 columns exactly one column is drawn. When the columns do not fit, only the columns that fit are drawn, centred on the selected one, and the title line shows the number of hidden columns right-aligned: `‹2` on the left, `1›` on the right. With one column drawn the header also names the neighbours, with `‹` and `›` where more lie beyond (the 40-column sketch below).
- **Issues mapped to no column.** A status the board configuration does not map to a column would be invisible in Jira's own board. Geeratya adds a last column `Unmapped` only when at least one loaded issue has such a status, so no issue is hidden (not yet verified against Jira: all statuses of the dev boards GCM and GTM are mapped, and an unmapped one needs a board-configuration write; tracked in #138).
- **Sub-tasks and epics** are cards like any other issue (the board endpoint returns them in their status column); the type tells them apart. Hiding epics follows the board configuration (#192).
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
 NORMAL        ? help  : command  q quit
```

## Issue detail

Enter on a list row or card opens the issue in the same tab; the tab's view is kept underneath as a stack (`Esc` pops, see workflow 4). A breadcrumb replaces the view title: `GCM board › GCM-12`. The screen is a stack of framed panels (header, description, relations, comments) in one scrolling column, so it reads as a form with parts rather than as a text dump. The mockup is the target layout: the relation panels (sub-tasks, children, links, remote links, attachments) are built (#88) and the cursor row (`>`) with section jumps and openable rows (#216). The border turning bright on the focused panel is not built; the `>` marker carries the focus.

Survey that shaped it (what comparable tools do): lazygit and k9s draw every part in a bordered box with the title set into the top edge and mark the focused box by border colour; `gh dash` and jira-cli show a field table next to or under the title and keep long text in its own titled block; the Jira web page groups the same parts into cards with headings. Taken over: titled rounded panels, a label column for the fields, long text in panels of its own, a muted border that turns into a bright one on the focused panel (not built: the cursor row marker is enough, see "Cursor and scrolling"). Not taken over: side-by-side panes (the detail needs the width for prose, and one scrolling column works in the 40-column case).

```text
 [1 Board: GCM board]  2 Backlog: GCM board  3 JQL: assignee = currentUser() AND resolution = Unres…

GCM board › GCM-12                                                                              Top
╭─ GCM-12 · Task ──────────────────────────────────────────────────────────────────────────────────╮
│ Fix login redirect after token refresh                                                           │
│                                                                                                  │
│ Status    In Progress ▾                        Priority  High ▾                                  │
│ Assignee  Jane Doe ▾                           Reporter  Ola Nordmann                            │
│ Labels    auth, bug-bash ▾                     Parent    GCM-17 Reporting overhaul               │
│ Updated   2 h ago                                                                                │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Description ────────────────────────────────────────────────────────────────────────────────────╮
│ After the token refresh the user lands on /login instead of the page they came from.            │
│ - reproduce: let the session expire, then open a deep link                                       │
│ - expected: the deep link opens after the refresh                                                │
│ [image] screenshot.png                                                                           │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Sub-tasks (1) ──────────────────────────────────────────────────────────────────────────────────╮
│ GCM-18 Write the migration · To Do                                                               │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Linked issues (2) ──────────────────────────────────────────────────────────────────────────────╮
│ blocks GCM-30 Launch · To Do                                                                     │
│ is blocked by GCM-9 Legal review · Done                                                          │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Remote links (1) ───────────────────────────────────────────────────────────────────────────────╮
│ Design spec · Confluence                                                                         │
│ https://example.com/spec                                                                         │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Attachments (1) ────────────────────────────────────────────────────────────────────────────────╮
│ screenshot.png · image/png · 3.3 MiB · 5 h ago                                                   │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
╭─ Comments (2 of 7) ──────────────────────────────────────────────────────────────────────────────╮
│ Jane Doe · 3 d ago                                                                               │
│ Can you check the refresh path?                                                                  │
│                                                                                                  │
│ Ola Nordmann · 1 d ago                                                                           │
│ Found it, the redirect target is dropped.                                                        │
│                                                                                                  │
│ Load more comments (Enter)                                                                       │
╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
 NORMAL  j/k scroll  Enter open  Esc back  <space>o browser  ? help
```

- **Panels.** A rounded frame (`╭ ╮ ╰ ╯ ─ │`, `+ - |` on terminals without Unicode, one cell for one cell) with one column of padding inside the sides and the title in the top edge. Borders are `Muted`, titles `Heading` (the header panel's title is the key in `Key` and the type in `Muted`). Panels follow each other without a gap. Below 12 columns the frames are dropped and the titles stay as plain lines. Content never touches the border: it wraps at the inner width, and a title that does not fit is cut with `…`.
- **Header.** The panel title is `KEY · Type`; inside, the summary (wrapped, bold), a blank line and the fields as a table with a 10-cell label column in `FieldLabel` (steel blue, apart from the values, which are `Strong` or the status colour; `StatusToDo` is bright, not muted, so a To Do value is never mistaken for a label): status (category role), priority, assignee, reporter, labels, parent (`KEY summary`, opens with `<space>p`), updated time. From 60 inner columns two fields that fit half the panel share a row; a field that does not fit (a long parent, many labels) takes a row of its own and wraps under its value. Below 24 inner columns the label and value run together as `Label: value`. Missing values are omitted, not shown as empty. Fields that can be changed (status, priority, assignee, labels) end with a ` ▾` marker (`v` on terminals without Unicode) in the `Editable` role (amber); read-only fields (reporter, parent, updated) have none. The editing keys arrive with the write issues; the marker only shows what is editable.
- **Description.** In the `Description` panel: the ADF rendered as formatted text (`docs/jira-layer.md` "ADF in real data"). Media nodes show as `[image] name` or `[image]` when no name can be matched; nothing is rendered as an image. A missing description shows `No description.` in `Muted`.
- **Sections, in order:** Sub-tasks (from the issue's `subtasks`), Epic children (`parent = KEY`, see below), Linked issues, Remote links, Attachments, Comments. Each is its own panel with `Name (n)` in the title and is drawn only when it has entries; with none of them the document goes from Description to Comments.
  - A sub-task or child row reads `KEY summary · Status` (the status in its category colour; children also end with `· Type`).
  - A link row starts with the link type's wording for this issue's side, `blocks GCM-30 Launch · To Do` or `is blocked by GCM-9 Legal review · Done`; the direction comes from which side Jira sent (jira-layer.md "Issue links").
  - A remote link is its title (`Strong`) and the application (`Muted`, Confluence and similar) on one line and the URL (`Link`) on the next, wrapped by character.
  - An attachment is `name · type · size · age`: the file name, the media type (`unknown type` when Jira sent none), the size in binary units (`47 B`, `1.5 KiB`, `3.3 MiB`) and the relative time. Nothing is fetched or drawn from the file; the row opens the issue page in the browser (the API download URL needs the auth header, jira-layer.md "Links").
  - Epic children are loaded when the issue arrives and only for an issue type with hierarchy level 1 or above (an epic, in both project kinds), by `parent = KEY ORDER BY Rank`, the first 50. A longer list is marked `Epic children (50+)` and `Showing the first children only.`; one level only, a grandchild is not shown. Level 0 issues (Story, Task, Bug) show their `subtasks` instead and make no search. The legacy Epic Link field is ignored (`parent` is the link in both kinds).
  - While remote links or children load, their panel shows `Loading…`; if the load fails the panel shows the error text and the other panels stay. A loaded empty list draws no panel.
- **Comments.** The `Comments` panel; oldest first, newest last, 50 per page. A thread with more comments ends with `Load more comments (Enter)`; `Enter` loads the next page below. The title reads `Comments (n)`, or `Comments (n of total)` while pages are missing; a thread is cut at 1000 comments with `Showing the first comments only.` A comment shows author, relative time (against the time of the last reply, so it does not tick on its own), `(edited)` when it changed more than a minute after writing, and the rendered ADF body, a blank line between comments. A failed page shows its error with `(Enter to retry)`.
- **Cursor and scrolling.** The whole body (header included) is one document under a one-line breadcrumb that ends with a `Top`/`NN%`/`Bot` ruler when it does not fit. The cursor is a line of that document, drawn with `>` in the `Selected` role in place of the left border character (the whole line in `Selected` on a screen too narrow for frames). `j`/`k`/`Down`/`Up` move it a line, `Ctrl-d`/`Ctrl-u`/`PageDown`/`PageUp` half a window, `g g`/`G` to the first and last line; the cursor skips the panel edges (the title and the bottom border) and the window scrolls only when the cursor would leave it. `] ]` and `[ [` (`detail.section-next`, `detail.section-previous`) put the cursor on the first line of the next section (a panel), or of the current one and then the previous; the window then starts at that panel's title edge. At the last or first section nothing moves. The document is laid out once per change of data or width (`IssueDocumentBuilder`), together with what each line opens and where each section starts, and the screen draws a window of it. The cursor keeps its line number when data arrives above it.
- **Openable rows.** A sub-task, epic child or linked issue row opens that issue (`Enter`, pushed on the stack, `Esc` pops; only a well-formed key `PROJ-123` is openable). A remote link (title and URL lines) opens its URL and an attachment opens the issue page in the browser, with `<space>o` or `Enter`; the file is never fetched or drawn. Remote link URLs must be `https` without credentials or control characters, anything else is refused with a status note.
- **Keys.** `Esc` goes back one level (pops the stack, after clearing a status notice or typed prefix; at the view it does nothing more). `Enter` opens the cursor row (see above); on any other line it loads the next comment page or retries a failed one. `<space>o` opens the cursor row in the browser, or the shown issue (the one on top of the stack, not the list row under it) from a plain line. `<space>p` pushes the parent of the shown issue (from the issue, or from the list row while it loads); with none, or with no issue open, the status line says so. `<space>r` reloads the issue.
- **Loading.** The header shows the fields of the list row at once and `Loading…` in the Description panel until the issue arrives (the breadcrumb also says `Loading…`). The cached issue with the `STALE` state arrives with the issue-detail cache (#122), and `<space>r` reload with refresh (#89). Related data (comments, remote links, epic children) loads separately, each section showing `Loading…` until it arrives or an error line.
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
- Sub-tasks and epics on a board are cards like any issue; the columns layout groups children under their parent (see "Columns layout").
