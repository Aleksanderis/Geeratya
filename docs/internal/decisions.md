# Decisions

*Internal design note for maintainers and agents. Not needed to use or contribute to Geeratya.*

Short dated log of live, non-obvious settled choices that cut across docs. Add an entry only when the choice is not already evident from `ux.md` and its parts (`ux-*.md`), `architecture.md` or the code, or when it overrides an earlier decision; a behaviour change documented in `ux.md` needs none. Agents follow these without asking. Open questions live in `roadmap.md`. Superseded, closed and purely historical entries are archived word for word in `history/decisions.md` (not required reading).

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Jira layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. Long detail lives in `docs/internal/history/`, the area docs or the issue or PR. When a decision changes, append a new entry that supersedes the old one, and archive the old entry once the new one states the outcome.

## 2026-11-02

**UI:** Create (#162): project and parent come from the issue shown or selected (a view has no project of its own), and the type picker is part of the create form, not an overlay. Why: least new state; the create metadata decides what is allowed. Details: `ux-workflows.md`, "Design details".

**UI:** The assignee picker is the field form with one person field, and the edit form offers only fields whose current value the view holds. Why: one state machine for every edit; writing a blind value over an unseen one loses data. Details: `ux-workflows.md`, "Design details".

**UI:** Comment writes (#167) wait for the reply and skip the row patch (`WriteRequest.ChangesRows`); another author's comment is not refused up front, Jira's 403 decides (overrides `ux-keymap.md`). Why: admins may edit all comments. Details: `ux-workflows.md`, "Design details".

**UI:** Write keys: one `<space>` group per noun, never a prefix of an M1 leaf, no bare letters; only sprint moves and rank steps take a set. Why: Gitunia redesigned its keymap late for the same reasons. Plan: `ux-keymap.md` (#152); Details: `ux-workflows.md`, "Design details".

**Jira layer:** `BrowserUrlCheck` (Core) is the single gate before anything reaches the system browser (https only; `RemoteLinkMapper` keeps any scheme, #118). Why: one gate for every open path. Details: `architecture.md`, "Design details".

**Scope:** Scrum boards and sprints are in (milestone M5, ahead of polish), in both project kinds; Scrum reports and charts are not. Why: Scrum is common and the tool is not only for the owner's team. Dev site: `GCS` (company-managed, board 6) and `GTS` (team-managed, board 5) are sandbox projects like `GCM` and `GTM`.

**Jira layer:** Project configuration (types, fields, required fields, allowed values, transitions, link types) is read per connection and project, never hard-coded; unsupported field types degrade to read-only with a browser link. Why: every site and project differs. Rule: `.claude/rules/jira-layer.md`.

**UI:** Suggestions: `@` opens people, `#` plus a project key opens issues (inside text editors, Neovim-style popup); picker fields (labels, components, assignee, selects) suggest by context as you type, fzf/zoxide `zi` style. Why: owner's choice; issues #213-#215.

**Process:** Milestones after M2 follow what Gitunia had to redo, not its order (write pipeline and `Geeratya.App` first). Why: Gitunia's keymap, effects and theme rework. Details: `roadmap.md`, "Milestone order rationale".

**UI:** The columns layout groups children under their parent card and names the parent on a child whose parent is in another column; no swimlanes (overrides #192, owner feedback on PR #143). Details: `ux-workflows.md`, "Design details".

## 2026-11-01

**Stack:** Same as Gitunia: C# on .NET 10 with Native AOT, Terminal.Gui v2, xUnit, JetBrains inspectcode. Why: the owner is a .NET developer, and the agent routines, style rules and tooling carry over.

**Process:** The agent routines (CLAUDE.md, reviewer agent, rules, the work-task, work-orchestrator, work-readiness, retrospective, testing, performance and screenshots skills) are ported from Gitunia and adapted to Jira. Ideas carry over freely; Gitunia code is rewritten, not copied.

**Jira layer:** Jira Cloud only, REST API v3 over `HttpClient`, Basic auth with email plus API token (Cloud has no Data Center style PATs). Why: the owner's target is Cloud.

**Testing:** Live Jira work happens only on the owner's free dev site, guarded by `GEERATYA_LIVE_HOSTS`; the company site is never used by agents or tests. Default tests are offline against recorded, scrubbed fixtures.

**Process:** CI never runs automatically; local build and tests are the gate, and CI is dispatched by hand (before closing a milestone). Why: CI minutes are scarce (carried over from Gitunia).

**Architecture:** One active Jira site at a time, from saved profiles; switching is cheap and built in, several sites open at once is not included. Why: the owner uses one site, and side-by-side sites cost the most.

**Scope:** Company-managed and team-managed projects are both supported; Jira Service Management is not included. Attachments and images show as links, never rendered. Why: the owner's company project kind is unknown and the owner does not use JSM.

**Architecture:** A view is a source (JQL, filter, board, backlog) plus a layout (list, columns). Why: the owner wants boards flexible; new board kinds are then a new source or layout.

**UI:** The keymap is `keymap.default.json` embedded in Core and read with source-generated System.Text.Json; `docs/keymap.md` is generated from it by `--print-keymap-doc` through `scripts/gen-keymap-doc.sh` (no separate generator tool). Why: one code path renders help and the reference, and a test compares the committed file.

**Stack:** Secret store: `secret-tool` on Linux (token on stdin), advapi32 Credential Manager on Windows, no store on macOS yet (`GEERATYA_TOKEN`). The store is tried first, the variable is the fallback. Why: no native library dependency, AOT-safe, and a keychain binding can come later.


## 2026-11-03 (code structure rules, #580)

**Code structure:** Terminal column width lives in Core `DisplayWidth`; its wide-character table (`WideRanges`, East Asian Width W/F plus Emoji_Presentation, Unicode 17.0.0) is generated by `scripts/gen-display-width.py` and committed as a constant span (no runtime download, no startup cost, no dependency). Rerun the script to adopt a new Unicode version.

**Jira layer:** One `JiraTransport` per `JiraConnection`; clients take only the transport (supersedes "every call takes a `JiraConnection`"; the "no global site" intent stands). Why: token and `HttpClient` live in one place. Details: `architecture.md`, "Design details".

**Stack:** The `GEERATYA_TOKEN` fallback applies only when exactly one profile exists; with several profiles a profile needs a stored token, and the status line says so (refines the entry on the secret store; issue #60). Why: the variable carries no site, so a profile switch would send one site's token to another host.

**UI:** M1 view commands and the source picker always open a new tab; `Tab`/`Shift-Tab` switch tabs (the vim jump list does not exist here); a board shows a trailing `Unmapped` column rather than hiding a loaded issue whose status maps to no column (issue #65, `ux-screens.md`); Jira's board endpoint never returns such issues (verified, #138), so the column is a safety net for stale data. Why: no data silently hidden, tabs are cheap.

**Architecture:** The cache is a host-owned `SiteCache` (memory plus disk per profile), not part of `AppState`; the reducer asks for writes by effect and the load handler answers with cached rows first. Why: LRU order and byte counts are infrastructure, and this keeps the reducer pure and cheap. Detail: `architecture.md`, "Cache and refresh".

**Architecture:** Load results are applied only if their per-tab request id is current and the host drops results of an older `SiteConnections.Generation`; refresh is the active tab only (60 s, 30 s on switch, `AsBackground()`, backoff to 5 min, injected `Tick`). Why: pure reducer, no stale data after a tab close or profile switch. Detail: `architecture.md`, "Views and tabs", "Loading".

**Architecture:** Switching profile drops the views and memory cache but keeps that profile's disk cache folder; removing the profile deletes it. Why: views reopen instantly when the profile comes back. Ids (`TabId`, request ids) come from one counter in `AppState` that survives a switch, so an old reply never matches a new tab.

## 2026-11-02

**UI:** [Superseded 2026-11-04, #703: newest first.] The issue detail lists comments oldest first (supersedes `ux-screens.md`, #84). Why: the client pages oldest first; reversing needs a second client mode for no reader benefit. `q` still quits in the detail. Details: `ux-workflows.md`, "Design details".

**Architecture:** Write pipeline (#151): operations live in `SiteSession.Operations`, row changes are optimistic with rollback, writes are never retried and a lost reply is verified by re-reading. Why: Gitunia's M7 retrofit. The single-transition confirm part is superseded by the #159 entry. Details: `architecture.md`.

**Architecture:** Writes run through `NetworkRunner.StartDetached`, not a scope keyed by operation id (#154), and read-back rows count only if they show the change. Why: a tab close must not cancel a write; the search index can lag. Details: `architecture.md`, "Design details".

**UI:** Change status (#159): a single transition confirms when its target is in the `done` category or it shows a screen with no fields; others send at once. Why: Jira exposes no post functions, so these are the only hints; resolves roadmap question 6 with its fallback. Details: `ux-workflows.md`, "Design details".

**UI:** `<space>kk` (rank.to) is pick up and drop, not an issue picker (#169, overrides #152). Why: the cursor already shows the target, so a searchable list is not needed. Details: `ux-workflows.md`, "Design details".

**UI:** The link form (#190) picks link type and side in one choice, and removing a link works from the detail cursor only. Why: one pick states the sentence and avoids a wrong-direction link. Details: `ux-workflows.md`, "Design details".

**UI:** The suggestion popup (#214, #293) drives labels, user, parent, select and multiple-choice fields; debounce runs in `SuggestionHandler`, not the reducer. Why: one picker style, and the reducer reads no clock. Details: `ux-workflows.md`, "Design details".

**Process:** Milestones reordered (owner decision 2026-11-02): themes come earlier, so the roadmap is renumbered and the former M3-M6 are now M4 (Scrum and sprints), M5 (More views), M6 (Keymap and discoverability) and M3 (Themes, mouse and terminals). Goals and content are unchanged.

**UI:** Comment drafts are not kept across a restart (#288): the discard question already says the text is "not saved anywhere", a draft file would need the cache's secret-free rules and a cleanup story, and a comment is short-lived. A reopened editor starts empty. Revisit when the description editor (#164) shows long texts are lost in practice.

**UI:** Inline suggestions in the comment editor (#215): `@` opens people, `#` plus a project key opens issues, shown as rows inside the box; a mention or issue is a link mark, not a smart card, so every Jira surface renders it. Why: simplest layout, viewer-independent output. Details: `ux-screens.md`, "Comment editor".

**Stack:** macOS secret store (#29, #202; supersedes "no store on macOS yet"): `security -i` with the token on standard input, no native binding, AOT-safe; written without a Mac, the owner checks it. Details: `architecture.md`, "Cross-platform" (Secrets bullet).

## 2026-11-03

**UI:** Realigned with Gitunia's proven interaction design where Geeratya diverged without a reason (issues #368 to #373). Not adopted (Settings screen, per-view key rows, repeat mode, a third command level, one merged config file), each with its reason: `ux.md`, "Not adopted from Gitunia".

**UI:** The palette (#368) is Gitunia's fuzzy list of every action, opened by `:` or `Ctrl-p`; commands with arguments stay a command line. Why: the list makes the keymap findable and gives every action a mouse path. Details: `ux-workflows.md`, "Design details".

**UI:** Status messages (#369) have a severity, a lifetime (5, 15, 30 s) and a history; a key press no longer clears one. Why: writes finish after the user has moved on, and a one-string notice cleared by the next key loses their errors. The history is `<space>n`, not Gitunia's `<space>m`, because `m` is the comment group; `<space>x` dismisses.

**UI:** The Confirm question becomes a centred dialog with clickable `y` and `n` (#370, M3 with the mouse paths); a dialog that does not fit is not shown and the action does not start. Why: a question cut off at the terminal width cannot say what changes or that Jira has no undo, and the status line version has no mouse path. Keys unchanged (`Enter` never confirms).

**UI:** The which-key popup waits 250 ms and groups by noun and cluster, and `hints.statusBar` gets `minimal` (#372); the help overlay opens on the active view's keys with a filter and a `Custom` page (#371); levels hide and never disable, `<space>*` toggles (#196). All as Gitunia learned after shipping the flat versions.

**Stack:** Settings go through one registry (reader, writer, `:set`, doc table) and a writer that replaces only the changed value so the user's comments survive (#373); `Ctrl-c` is read before the keymap so a user file cannot remove it (#303). Both as in Gitunia.

**UI:** Themes (#198; source: Gitunia; supersedes PR #356): a theme is a JSON file with a required `variant` and twelve colours; only built-ins are contrast-tested. Why: the terminal's palette decides at 16 colours. Details: `ux.md`, "Colour tiers".

**UI:** Theme `auto` is the default and asks the terminal for its background with OSC 11 before the UI starts (#375, Gitunia's #527, code rewritten), then `COLORFGBG`, then dark; the answer is cached, so `:theme auto` never queries later. Why: most terminals set no `COLORFGBG`. Supersedes "`auto` is reserved" in the #198 entry. Details: `ux.md`.

**Stack:** Settings registry (#373, as in Gitunia): one registry for reader, writer, `:set` and doc table; keys are grouped (`hints.statusBar`, flat names from before #391 still read). Details: `settings.md`.

**UI:** Command levels (#196): the level belongs to the action in `ActionCatalog`, two levels, and the palette searches advanced actions once a filter is typed. Why: `delete` must stay reachable. Details: `ux-keymap.md`.

**UI:** Quit and cancel (#390, Gitunia rules unless writes or typed text are at stake): `Ctrl-c` quits from Confirm and cancels read loads, but asks while writes run and never quits in a text form; `q` does nothing in Confirm. Why: a stray key never answers a destructive question. Details: `ux-keymap.md`.

## 2026-11-03 (later)

**Process:** Milestones renumbered again (owner decision): Keymap and discoverability is M4 and runs first, Scrum M5, More views M6, Release M7; issue numbers unchanged. Older text saying "M4" meant Scrum and "M6" meant Keymap.

## 2026-11-03 (Visual mode)

**UI:** Visual mode (#393) is a state of the tab (`TabState.VisualAnchor`) over Normal mode, not an `InputMode` of its own as in Gitunia (`VisualReducer`, key mode Visual). Why: Geeratya's Visual keys are exactly the Normal keys (motions extend the range, write keys act on the set), so a second mode would duplicate every Normal row and every Normal-only check for no difference in behaviour; the badge still reads `VISUAL`. Revisit if Visual ever needs keys Normal must not have..

## 2026-11-03 (keymap audit decisions, #465-#468)

Owner asked for maximum autonomy; each took the recommended default (agent: orchestrator).

**UI:** Message history stays `<space>n` (#465), not Gitunia's `<space>m`. Why: `<space>m` is the comment group and the layout rule is one noun, one `<space>` group. No code.

**UI:** Help overlay keeps `Tab`/`Shift-Tab` for pages and `j`/`k` for scrolling (#466); `Ctrl-d`/`Ctrl-u` half-page scroll is added (follow-up issue). Why: the help pages are short lists and `j`/`k` matches the rest of the app.

**Scope:** In-view find (`/`, `n`/`N`) and copy are not included (#467). Why: filtering is done with JQL and README Features does not list them. Revisit only with a README Features line first.

**UI:** The actions menu stays `<space>a` (issue) and `<space>A` (tab and view) (#468), not Gitunia's `<space>.`. Why: already built, documented and taught in hints. No code.

## 2026-11-03 (look spec, #523)

**UI:** The look follows Gitunia (one frame with shared edges, title chips, accent focus, floating overlays) but differs where listed in `ux-look.md` section 8: panes per screen instead of a fixed pane set, one `OverlayBox` over a muted visible view, no pane-resize keys, and issue-type and status colours instead of Git colours so it does not read as a Git client. Pane focus reuses `h`/`l` and `]`/`[`, so no keymap change. Why: owner intent (slicker, Gitunia-styled, not a Git look); Gitunia `ux-look.md` and `ux-mouse.md` (Backdrop) shaped it.

## 2026-11-03 (profile manager, #528)

**UI:** The profile manager (#528) was first the profile list overlay plus one four-field (the two-pane screen came with #535) add/edit form (`Tab` between fields, `Ctrl-s` save, `Alt-t` test connection); no token source row. Why: popup and form components were restyled first (#524 to #527), the two-pane screen followed in #535; the token is always the OS secret store (`GEERATYA_TOKEN` is process-wide and works with one profile only), so there is no per-profile choice to show. `Alt-t` because `Ctrl-t` is the picker's open-in-new-tab key. Details: `ux.md`, "Profiles".

## 2026-11-03 (quick filters and swimlanes, #192)

**UI:** Swimlanes are back in the columns layout, as Geeratya's own grouping (assignee, epic) shown one lane at a time (superseded by the 2026-11-09 entry below: all lanes are listed in a strip, #874), and quick filters are the board's own, applied as extra JQL (overrides the 2026-11-02 "no swimlanes" entry and #143 feedback; the later #192 comment kept both). Why: the agile API gives no swimlane definition, and one lane at a time keeps the card-pitch viewport maths. Epics are neither hidden nor added: the board endpoint already follows each board's own rule. Details: `ux-screens.md`, "Columns layout".

## 2026-11-03 (start on a board, #542)

**UI:** A start reopens the profile's remembered tabs (definitions only, `tabs.json`); with none, the board picker opens (the board itself when there is one) once the connection works. Overrides roadmap question 3 (default "no"). Why: the owner wants a useful first screen. `startup.restore off` skips the file and always opens the picker; closing every tab also counts as nothing remembered. Details: `ux-workflows.md`, workflow 0.

## 2026-11-03 (profile manager screen, #535)

**UI:** The profile manager stays open under the profile form and the remove question and is the first screen when no profile exists; its details pane shows the token source only (looked up by the host, never the token) and, for inactive profiles, no connection status (no request goes to an inactive site). Why: a screen that vanishes on Ctrl-a loses the list the user is editing; the first run needs one place to add a profile.

## 2026-11-03 (theme backgrounds, #559)

**UI:** An overlay box is filled with the theme's own page colour and the view behind it is darkened (the page moved towards black) instead of the box being a `selection`-derived gray raised from an unchanged page; a theme file may set an optional `overlayBackground` for the box. Why: measured in tmux, every theme painted its published `baseBackground` faithfully, but the box fill (a gray step towards `selection`) and the dimmed text on the unchanged page read as two competing grays. Overrides the "backgrounds unchanged" rule of `ux-look.md` section 6. Details: `ux.md`, "Themes"; `ux-look.md`, sections 6 and 7.

## 2026-11-03 (settings screen, #556)

**UI:** The Settings screen (reverses the "not adopted" note in `ux.md`, owner request) runs in Normal mode on the Normal keys, not in Insert mode like the profile manager; `<space>,` opens it (as in Gitunia), `Alt-r` resets a row (`r` is vim's). Why: it has no text field, so the existing motion keys apply and nothing new is modal.
**UI:** `q` quits from the settings screen like everywhere else (the "writes running" confirm still applies); `Esc` or `<space>,` closes it. Why: one meaning of `q` outside Help.
**UI:** The theme list previews through `ApplyThemeEffect(Preview: true)` (drawn, never saved or kept in the settings); Esc or any other way out redraws the saved theme (`SettingsScreenReducer.RestorePreview`). Why: the reducer cannot see the screen, so restoring is a rule on every command.
**UI:** Rows come from `SettingsRegistry` (new `Choices` and `Category` fields); a free-text setting without choices is not edited in place (`:set`). Why: the registry has none today and a text prompt needs Insert mode; registered as a follow-up.

## 2026-11-03 (code structure rules, #580)

**Code structure:** Dependencies are objects (no `Func<>`/`Action<>` collaborators, at most five constructor parameters), no `.cs` file over about 500 lines in `src/`, and fixed places for logic (#580). Why: agents kept growing files and wiring delegates. Source: Gitunia's rules from the owner's code review of 2026-11-01. Rules: `.claude/rules/csharp-style.md`.

## 2026-11-04 (terminal background, #566)

**UI:** `:set transparent on` keeps the terminal's own background for the page roles in every tier; it is never refused and no tint is blended from a detected colour (overrides the plan in the issue). Why: OSC 11 is missing on Windows and the selection, scrim and chip fills are painted fills that need no base; contrast cannot be guaranteed against an unknown colour, so text keeps the theme's colours (tested against the theme page) and the user picks a theme of the terminal's side. The overlay box stays painted so it still stands out. Details: `ux.md`, "Themes".

## 2026-11-04 (remove autoCompleteUrl, #627)

**Jira layer:** The field `autoCompleteUrl` is not mapped and `SuggestionClient.SearchByAutoCompleteUrlAsync` is removed (#627). Why: no form used them; the pickers for people, labels, issues, components and versions use their own endpoints, and custom pickers (`autoCompleteUrl` of non-standard fields) are Unsupported/read-only with a browser link, which is outside the feature list. Re-add with a concrete picker when one is needed, restricted to the connection's own scheme and host under `/rest/api/` as the removed code was.

## 2026-11-04 (start target, #577)

**UI:** The command line takes one start target (issue key, `--board`, `--backlog`, `--jql`, `--filter`) and `--profile`, added to the README features (owner's yes by recommended default). Why: scripts, aliases and link handlers need to open an issue or view from a shell (Gitunia opens `gitunia <path>`). The target is held in `AppState.PendingStart` and opened by `StartupReducer` after the remembered tabs are read, so it never races the restore; the opened tab is saved with the next change of the tabs (not at once, so a launch that only looks leaves the remembered tabs alone). Details: `ux-workflows.md`, workflow 0.

## 2026-11-04 (status bar content, #646)

**UI:** The status bar in Normal mode shows the source and count, the selected issue (key and status) and the connection only when it is not healthy (Checking, Offline, rate limited, check failure); `Connected as ...` and the `col n/N  card n/N` counters are gone from it (owner request, overrides the cursor position of #569). Why: both were noise; the key hints already carry the contextual help. The profile screens keep the connected line.

## 2026-11-04 (edit form navigate state, #650)

**Superseded in part by #762:** the multi-field edit form is gone (Summary and Description are edited in place, every other edit form has one field); the navigate state remains for the create form only.

**UI:** The edit form (more than one editable field) has a navigate state inside Insert mode: `j`/`k` (the Normal `nav.down`/`nav.up` rows), `Tab`, `Down`/`Up` move, `Enter` or the literal `i` start editing; the status bar keeps `INSERT`, the form shows `[navigate]`/`[editing]` and its own footer. Why: no new input mode for one overlay; `i` is a fixed key like `y`/`n` in discard (an Insert row for `i` would swallow typed text). Amends `ux-keymap.md` principle 7 for this form only.

## 2026-11-04 (M6 hand-over, owner answers)

**UI:** `Ctrl-t` and `Ctrl-w r` keep Gitunia's meaning (new tab, tab prefix) although they shadow vim's tag-pop and window prefix. Why: same keys as Gitunia, nothing else uses them here. `<space>j` (flash jump to a visible row, #578) is built now, and the command-line start target (#577) is wanted; `--profile` stays out (no such option).

**UI:** The Settings screen is a popup over the dimmed view (Help-style `OverlayBox`), not a full-body screen (#677, owner feedback); this reverses the full-body choice of #556 in `ux-look.md`. Controls follow Gitunia's look (`[x]`, `[value ▾]`, list under the row); number and path-list controls and the `/` filter are not built because Geeratya has no such settings or feature.

## 2026-11-04 (issue detail as a form, #678)

**UI:** The issue detail's fields are one cursor stop each (one per row, a wrapped value still one stop) and `Enter` edits the focused field through the existing paths; `Tab` stays tab-next and `i`/`e` stay unbound. The work is two PRs (fields, then sections) and the mock-up was posted on the issue without waiting for the owner (the owner is never the bottleneck). Why: the earlier #650/#651 only changed the edit popup, so the screen the owner means is the read-only detail; `Tab` and the vim keys are taken.

**UI (superseded for the detail by 2026-11-04, #702: panes do not fold):** The issue detail's sections fold with `z a` (the backlog's fold key) and long text is cut with a `more lines` row, not with new keys; fold state is per issue in the tab, keyed by pane title without its count. Why: vim's fold key already exists here; counts change as pages arrive. Fold marks are the text `(folded)`, not glyphs, so the ASCII set needs no mapping.

## 2026-11-04 (Done cutoff from Jira, #681)

**UI:** The Done cutoff picker is rows of the command palette (found by "Done cutoff:", opened by `<space>vd`), not a new overlay, and the choice lives on the tab and in the remembered tab (so per profile and board) instead of a separate per-board store. Why: least new state and no second file; the palette already is the picker used for themes. Source order: tab choice, Jira's value (gateway, optional), setting.

## 2026-11-04 (pane layout, M7)

**UI:** The issue detail, board cards and the edit and create forms follow the approved mockups in `ux-screens.md`, "Pane layout for the issue detail, board cards and forms" (milestone M7). Why: the box-per-field form and the dense detail did not read like a form; the owner approved a pane layout after a mockup session (Claude Design canvas, private).

**UI:** Pane focus is `Ctrl-h/j/k/l` (Gitunia's `pane.focus-*`; `Ctrl-w h/j/k/l` as fallback, as `Ctrl-h` is Backspace on some terminals), zoom is `<space>z` (`pane.zoom`), and `Ctrl-j/k` also choose in dropdowns. `]]`/`[[` leave the detail. Why: `Alt-h/j/k/l` are card move and rank, and Gitunia already uses Ctrl for panes. Overrides the `]]`/`[[` pane chords of `ux-look.md`, section 4, for the detail.

**UI:** Comments are blocks (header bar, body, one empty row) cut after 3 lines (6 zoomed) with `Enter` to expand, so one long comment cannot hide the rest while the list stays one scroll. Why: owner feedback on the mockups.

**UI:** The issue detail (D) and the create form (C) are wide overlays over the tab's view in the look of the old edit form (A), holding the issue stack; there is no separate edit form: every field is edited in place in the detail (dropdowns in Details, `Enter`/`i` on the Summary and Description panes). Overrides "Field form overlay" for edit and create and the tab-body detail of "Issue detail"; the one-field pickers stay overlays and the type picker stays first in create. Status is always a transition, never a free field write. Why: owner's choice after the mockup session; one place to edit, no second form to build.

**UI:** Key hints are accent-coloured bold keys with muted labels everywhere, overlay footers included (overrides the `warn` key role of `ux-look.md`, section 5), and field labels are accent, not teal. Why: owner feedback, Gitunia's look. Unassigned is a `--` chip as wide as initials, never a glyph.

## 2026-11-04 (pane keys against Insert-mode keys, #701)

**UI:** The pane keys (`pane.focus-*`, `pane.zoom`) are Normal-mode rows only. Insert mode keeps `Ctrl-j`/`Ctrl-k` as `picker.next`/`picker.previous` and `Ctrl-l` as `form.next`; the rule for the two states of a form is: (1) a form in its navigate state (`[navigate]`, nothing is typed, no dropdown) takes `Ctrl-h/j/k/l` as pane focus when the tab shows panes, and is unchanged when it does not (`Ctrl-j`/`Ctrl-k` step the field, `Ctrl-l` still starts editing like `Enter`); with panes, `Ctrl-l` is pane focus and only `Enter` or `i` starts editing; (2) a form being edited, an open dropdown and the suggestion popup keep the Insert rows untouched, so `Ctrl-j/k` choose in the list and `Ctrl-l` accepts, and `Ctrl-h` is not bound (`Esc` returns to navigating first, then a pane key moves the focus); (3) the `Ctrl-w h/j/k/l` chords exist in Normal mode and, since #721, in a form's navigate state with panes (pending key held in `AppState.PendingKeys`); in an editing form `Ctrl-w` deletes a word. Why: the owner wants `Ctrl-h/j/k/l` across panes and forms, but typing and choosing must never lose a key; the navigate state has no text and no list, so nothing is shadowed there. Implemented as `PaneActions.Apply` consulted first in `FormReducer.Navigating`; a tab without a pane grid is untouched (the grid comes with #702).

**UI:** `Ctrl-h` is bound as `Ctrl-h`, `Ctrl-Backspace` and `Ctrl-w h`. Result of the Terminal.Gui check: a terminal that sends `0x08` for Ctrl-h may be reported as `Backspace` or as `Ctrl+Backspace` depending on driver and terminal; `KeyTranslator` spells the second `Ctrl-Backspace` (tested), and the first is indistinguishable from the Backspace key, so there `Ctrl-w h` is the working key. Gitunia (same Terminal.Gui 2.5.0) recorded that under tmux `Ctrl-h` arrives as `Ctrl-Backspace`; Geeratya did not verify it in a real terminal itself (owner check: #720); the hint chips will show the key that works once pane screens exist. Why both spellings: Gitunia binds `Ctrl-h` and `Ctrl-Backspace` the same way.

## 2026-11-04 (Windows scripts, #632)

**Testing:** On Windows the known folders stay the default; with `GEERATYA_USE_XDG=1` a fully qualified `XDG_CONFIG_HOME`, `XDG_STATE_HOME` or `XDG_CACHE_HOME` overrides them (overrides the "Windows ignores XDG" rule in `architecture.md`). Why: .NET reads the known folders from the shell, so `APPDATA` cannot be redirected and `measure.ps1` would otherwise read the real profile and talk to a real site; the opt-in keeps a global `XDG_CONFIG_HOME` (neovim, starship) from moving a real install. `screenshot.ps1` was tried and dropped (not worth it on Windows, #632).


## 2026-11-04 (M7 and M8 swapped)

**Process:** Milestones renumbered (owner decision): M7 is now Pane-based detail, cards and forms and M8 is Release and going public; issue numbers unchanged. Older text saying "M7" for the release (history, issue comments) meant M8. Why: the pane work runs before the release check.

## 2026-11-04 (#702, panes in the detail)

**Jira layer:** A sub-task's assignee comes from the `parent = KEY` search that epics already run, so an issue with sub-tasks now runs it too (one request, no Child issues pane for it); the cell is blank until it arrives or fails. Why: Jira's `subtasks` stubs carry key, summary, status and type only, and a request per sub-task would be N+1.

**UI:** The detail's `]]`/`[[` and `z a` are gone, panes expand with `Enter` (the backlog and the board keep the chords). Zoom (`<space>z`) answers "not available yet" in the detail until #705. Why: the pane decisions of this day; a hidden zoom state with no drawing would trap `Esc`.

## 2026-11-04 (#703, comments newest first)

**Jira layer:** [Superseded 2026-11-09, #875: oldest first by default, reversible.] The comment call sends `orderBy=-created` and the detail lists comments newest first; an added comment goes on top, a thread cut at 1000 keeps the newest. Why: the Comments pane spec says `newest first` and a long thread should show the latest at once (readiness default, owner not blocking; supersedes the 2026-11-02 "oldest first" entry, #84).

## 2026-11-04 (Details pane: sidebar, field blocks and dropdowns, #704)

**UI:** (1) The sidebar shows from an overlay width of 100 columns (terminal 104), not terminal 100: `DetailLayout.HasSidebar` takes the one width the document is built for, and the overlay is 4 columns narrower than the terminal; the 96-column overlay of a 100-column terminal stays one column. (2) Below that, Details is the last pane under Comments ("under the main panes"). (3) A dropdown is the existing field form drawn as a popup under the field (`FormState.Dropdown`), not a second editor: the keys, the metadata read, the confirmations and the write path are the form's, and accepting a row (Enter) writes at once, for a person or label too. Why: one write path; the form already holds the load, popup and save states. (4) Editability of Priority, Assignee and Labels comes from `editmeta`, read once per issue load in the background (one extra request per issue, besides the form's own editmeta read when a dropdown opens, which stays the one that fills the form); Status, Sprint and Estimate keep their own pickers and are always marked. Why: CLAUDE.md and jira-layer.md say what a project can edit is read from Jira. Consequence: while it is unread the three fields show no mark but `Enter` still opens the form. (5) In the narrow layout `j` and `Ctrl-d` run on from the last comment into the Details pane under it; `G` still lands on the last comment (#703).

## 2026-11-04 (create form panes, #708)

**UI:** The create form has the navigate and edit states of the edit form but opens in the edit state on Summary (the mockup shows the caret there), so a first `Esc` navigates and a second closes (asking when changed); `Enter` on a single-line field no longer jumps to the next field or creates, `Ctrl-s` creates. Why: one form model for edit and create, and `Esc` has to leave typing before pane keys can mean anything. The folded optional fields are the Details fields after the first five, except required, changed or errored ones, so a hidden field never swallows an error or an entered value; the field order is fixed in `FormFieldMapper.FromCreate` (Summary, Description, Parent, required, optional) so `Tab` is reading order.

## 2026-11-04 (NU1903 suppression, #719)

**Stack:** `NuGetAuditSuppress` for GHSA-p3jp-9v83-76w9 (Brotli decoder out-of-bounds write, CVE-2026-111497; ILCompiler 10.0.12 is the version bundled by SDK 10.0.401) in `Directory.Build.props` (#719). Why: NU1903 fails the warnings-as-errors build; pinning ILCompiler 10.0.14 does not work (NU1009 under central package management, then a `PrivateSdkAssemblies` error in the AOT publish). Tracked for removal in #747.

## 2026-11-05 (workflow plugin replaces the project work skills)

**Process:** The project-local `work-task`, `work-orchestrator`, `work-readiness` and `retrospective` skills, the `reviewer` agent and `scripts/test-owner-feedback.sh` are removed; the workflow plugin provides them, configured by `.claude/workflow.md`. Project-specific content moved to `.claude/review-checklist.md` (reviewer checks) and `.claude/agent-identity.md` (comment headers, markers, signatures). Same change as in Gitunia.

## 2026-11-05

**UI:** In the issue detail `Enter` edits (Description included) and `z a` (`section.toggle`, vim fold) expands or cuts a long Description or comment, the key named in the pane frame; no `>` cursor mark in any detail pane (selection tint instead); which-key keys are warn, group headers accent (Gitunia); the Details sidebar is ~30% of the overlay; the detail and create form sit in a framed card. Why: owner review of the pane UI, Enter was ambiguous between edit and expand. Yank/copy is still out of scope (2026-11 #467).

**Scope:** Copying text out of the issue detail is in (#773, owner request; supersedes "copy is not included" in the 2026-11-02 entry for #467; in-view find stays out). `v` starts a line selection in the detail (its vim meaning, Visual mode), motions extend it, `y` yanks it (vim yank; with no selection, the whole field or comment under the cursor, also its cut part), `Esc` cancels, a mouse drag over the main column selects and yanks on release. The clipboard is an effect in `Geeratya.App` over `IClipboard` (Platform): Linux `wl-copy`, `xclip`, `xsel`; macOS `pbcopy`; Windows `powershell` `Set-Clipboard` (the text travels base64 encoded, so Unicode survives); when none is installed or works, the OSC 52 terminal sequence; no way left gives an explicit warning. Why: the owner could not copy anything out of the Description. Details: `ux-keymap.md`, `ux-mouse.md`.

**UI (#772):** One cursor rule on every screen: the cursor or focus row is the `Selected` role (selection tint; bold and reverse on 16-colour, `NO_COLOR` and ASCII, so never colour alone) and no row draws a `>` mark. Applies to list, board, backlog headers, Settings, profile form and manager, form fields and popups, jump, source picker, palette, context menu, Help menu. Kept: the input prompt `> ` and the markdown quote `> `. Trade-off: the plain print frame (`--print-frame`) no longer shows the cursor row, since it carries no styles; tests assert the `Selected` role instead.

## 2026-11-08 (README demo hosting, #209)

**Process:** The README demo (`demo.gif`, `demo.webm`) is recorded by `scripts/demo.sh` and hosted on the project's GitHub Pages site (`geeratya.github.io`, `media/`), not in this repository and not in the screenshots repository (owner decision). The README links `https://geeratya.github.io/media/demo.gif`; copying the files there is the owner's step. Why: no binary media in the repo (see `testing.md`, "README demo").

## 2026-11-08 (landing page, #695)

**Process:** The landing page is a separate static repository `Geeratya/geeratya.github.io` (owner decision), not a `docs/` page of this repository; supersedes the issue's "Pages workflow behind a switch" and "no binary media in the repository" (the demo files live there). Agents build and commit it locally; the owner pushes it, enables Pages and makes it public. Details: `landing-page.md`. Why: the owner's other project does the same, and the README demo already points at `geeratya.github.io/media`.

## 2026-11-08 (Homebrew tap, #821)

**Process:** No macOS signing or notarisation (owner decision, #207 closed): Homebrew (`Aleksanderis/homebrew-tap`) is the macOS route, archives need the `xattr` line. The tap update is its own workflow `homebrew.yml` (on release published, off unless the variable `HOMEBREW_TAP_UPDATE` is `true`), not a step in `release.yml`, so it runs after the draft is published and the archive URLs are public; same shape as the winget workflow of the owner's other project. Why: a formula pointing at draft-release assets would be broken until publication.

## 2026-11-08 (shared Homebrew tap, #823)

**Process:** The tap is `Aleksanderis/homebrew-tap`, shared with the owner's other apps (install: `brew install aleksanderis/tap/geeratya`), replacing `Geeratya/homebrew-tap` from #821. The generator and `homebrew.yml` write only `Formula/geeratya.rb` (no tap README, no `git add -A`), so other apps' formulae are never touched. The token scope covers the whole tap (GitHub cannot scope by path).

## 2026-11-08 (arm64 release builds, #859)

**Process:** linux-arm64 and win-arm64 build on native arm64 runners (`ubuntu-24.04-arm`, `windows-11-arm`, free for public repositories), not cross-compiled: Native AOT needs the target's native toolchain and linker, and Gitunia does the same (#1247). The linux arm64 job also builds the arm64 `.deb`/`.rpm` and installs them in native arm64 containers. macOS stays Apple silicon only (osx-x64 is out). Asset names stay `geeratya-<version>-<rid>.tar.gz|.zip`, rid in linux-x64, linux-arm64, win-x64, win-arm64, osx-arm64.

## 2026-11-08 (Arch package, #861)

**Process:** The Arch/Omarchy `.pkg.tar.zst` is built by `scripts/package-linux.sh` itself (tar + zstd + `.PKGINFO`, no nfpm or makepkg, like the `.deb`) from the linux archive, so no extra tool is fetched in the release job. Install check: real `pacman -U` in `archlinux:latest` for x86_64; aarch64 has no official Arch image (Arch Linux ARM is third-party, not trusted in a release job), so the native arm64 runner unpacks the package, checks `.PKGINFO` and runs the packaged binary. The package is covered by `SHA256SUMS`; the AUR PKGBUILD already builds from the same archives for both arches and is unchanged. Gitunia (nfpm, no install check) was read for scope only.

## 2026-11-09 (#875, comment order)

**UI:** Comments are oldest first by default and `<space>mo` reverses them, saved as the `comments.order` setting (supersedes the 2026-11-04 "newest first" entry, #703). The request follows the order (`orderBy=created` or `-created`), so paging stays by offset; an issue keeps the order it was opened with, a flip only changes the shown issue and the default for later ones. Why: owner request. Threads: [Superseded 2026-11-09, #897: the claim "no reply field" was wrong, see the entry below.]

## 2026-11-09 (copy issues, #876)

**UI:** Issues are copied with `<space>y y` (key), `<space>y u` (link) and `<space>y s` (`KEY Summary`), also from the right-click menu; a bare `y` outside an opened issue copies the key. The target is the focused issue (row or card, the shown issue, or the related issue or Parent/Epic field under the detail cursor). Extends #773: same `CopyToClipboardEffect` and `IClipboard`. Why: leader chords keep vim's `yy`/`Y` free (vim-clash rule); three forms cover what is pasted into chat, commits and notes.

## 2026-11-09 (swimlane strip, #874)

**UI:** Swimlanes show every lane together as a one-row strip above the frame (`▾ Name n` for the lane whose columns are drawn, `▸ Name n` for the others; click a chip to show it), instead of only a `Lane n/N` text in the status line (supersedes "one lane at a time" of #192; the owner's Jira screenshot shows lane headers over the columns). Why: all lanes visible with their counts, at a fixed cost of one row, so the card-pitch viewport maths stay valid (`LaneStrip.BoardRows`). Several lanes with columns open at once and folding lanes are follow-ups; they need a per-lane cursor and scroll. Details: `ux-screens.md`, "Columns layout".

## 2026-11-09 (stacked lanes, #891)

**UI:** Several lanes' columns can be open at once, stacked under a header row each (`<space>vl`, `view.swimlane-stack`), a per-tab toggle next to the grouping rather than an open/closed state per lane; the body is divided equally between the lanes that fit at 8 rows each, paged around the current lane, and every lane keeps its own cursor and scrolls (the owner's Jira screenshot on #874 shows all lanes with their columns under a header). Why: a toggle needs one key and one flag and keeps the single-lane layout as the default; per-lane open/closed state is what folding (#893) adds on the same regions, and the key stays free for it. Details: `ux-screens.md`, "Columns layout".

## 2026-11-09 (fold a lane, #893)

**UI:** A stacked lane folds to its header row (`z c` `lane.fold`, `z o` `lane.unfold`, chevron click), the current lane is never folded (entering a folded lane opens it) and folding needs stacked lanes. Why: the card actions (`Enter`, marks, writes) act on the current lane's selected card, so a folded current lane would leave a hidden target; `z a` stays the sub-task toggle because a card is under the cursor in columns, so the fold keys are `z c` and `z o`, like vim's close and open. Folds are per tab for the session, not saved (they are a view state like the column scroll). Details: `ux-screens.md`, "Columns layout".

## 2026-11-09 (swimlanes by labels, #892)

**UI:** `<space>vs` gains a fourth grouping, labels: a card with several labels is in the lane of each of them (counted in each), a card without a label is in `No label` (last). Why: Jira's own "Labels" swimlane does the same and the issue asks for multi-membership; the first label only would hide a card from the lanes the owner filters by. The lane is a view over the same issue, so nothing is copied: the cursor and writes act on the card of the current lane, lane cursors are per lane (`ux-screens.md`, "Columns layout"). Labels are already part of the board fields, so no extra request.

## 2026-11-09 (landing page is always pushed)

**Process:** Agents commit and push the landing repository (`../Geeratya-landing`) to its `main` after each change, no owner push step; supersedes "the owner pushes it" in the 2026-11-08 entry (#695). Why: a local-only landing commit left the page stale (#901). Other repositories keep the push rule.

## 2026-11-09 (#897, threaded comments)

**Jira layer and UI:** A reply carries `"parentId": <parent comment id>` (a JSON number, while `id` is a string) in the REST v3 comment list; top-level comments have none (seen on the dev site, GCM-9). `IssueComment.ParentId` maps it (string, null when absent). The Comments pane lays comments out as threads (`CommentThreads`, Core): parents keep the loaded order (`comments.order`), replies follow their parent oldest first, a `│ ` guide (a `Border` span, so yank leaves it out) marks each level, and a reply whose parent is not loaded shows flat in place. Supersedes the #875 claim "no reply field" (the recorded fixtures just held no reply). Writing a reply (`parentId` on create) is not part of this and is registered as a follow-up.

## 2026-11-09 (#905, reply to a comment)

**Jira layer and UI:** `<space>mr` (`comment.reply`, also in the comment's right-click menu) opens the add-comment editor titled `Reply to <author> on <key>`; saving is an add with `parentId` (JSON number, the shape of the read), with no extra confirm, as for any add. One level only: a reply to a reply goes under the thread's top-level comment (the web UI showed one level in #897). The public REST docs do not list `parentId` for the create call, so it is unverified: a live check on the dev site is pending (if Jira ignores the field the comment simply shows top-level).
