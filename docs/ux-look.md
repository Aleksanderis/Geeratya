# UX: look (frame, chips, focus, overlays, chrome)

Part of the interface specification; start at [ux.md](ux.md). This is the visual contract for the rendering layer: what a screen looks like, in every colour tier. Behaviour (keys, modes, workflows) stays in the other parts; colours by role and the theme files stay in [ux.md](ux.md), "Colour tiers". Implemented by #524 (frame), #525 (focus and chips), #526 (overlays), #527 (tab row, hints, status bar, palette) and #528 (profile manager); each section below says which. The reference is Gitunia's `docs/internal/ux-look.md` (read-only; the code is rewritten, not copied). Mockups are 80 columns wide and are excerpts: rows are elided where the content repeats (a full screen at 24 rows has a 22-row frame between the tab row and the status bar), accent colour and fills cannot be drawn so each mockup says in its caption or surrounding text what is accent, and the `>` marks the selected row (in the colour tiers the selection is a tint and the marker is drawn in every tier as a second cue). Overlay mockups show the box on a few rows of the muted view, without bars. They show the glyph tier with rounded corners and were checked against the real render of the fake Jira (gap list at the end).

## 1. Principles

- **One frame, split into panes.** The screen between the tab row and the status bar is one box; panes share edges (section 2). No loose boxes with gaps, no title line outside the frame.
- **Titles are chips in the top edge** (section 3). Focus is the box and the chip; the selection marker `>` in the padding column is extra, not the focus cue.
- **Overlays float over a visible view** (section 7). They never erase the view.
- **Quiet by default.** Border, hints and counts are muted; the accent is for the focused box, chips, keys and the selection.
- **Meaning never rests on colour or glyph alone.** Every tier (24-bit, 16-colour, no colour, ASCII glyphs) keeps focus, selection and severity readable (section 8).
- **Not a Git client.** The look borrows Gitunia's chrome (frame, chips, popups, bars) and nothing of its content: no commit graph, lanes, branch labels, hashes or diff colours; the accent is Jira-flavoured (status categories, issue-type colours, keys such as `GEN-5`) and every chip names a Jira thing (column, sprint, issue key and type, comments). Mode badge words, key hints and the palette are shared UX, not Git look.

## 2. Frame (`FrameGrid`; #524)

Implemented (#524): `FrameGrid` (pane rectangles, plain junctions, rounded outer corners, chips written into top edges) draws the board; `PaneFrame` draws the one-column screens (list, backlog, empty states) around their content lines, with shared edges where a section starts; the issue detail document carries its own sides and shared edges (`Panel`) and the renderer adds the top and bottom edge. Differences from the mockups below, kept on purpose: the backlog's top edge carries the source and count (the first sprint's header is the shared edge under the column titles) so that the list and the backlog keep one geometry; the title chips, focus box and quiet borders were added by #526 (section 4); the board's title line moved to the status bar, which gives it what the key hints leave over (the mode badge, hints and colours of the bars are #527); in the detail the first pane's title is the top edge of the frame and a long document shows the title of the pane in view there; the selection marker `>` replaces the first padding cell (the content follows it directly). The start screen and the messages screen are not framed yet.

Every pane is a box and neighbouring boxes share their edge, so the body is one frame divided into panes. A `FrameGrid` (Tui layer, pure geometry plus a draw function; `--print-frame` uses the same) takes the body rectangle and a list of pane rectangles that tile it, and draws each shared edge once with the right junction.

```
 1 Board GCM   2 JQL project = GEN   +                        <space>g jump   <- tab row (bar)
╭─ To Do 5 ────────────────┬─ In Progress 4 ──────────┬─ Done 3 ───────────────╮
│ ┌──────────────────────┐ │ ┌──────────────────────┐ │ ┌────────────────────┐ │
│ │ GCM-8   Epic         │ │ │ GCM-9   Epic         │ │ │ GCM-10  Story      │ │
│ │ Seed: Onboarding     │ │ │ Seed: Reporting      │ │ │ Sign up with email │ │
│ └──────────────────────┘ │ └──────────────────────┘ │ └────────────────────┘ │
│ ┌──────────────────────┐ │ ┌──────────────────────┐ │                        │
│ │ GCM-11  Story        │ │ │ GCM-14  Task         │ │                        │
│ └──────────────────────┘ │ └──────────────────────┘ │                        │
╰──────────────────────────┴──────────────────────────┴────────────────────────╯
 NORMAL  GCM board 24  ·  Connected as Test User 1      ? help  : commands  q quit
```

| Part | Unicode tier | ASCII tier |
|---|---|---|
| Outer corners | `╭ ╮ ╰ ╯` (rounded) | `+` |
| Edges | `─ │` | `- \|` |
| Junctions (T and cross) | `┬ ┴ ├ ┤ ┼` (plain) | `+` |
| Card box (board) | `┌ ┐ └ ┘` | `+` |

- Rounded outer corners, plain junctions. Where two panes meet there is one line, not two, and a junction glyph where the line meets another (also against the outer frame: `┬` on the top edge, `┴` on the bottom, `├ ┤` on the sides).
- The first and last column and the top and bottom row of the body belong to the frame. Content is padded by one column inside each pane (two from 100 columns of width, `PanePadding`, as Gitunia); the selection marker lives in the padding column, never on the border (today the `>` overwrites the left border on the issue detail).
- Cards on the board keep the content and height of `ux-screens.md` ("Columns layout": type, key, assignee initials, summary wrapped to two lines, five rows). Only the border changes to the frame family: plain `┌ ┐ └ ┘`; the selected card keeps its heavier border (`┏ ┓ ┗ ┛`, ASCII `+` with `#` edges), only in the focused column (rule of `ux-screens.md` stays), and gets the tint. The mockups draw shorter cards to fit the page.
- The rows the frame uses are the body: with 24 rows that is 22 (row 0 tab row, rows 1 to 22 frame, row 23 status bar). The line "`GCM board · 24 issues`" that sits above the board today moves into the chrome (the active tab chip names the source; the count goes to the status bar left, section 5). It is not a row of its own.
- Resize: the grid is recomputed from the size; a pane that would get fewer than 3 rows collapses to its title edge; board columns keep the rules of `ux-screens.md` (at least 20 cells wide, below 60 columns exactly one column, the hidden-column marks `‹2` and `1›` move to the ends of the top edge, dim chips). Below 20 columns or 5 rows the body is drawn without a frame, as the content alone.

### Screen layouts

**Board (columns layout).** Panes are the columns, side by side, titles in the top edge (above). The focused column's box is accent. Sprint boards add the sprint name and dates to the status bar left, not a title row.

**Issue detail.** Panes stacked vertically with shared horizontal edges; each pane has its chip (section 3):

```
 1 JQL project = GEN   2 Board GCM   +                       <space>g jump
╭─ GEN-5 · Story ──────────────────────────────────────────────────────────────╮
│ Generated issue 5                                                            │
│                                                                              │
│ Status    To Do ▾                      Priority  Medium ▾                    │
│ Reporter  Generated Reporter           Labels    generated, sample ▾         │
├─ Description ────────────────────────────────────────────────────────────────┤
│ Summary                                                                      │
│                                                                              │
│ This is generated issue 5. It has formatted text, a list and a code block.   │
│ - First generated step                                                       │
│ - Second generated step                                                      │
├─ Comments 50 of 120 ─────────────────────────────────────────────────────────┤
│ Generated Writer 0 · 4 d ago                                                 │
│ Generated comment 1 on issue 5.                                              │
│                                                                              │
│ Generated Writer 1 · 3 d ago                                                 │
│ Generated comment 2 on issue 5.                                              │
╰──────────────────────────────────────────────────────────────────────────────╯
 NORMAL  GEN-5  13%  ·  Connected as Test User 1       ? help  : commands  q quit
```

The panes are the sections the detail already has (header and fields, Description, Comments, then Relations, Attachments and Links when present, in that order). Heights: the header pane is its content height; the others share the rest by content, each at least 3 rows, and the focused pane gets the surplus. The detail stays one scrolling document with one cursor (`ux-screens.md`, "Issue detail"): the panes are how that document is drawn, scroll and the `Top/NN%/Bot` ruler stay document-wide (the status bar shows the ruler), `j`/`k` cross from one pane to the next, and the focused pane is the one holding the cursor. There is no side-by-side layout and no collapse.

**Backlog (Scrum and Kanban).** Panes are the sprint sections and the Backlog section, stacked; each pane's chip carries the sprint name and state, its right-hand chip dates and points:

```
 1 Backlog GEN board   2 Board GEN   +                       <space>g jump
╭─ Sprint 2 · active ── 19 Jan - 1 Feb ── 11 issues · 41 pts ──────────────────╮
│  Key    T Summary                                          Status         Pts│
│> GEN-12 T Generated issue 12                               Backlog          3│
│  GEN-13 B Generated issue 13                               To Do            5│
│  GEN-14 S Generated issue 14                               In Progress      8│
├─ Sprint 3 ── 2 Feb - 15 Feb ── 10 issues · 38 pts ───────────────────────────┤
│  GEN-23 S Generated issue 23                               Done             5│
│  GEN-24 T Generated issue 24                               Backlog          8│
├─ Backlog ── 8 issues ────────────────────────────────────────────────────────┤
│  GEN-31 B Generated issue 31                               To Do            2│
│  GEN-32 S Generated issue 32                               Backlog          3│
╰──────────────────────────────────────────────────────────────────────────────╯
 NORMAL  GEN board 29  ·  Connected as Test User 1      ? help  : commands  q quit
```

The sprint goal is the pane's first content line (`Goal: ...`, muted) when set. The column header line (`Key T Summary ...`) is shown once in the top pane's first content row and repeats nowhere. A section collapsed with `za` stays a pane that is only its title edge, with `▸` (ASCII `>`) before the chip text; `j`/`k` step over all sections as in `ux-screens.md`.

**List (JQL and filters).** One pane; chip: the source (`project = GCM`), right-hand chip the count `40 issues`. Header row inside the pane as in the backlog.

**Start and empty screens.** The centred text stays; it sits inside the frame with the chip of the profile (`Geeratya`).

## 3. Title chips (#525)

A chip is the text of a title, written into a top edge as `─ text ─` (one `─` of the border before and after, and a space inside each side). The edge continues as border on both sides. A chip never contains more than one noun and one count; the text is truncated with `…` from the right when the pane is narrower than the chip plus 4 columns, and dropped (plain edge) under 8 columns.

| Screen | Pane | Chip text | Right-hand chip (only when it fits) |
|---|---|---|---|
| Board | column | `To Do 5` (name, issue count, `/limit` when the column has a WIP limit: `In Progress 4/3`, over the limit in the `warn` role) | none |
| Board (Scrum) | column | as above; `Done 3 · 12 pts` when the board estimates in points | |
| Issue detail | header | `GEN-5 · Story` (key bold, type in the issue-type colour) | `In Progress` (status chip in its category colour) |
| Issue detail | other panes | `Description`, `Comments 50 of 120`, `Relations 4`, `Attachments 2`, `Links 3` | none |
| Backlog | sprint | `Sprint 2 · active` | `19 Jan - 1 Feb · 11 issues · 41 pts` |
| Backlog | backlog | `Backlog` | `8 issues` |
| List | list | the source (`project = GCM`, filter name) | `40 issues` |
| Overlay | box | its title (section 7), always the accent chip | optional right-hand count (`12/87` in the palette) |

Chip kinds: the **focused** pane's chip is the filled accent chip (accent background, chip text colour, bold); every other chip is a dim chip (muted text on the page background, no fill). Right-hand chips are always dim. Colour inside a chip text (issue-type colour, status colour) is used only in the dim state and in the focused state turns into the chip text colour, so a filled chip has one foreground.

## 4. Focus and selection (#525)

- **Focused pane**: the whole box (the pane's own edges, shared edges included on the sides that belong to it) is drawn in the accent colour, bold; its chip is the filled accent chip. Every other pane's lines are the quiet border colour with dim chips. A shared edge between a focused and a quiet pane is drawn in the accent colour (the focused pane wins).
- **Selected row/card**: a background tint (the theme's `selection` role), never inverse video, so the row keeps its colours. The focused pane's selection is the full tint, other panes' selections a quieter tint (`SelectedDim`: the selection tint moved halfway to the page, derived, not themed). On the board an unfocused column marks the card the cursor would land on when moving back (the remembered column row, `Wanted` clamped to the column), so the user finds their place; the other screens have one selection and show no quiet one. The selected card on the board also has an accent border (heavy glyphs, accent, bold) while its column is focused. Implemented by #526 (`TextRole.Border`, `FrameFocus`, `ChipFocus`, `SelectedDim`; chips restyled by `ChipStyle`; `FrameGrid` takes the focused pane).
- **Visual range** (#568): the rows of a range are drawn stronger than the selected row, with or without pane focus: the `Range` role is the selection tint moved towards the accent (the step shrinks per theme until the text, moved towards black or white, keeps contrast 4.5, or 7 on a high-contrast theme), bold in every tier; at 16 colours reverse video of the page (black on gray, white on black on a light theme) instead of the selection's blue; without colour reversed and bold. The row under the cursor (the moving end) is underlined in every tier, the other rows of the range are not. The `+` in the second cell stays (`*` for a marked row, which keeps the range look too). List and backlog rows are built from the row's own spans over the tint (`SpanGround.Selection`, `Range`, `Cursor`), so a selected or in-range row keeps its status and issue-type colours wherever they reach contrast 3.0 on the tint (else the tint's text colour); the no-colour tier reverses the whole row. The type marker `B S T E s` of list and backlog rows has the issue-type colour, as the board cards' type word.
- **No colour** (`NO_COLOR`): the focused box is bold, its chip reversed-bold, the selected row reversed; quiet panes are plain and their chips plain. The marker `>` in the padding column marks the selected row in every tier (the renderers do not know the colour tier; never over the border). The quiet selection without colour is underlined.
- **Focus never relies on colour alone**: besides colour there is always boldness (box), fill (chip) and, without colour, reversal. A test checks each tier.
- **One focus.** Exactly one pane has focus in a tab; an overlay takes focus while open and gives it back unchanged on close (a closed overlay never moves the selection).

### Pane navigation (no new keys)

Moving between panes uses the keys the views already have, so `ux-keymap.md` does not change: on the board `h`/`l` move to the previous/next column; in the issue detail and the backlog the chords `] ]` and `[ [` (`detail.section-next`, `detail.section-previous`) jump to the next and previous pane; `j`/`k` move the cursor and cross pane edges, so the focused pane follows the cursor. A click focuses the pane it lands in by placing the cursor there (the existing column, row and line targets; no pane-focus target of its own, so `ux-mouse.md` is unchanged). The hints `]] pane` (detail and backlog, last of the screen hints) name the chord. The wheel scrolls the document as today.

## 5. Chrome: tab row, status bar, key hints (#527)

Both bars use the **bar background** (`barBackground` role), one step lighter than the page on a dark theme and darker on a light one (contrast against the page base at least 1.15:1; a theme that does not reach it is nudged, like the selection step). They are a full row each, exactly the terminal width, never wrapped.

**Tab row** (row 0):

```
 1 Board GCM   2 JQL project = GEN   3 Backlog GEN board   +            <space>g jump
 ^^^^^^^^^^^^^ filled accent chip, bold; the rest dim chips on the bar
```

- One chip per tab: ` <number> <kind> <name> `, the active tab the filled accent chip (bold), others muted text on the bar. Kind words are Jira nouns (`Board`, `Backlog`, `JQL`, `Filter`, `Issue`); a tab label is at most 24 columns, truncated with `…`.
- After the last chip a `+` target (opens the source picker, same as `<space>t`). The right end keeps the `jump` chip (`JumpChip`, still a click target: opens the jump prompt), now drawn as the dim chip `<space>g jump`. The close affordance (new mouse target) `x` shows on the active chip when the width is 80 or more (` 1 Board GCM x `), and on a hovered chip; middle-click closes any chip.
- Mouse targets (`ux-mouse.md`, tab targets): a chip switches, `x` closes (at once, like `tab.close`), `+` opens the picker; the targets are exactly the cells the chip occupies (the `x` target is the `x` and the pad after it).
- Overflow: the active chip is always visible; chips that do not fit are hidden behind the dim `‹n` and `+n ›` chips (click steps to the neighbouring hidden tab). Under 40 columns only the active chip and `+` remain. Not done in #527, registered as #547: middle-click close, `x` on hover, and the `+N` chip opening a tab list in the picker.
- ASCII tier: chips are bracketed, `[1 Board GCM]`: the padding cells of the chip become the brackets, so a chip keeps its width and its mouse cells. No-colour tier: the bar is reversed and the active chip is bold without reverse, so it reads as the one normal-video chip on the reversed bar (a reversed chip on a reversed bar would vanish).

**Status bar** (last row):

```
 NORMAL  GCM board 24 · Connected as Test User 1       ? help  : commands  q quit
 ^^^^^^^ mode badge       ^ message / state                  ^ hint chips
```

- Column 0: a one-space-padded **mode badge** (` NORMAL `, then one more space before the text) as a filled chip, bold: `NORMAL` accent, `VISUAL` the `remote` role, `INSERT` `good`, `COMMAND` `warn`, `CONFIRM` `bad`, `HELP` `info`. Text on a badge is the theme's chip text colour. Without colour the badge is reversed-bold and the word tells the mode.
- Then the message or state text: the pending keys in Normal mode; otherwise the message of the status history, coloured by severity (info normal text, warning `warn`, error `bad` bold, success `good`; a message always carries a word as well, `Failed:`, `Warning:`, so colour is not the only carrier). When there is none: the cursor position (#569: `5/40` in a list or backlog, `col 2/4  card 3/7` on a board, counted over the loaded rows with a `+` after the total while more pages exist; nothing with no row to stand on), the source and its count (`GCM board 24`, sprint name for a sprint board, `GEN-5 13%` in the detail), then the connection state (`Connected as ...`, `Offline`, `Rate limited, retry in 12 s`). The network word stays first. The position is shown last in priority: it appears only when the line, the screen hints and the global hints all still fit, and on a board it takes cells from the board text (which is cut with an ellipsis) before a hint is dropped; on a short line it is the first part to go.
- **Key hints** (right side, `HintBar`): chips `key label`, the key bold in the key role of `ux.md` (`warn`; the accent stays for focus), the label muted, two spaces between chips, right-aligned, within the keymap rules of `ux.md`, "Hints". When the width is short, chips drop from the right end towards the left (the global `? help  : commands  q quit` are dropped last and never partly). In the other modes the same bar shows that mode's hints (`Tab/Shift-Tab field  Ctrl-s save  Esc cancel`).
- Whole-line fallbacks: 16-colour uses the bar as white-on-blue as today with bold keys; no colour: the bar is reversed.

## 6. Colour and glyph tiers (all sections)

| Element | 24-bit | 16-colour | No colour (`NO_COLOR`) | ASCII glyphs (classic consoles; combinable with any colour tier) |
|---|---|---|---|---|
| Focused box | accent, bold | bright cyan (dark) or blue (light), bold | bold | same as the colour tier |
| Quiet box | `border` role | bright black / gray | plain | `+ - \|` |
| Focused chip | accent fill, chip text | cyan/blue fill, black text, bold | reversed, bold | plain `- text -` in an edge, bracketed only in the bars (section 10) |
| Dim chip | muted on page | bright black on default | plain | plain, muted |
| Selected row | `selection` tint | white on blue (focused), underlined (unfocused, as in Gitunia) | reversed + `>` in the padding column (unfocused: underlined) | `>` marker |
| Visual range row | `selection` tint moved towards the accent, bold, own colours kept | reverse video (black on gray; white on black on a light theme), bold | reversed, bold, `+` in the second cell (same reversal as the selected row, the `+` and the underline on the cursor row tell them apart) | `+` marker |
| Bars | `barBackground` | white on blue as today | reversed | unchanged |
| Overlay box | accent frame, the theme's own page colour as fill | bright cyan frame, default background | bold frame | `+ - \|` frame |
| View behind an overlay | muted and darker (the page background moved 60 percent towards black, 15 percent on a light theme, the scrim; foreground blended about 30 percent towards the scrim, lowered until the muted text keeps contrast 3.0; cells with a fill of their own, such as the selected row, chips and the bars, keep it) | faint (SGR 2) on terminals that are not classic consoles, else bright black | unchanged | unchanged |
| Confirm frame | `bad` for destructive, `warn` for other | red / yellow | bold | as above |

Contrast: chip text on the accent fill, text on the bar background, the quiet border (3.0) and the dimmed view (muted text on the page, 3.0 as a floor so the view stays readable) are covered by the existing role contrast tests plus new roles tested in all built-in themes.

## 7. Overlays (#526)

Every overlay is one component, `OverlayBox`, with the same look; palette, help, confirm, forms, pickers and the profile screens differ in content only.

- **Placement.** A floating box centred horizontally; vertically centred, except the palette and the pickers, which are anchored 2 rows below the top of the body so their height does not jump while filtering (they keep the height of the unfiltered list, up to their limit).
- **Over a visible view.** Only the box's cells are drawn; the rest of the screen keeps the view, but muted (table in section 6). It is never erased: today rows are blanked left and right of the box. The tab row and status bar stay (the status bar shows the overlay's mode and hints).
- **Frame.** Rounded accent frame, bold; the title is the filled accent chip in the top edge (left aligned, one `─` in). The interior has the **surface** background: the theme's own page colour (or the optional `overlayBackground` of the theme file), so the box shows the palette as published and the dimmed view around it is the same hue only darker, never a second gray (#559; the first version filled the box with a tint of `selection`, which read as a gray step on the page, contrast against the scrim at least 1.15:1 is tested); in the no-colour tier the frame is bold and the box cells are cleared, nothing else changes.
- **Size limits.** Width: content width plus 4, at most 100 columns and at least 30 where the terminal is 34 columns or wider, never more than the terminal width minus 4. Height: content plus 2, at most the body height minus 2, at most 18 for the palette and pickers. The box shrinks to content (a confirm is as wide as its longest line up to 62). Long content scrolls inside the box with a `▲`/`▼` marker (ASCII `^`/`v`) in the right padding column.
- **Too small.** Below 34 columns or 8 rows the overlay is drawn without a frame filling the body (title as the first line, hints on the status bar), as the repository picker of Gitunia does; below 12 columns it shows only the title.
- **Mouse.** The area outside the box is the **Backdrop**: a click on it closes the overlay as `Esc` does (confirm: cancels like `n`, as today; any form or editor with typed text asks first, as `Esc` does; the tab row and the status bar are not Backdrop, a click there works as always, and the overlay closes first only for a tab click). Clicks inside the box that hit no row do nothing. Rows and chips inside are mouse targets as today (`ux-mouse.md`); the Backdrop target replaces the "click outside" rules listed per overlay there (#526 updates them).
- **Footer.** The last interior row is a hint line of dim chips (`Up/Down move  Enter run  Esc cancel`), separated from the rows by a `─` rule across the box; the same hints also show on the status bar.
- **Selected row** in lists: the focus tint, with the marker `>` in the first padding column (every tier).

**As implemented by #525.** One `OverlayBox` (rounded accent frame, title as the filled accent chip, `Backdrop` for the dimmed view, `Over` to splice the box into the view rows, `Cover` for the mouse box) draws the field form, comment editor, suggestion rows, palette, help, confirm, jump, source picker, context menu, profile list and profile form, and the which-key popup (frame and chip only: it keeps the view undimmed and flush right, as no Backdrop is specified for it). The view is `SpanGround.Dim` (24-bit: text blended towards `Theme.ScrimBackground` by `Theme.DimAmount`; 16 colours: faint, or bright black when the glyph set is ASCII, which marks a classic console; no colour: unchanged), the box is `SpanGround.Surface` (24-bit: `Theme.Surface`, the page colour unless the theme sets `overlayBackground`; the view is `Theme.ScrimBackground`; other tiers keep the default background); a box under another overlay is both. Confirm frames use `bad` for requests marked `Destructive` (delete issue, delete comment, remove profile), `warn` otherwise and the accent for notices. A click on the body outside the reported box is the Backdrop. Deviations kept on purpose or left open (#539 closed the rest): the form, editor and picker keep their present widths (up to the limits above) instead of shrinking to content, so a box does not change width while the user types (only the confirm shrinks, to its longest line, at least 30 and at most 62 columns); the palette keeps the height of its unfiltered list (up to 18 rows) while filtering; the form and the comment editor stay anchored one row below the top of the body (their height changes while suggestions open, so centring would move the field being typed in); the source picker and jump prompt are capped at 18 rows (the profile list is left to #535); footers of the form, editor, palette, picker and jump are hint chips (key bold accent, label muted, `OverlayBox.Hints`), those of the profile list and form follow with #535; the palette, picker and jump show `▲`/`▼` (ASCII `^`/`v`) in the right padding column when rows are hidden above or below (the help pane and the form do not scroll rows yet); confirm frames use `bad` for requests marked `Destructive` (deletes, removing a profile, side-effecting transitions, completing a sprint, moving several issues to a sprint) and the chip names the action and target (`Move GCM-8`; `ConfirmRequest.Heading`).

Mockups below show the box on a few rows of the muted view.

### Command palette (`:`; #527)

```
 1 JQL project = GCM   +                                     <space>g jump
╭─ project = GCM ─ 24 issues ──────────────────────────────────────────────────╮
│ GCM-8   E Seed: Onboarding flow                         Backlog              │
│ GCM-9  ╭─ Commands ───────────────────────────────────────── 12/87 ─╮        │
│ GCM-10 │ : rank_                                                    │        │
│ GCM-11 ├────────────────────────────────────────────────────────────┤        │
│ GCM-12 │ Issue                                                      │        │
│ GCM-13 │ > Rank the issue up (a count moves it that far)      Alt-k │        │
│ GCM-14 │   Rank the issue down                                Alt-j │        │
│ GCM-15 │ Backlog                                                    │        │
│ GCM-16 │   Rank the issue to the top of the backlog                 │        │
│ GCM-17 ├────────────────────────────────────────────────────────────┤        │
│ GCM-18 │ Up/Down move  Enter run  Esc cancel                        │        │
│ GCM-19 ╰────────────────────────────────────────────────────────────╯        │
╰──────────────────────────────────────────────────────────────────────────────╯
 COMMAND :rank                             Up/Down move  Enter run  Esc cancel
```

- Title chip `Commands`; right-hand dim chip `shown/total` in the top edge. Query line directly under it: the prompt `:` in the accent colour, the typed text, a caret. A rule below the query line.
- Rows are grouped; each group has a **subheader** line (the group name, accent, bold, not selectable, not counted; shown only while the filter matches rows of the group). A row: title (plain), then the main key right-aligned in the muted accent (`<space>im`), nothing when unbound. Disabled actions (not applicable to the current view) are muted and skipped by selection. A title that does not fit is truncated with `…`, never the key.
- The selected row is the tint; `Enter` runs it. A typed command line (`:jql ...`, `:board GEN`) shows its usage line as the single row, and `Enter` runs the command (unchanged behaviour, `ux.md` "Command palette").
- No match: one muted row `No command matches "rank"`.

### Help (`?`)

```
╭─ Help ────────────────────────────────────────────────────────── / filter ───╮
│ > List        │  j          Move down  (Down)                                │
│   Global      │  k          Move up  (Up)                                    │
│   All actions │  gg         Go to the top  (Home)                            │
│   Commands    │  <space>a   Open the actions menu of the selected issue      │
│               │  <space>ie  Edit fields of the issue (summary, labels,       │
│               │             priority, description)                           │
│               │  <space>o   Open the issue in the browser                    │
│ ──────────────┴────────────────────────────────────────────────────────────  │
│ j section  J scroll  / filter  Esc close                                     │
╰──────────────────────────────────────────────────────────────────────────────╯
```

Inside the box the page menu and the page text are two panes split by one plain `│` junction (`┴` in the footer rule). The active page is the tint row (accent text, bold); the keys column is the accent role. Everything else as in the general rules; sized up to 96 columns.

### Confirm

```
          ╭─ Move GCM-8 ─────────────────────────────────────────╮
          │ Move GCM-8 to Done?                                  │
          │                                                      │
          │ Jira cannot undo this; the workflow may do more      │
          │ than change the status.                              │
          │                                                      │
          │  y  Move issue        n  Cancel                      │
          ╰──────────────────────────────────────────────────────╯
```

The frame and chip use the `bad` role for destructive actions (delete, bulk edits, workflow-firing transitions, assignee or sprint changes for many issues) and `warn` for the rest; the chip names the action and its target, the body says exactly what will change and that Jira has no undo (`CLAUDE.md`). Answers are two chips on one line (`y`/`n` keys bold, label plain; the default `n` has no tint, `y` never does), both clickable. Wider than 62 columns the text wraps.

### Field form (edit, assign, labels, comment editor, link form) and suggestions

```
      ╭─ Edit GEN-1 · Labels ───────────────────────────────────────╮
      │ Labels                                                      │
      │ ┌─────────────────────────────────────────────────────────┐ │
      │ │ urgent_                                                 │ │
      │ └─────────────────────────────────────────────────────────┘ │
      │   > urgent (new)                                            │
      │     geeratya-seed                                           │
      ├─────────────────────────────────────────────────────────────┤
      │ Down/Up choose  Tab/Enter accept  Ctrl-s save  Esc cancel   │
      ╰─────────────────────────────────────────────────────────────╯
```

Field label above its input; the focused field's input box is accent (the others quiet, one-line plain), the field's label muted/bold. Suggestions are rows under the input (not a second box); the selection is the tint; a validation error is a `bad` line under the field. Multi-field forms stack the fields, `Tab`/`Shift-Tab` move the focus (accent input box follows). The comment editor is the same box with a taller input pane (at most body height minus 6).

### Pickers (source picker `<space>t`, jump `<space>g`, status, assignee, sprint)

```
  ╭─ Open a view ──────────────────────────────────────────── 12 ──╮
  │ > _                                                            │
  │ ────────────────────────────────────────────────────────────── │
  │ Boards                                                         │
  │ > Board   GCM board (kanban, GCM)                              │
  │   Backlog GCM board (kanban, GCM)                              │
  │ Filters                                                        │
  │   Filter  Filter for GCM board (Test User 1)                   │
  │ Recent                                                         │
  │   Recent  project = GCM                                        │
  │ ────────────────────────────────────────────────────────────── │
  │ Up/Down choose  Enter open in a new tab  Esc close             │
  ╰────────────────────────────────────────────────────────────────╯
```

Same anatomy as the palette: query line, subheaders (`Boards`, `Filters`, `Recent`), rows with a kind column in the muted role, tint selection, footer hints, count chip.

### Profile manager and profile form (#528)

The manager is a full-body screen, not an overlay (it is also the first-run screen when there is no profile): a frame with two panes, the list and the details of the highlighted profile.

```
 Profiles                                                      <space>g jump
╭─ Profiles 2 ───────────────────┬─ Details ───────────────────────────────────╮
│ ● work       me@acme.com       │ Site     https://acme.atlassian.net         │
│ > personal   me@home.net       │ Account  me@home.net                        │
│                                │ Token    OS secret store (stored)           │
│                                │ Status   Connected as Me (checked 2 min ago)│
│                                │                                             │
│                                │                                             │
╰────────────────────────────────┴─────────────────────────────────────────────╯
 NORMAL  * active    Enter switch  Ctrl-x remove  Esc close   (more keys: #528)
```

`●` (ASCII `*`) marks the active profile, in the `good` role. The tab row shows a single `Profiles` chip while the screen is open (it is not a tab of the active site; it can be opened without a connection). The details pane never shows a token, only its source (`OS secret store`, `env GEERATYA_TOKEN`, `not set`). Keys are proposed here; #528 settled them (see the end of this section). Remove asks (confirm box above: `Remove profile "personal"? Deletes the entry and its stored token. Jira data is untouched.`).

The add and edit form is one overlay with labelled fields (a form box as above, size 56 to 72 columns):

```
        ╭─ Edit profile · personal ────────────────────────────────╮
        │ Site URL      https://home.atlassian.net_                │
        │ Account       me@home.net                                │
        │ Token source  ( ) OS secret store   (●) Env var  GERA... │
        │ Token         ●●●●●●●●  kept (type to replace)           │
        │                                                          │
        │  Test connection    Save    Cancel                       │
        │ Connected as Me. Save to keep it.                        │
        ├──────────────────────────────────────────────────────────┤
        │ Tab field  Ctrl-t test  Ctrl-s save  Esc cancel          │
        ╰──────────────────────────────────────────────────────────╯
```

**As implemented by #528.** The manager is the profile list overlay (the existing popup component) with two buttons, Add and Edit, and its keys are `Enter` switch, `Ctrl-a` add, `Ctrl-e` edit, `Ctrl-x` remove, `Esc` close. The form shows the four fields at once (Name, Site URL, Account, Token) with `>` on the focused row, a note line (validation as you type, or the connection test result), the buttons `Test connection`, `Save`, `Cancel` and the keys `Tab` field, `Ctrl-s` save, `Alt-t` test (`Ctrl-t` is the picker's open-in-new-tab key), `Esc` cancel. The token source row is not shown: the token is stored in the OS secret store, and `GEERATYA_TOKEN` serves only a single profile without a store (`ux.md`, "Profiles"), so there is nothing per profile to choose. **As implemented by #535.** The manager is a full-body screen (`ProfileManagerScreen`, one `FrameGrid` with a list pane of 40% of the width, 26 to 44 columns, and a details pane; under 60 columns the details pane is left out). The details are Name, Site, Account, Token (the source only, looked up by the host through `LoadTokenSourcesEffect`; `looking up...` until it answers) and Status (the connection line for the active profile, `not checked` for others, so no request goes to an inactive site). The tab row shows one `Profiles` chip (no jump chip: it is not a tab of a site), the status line shows the notice or the active profile's connection, and the Add and Edit buttons sit in the last row of the list pane. The form and the remove question draw over it (dimmed) and return to it. The active mark is `●` (`*` in the ASCII tier). With no profile the same screen is shown at start with an empty list.

Labels are a fixed-width muted column; the focused field's value is accent with the cursor; validation (URL shape, email shape) is a `bad` note at the end of the field's line while typing; the buttons are chips (the focused one filled accent) and are mouse targets; the result line is `good` or `bad` with a word (`Connected`, `Failed: 401`). Editing never shows an existing token; "kept" means the stored one stays unless the field is typed in.

## 8. Differences from Gitunia, and why

| Gitunia | Geeratya | Why |
|---|---|---|
| Fixed pane set (sidebar, history, detail, diff) | Panes per screen: columns, sections, sprints | Jira screens differ per view; the grid is the same |
| Sidebar chip shows the repository name | Tab chip names the source; count in the status bar | Several sources per site, no repository |
| Overlay is erased background with a frame in some places | Overlay always over a muted visible view, one component | Owner intent: see the view behind; fewer variants |
| Mode badge only in the status bar | Same, plus overlays keep the badge | Same |
| Pane resize keys, drag | None | Layouts are content-driven; no resize keys are in the keymap |
| Branch and commit colours, graph lanes | Issue-type and status-category colours | Must not look like a Git client |
| Theme roles for the graph | Roles unchanged; new roles `surface`, `dim view` derived, not themed by file | Existing theme files keep working |
| Repository picker overlay | Source picker and profile manager | Jira nouns |

## 9. What the render shows today (gap list)

From real captures of the debug binary against the fake Jira at 80x24 (scenarios board, detail, form with suggestions, palette, help, confirm, backlog, picker, profiles, profile add), 2026-11-03:

| Screen | Today | Spec section | Issue |
|---|---|---|---|
| Board | Done in #524: one rounded frame, shared edges, column chips in the top edge, the title line is in the status bar. Done in #526: the focused column's whole box and chip are accent, others quiet with dim chips, the cursor card has an accent heavy box, other columns mark their landing card with the quiet tint | 2, 3, 4 | #524, #526 |
| Detail | Done in #524: one frame with shared edges, the cursor marker sits inside the border, the top edge names the pane in view. Done in #526: the pane holding the cursor is accent (shared edges included) with a filled chip, others quiet with dim chips. Done in #557: the status chip on the right of the header, in its category colour | 2, 4 | #524, #526, #557 |
| Backlog and list | Done in #524: one frame, sprint headers are shared edges with chips (a collapsed one marked `▸`), the top edge names the source and the count. Done in #526: the section holding the cursor is the focused pane (accent box, filled chip), others quiet with dim chips | 2, 3, 4 | #524, #526 |
| Tab row | Done in #527: chips on the bar background, filled accent for the active one, `x`, ` + `, dim jump chip with the keys. Open: middle-click, hover `x`, `+N` tab list (#547) | 5 | #527, #547 |
| Status bar | Done in #527: mode badge as a filled chip at column 0, message in its severity role on the bar, hint chips (key bold, label muted). The one-line `Confirm  (y/n)` and form texts keep their wording | 5 | #527 |
| Palette | Done in #525: rounded accent frame and chip over the dimmed view. Done in #548: dim count chip `shown/total` in the top edge (role `OverlayCount`, muted, no fill), query line (`:` accent, text, caret), joined rules, footer line `Up/Down move  Enter run  Esc cancel` (dropped under 12 box rows), accent bold subheaders, tinted selected row; the `>` marker stays in every tier (as in the other lists) | 7 | #525, #548 |
| Help | Done in #525: rounded accent frame over the dimmed view | 7 | #525 |
| Confirm | Done in #525: rounded frame (`bad`, `warn` or accent), view dimmed around it. Done in #539: shrinks to content, `bad` for side-effecting transitions, sprint completion and multi-issue sprint moves, chip names action and target | 7 | #525, #539 |
| Forms and pickers | Done in #525: rounded accent frame, centred, view dimmed around it, suggestion rows inside the form box. Done in #539: hint chips in the footer, `▲`/`▼` scroll markers and the 18-row cap (picker, jump); the mouse wheel does not scroll the picker rows yet | 7 | #525 |
| Settings | Done in #556: two-pane full-body screen like the profile manager (categories 20 columns, rows, help row, hint row), a `Settings` chip in the tab row, the open choice list under its row; under 60 columns one list with category headings | 7 (settings) | #556 |
| Profiles | Done in #535: two-pane full-body manager with details pane and the `Profiles` chip; the form and the remove question draw over it | 7 (profile manager) | #528 (form and keys), #535 (two-pane screen) |

## 10. Reconciliation with the other parts, and what stays open

The look changes how screens are drawn, not what they do. Where this part and an older paragraph differ, this part wins for drawing and the older part for behaviour:

- **`ux-screens.md`**: the board's title line, the tab bar rows and the "Detail" box layout are replaced by sections 2 and 5 here (the paragraphs are updated by #524 and #527 when they land); cards, column widths, scroll and cursor rules stay as written there (see section 2). The detail is one document with one cursor.
- **Pane keys**: there are none beyond the existing ones (`h`/`l`, `] ]`, `[ [`). `ux.md` "Not adopted from Gitunia" ("pane focus keys, no panes to resize") stays true: no resize keys, no direct pane-focus keys. "Focus model" stays: one focus place, the overlay or the body; the focused pane inside the body is a drawing detail of the cursor.
- **Profile manager and form (#528).** Settled by #528: the keys (no bare letters, `ux-keymap.md` rule; `Ctrl-x` remove, `Enter` and click switch stay, `Ctrl-a` add, `Ctrl-e` edit, `Alt-t` test), a Name field (kept, editable, unique), and no token source row in the form (see the section above). The two-pane screen and its status line (only what is already known for the active profile) were delivered by #535, which also made the manager the first screen when there is no profile.
- **Mouse** (`ux-mouse.md`): new targets are listed here and added there by the implementing issue: Backdrop (#526, replaces the per-overlay "click outside" lines), tab close `x`, middle-click close and the `+N` overflow chip (#527).
- **Network banner and stale row, issue-stack breadcrumb, which-key popup** (not placed above): the banner stays one row directly under the tab row (it shifts the frame down by one row); the breadcrumb (`GCM board › GCM-12`) goes to the status bar left in the detail, before the ruler; the which-key popup is an `OverlayBox` anchored at the bottom right without a Backdrop (it closes on any key as today). #525 and #527 may refine these.
- **Mode badges**: `MESSAGES` (the messages overlay) uses the `info` role like `HELP`.
- **Destructive vs warn confirm frame**: `bad` for the destructive list in `CLAUDE.md` (delete, bulk edits, many-issue assignee or sprint changes) and for a transition the workflow model marks as side-effecting when it is known; `warn` otherwise. How a transition is known to fire side effects is not decided here; until it is, a transition uses `warn`.
- **Palette**: the mockup shows an advanced command; by default the list holds the everyday level only (`ux-keymap.md` rule 4).
- **ASCII tier**: a quiet pane edge is `+--- To Do 5 ---+`: the chip text sits in a plain `- text -` edge; the filled accent chip is the same with bold or reversed text (`NO_COLOR`), and the glyph tier brackets chips only in the status and tab bars (` [1 Board GCM] `), never in a frame edge, so edges stay unambiguous.
