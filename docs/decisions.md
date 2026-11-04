# Decisions

Short dated log of live, non-obvious settled choices that cut across docs. Add an entry only when the choice is not already evident from `ux.md` and its parts (`ux-*.md`), `architecture.md` or the code, or when it overrides an earlier decision; a behaviour change documented in `ux.md` needs none. Agents follow these without asking. Open questions live in `roadmap.md`. Superseded, closed and purely historical entries are archived word for word in `history/decisions.md` (not required reading).

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Jira layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. Long detail lives in `docs/history/`, the area docs or the issue or PR. When a decision changes, append a new entry that supersedes the old one, and archive the old entry once the new one states the outcome.

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

**UI:** The issue detail lists comments oldest first (supersedes `ux-screens.md`, #84). Why: the client pages oldest first; reversing needs a second client mode for no reader benefit. `q` still quits in the detail. Details: `ux-workflows.md`, "Design details".

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

**UI:** Swimlanes are back in the columns layout, as Geeratya's own grouping (assignee, epic) shown one lane at a time, and quick filters are the board's own, applied as extra JQL (overrides the 2026-11-02 "no swimlanes" entry and #143 feedback; the later #192 comment kept both). Why: the agile API gives no swimlane definition, and one lane at a time keeps the card-pitch viewport maths. Epics are neither hidden nor added: the board endpoint already follows each board's own rule. Details: `ux-screens.md`, "Columns layout".

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

**UI:** The command line takes one start target (issue key, `--board`, `--backlog`, `--jql`, `--filter`) and `--profile`, added to the README features (owner's yes by recommended default). Why: scripts, aliases and link handlers need to open an issue or view from a shell (Gitunia opens `gitunia <path>`). The target is held in `AppState.PendingStart` and opened by `StartupReducer` after the remembered tabs are read, so it never races the restore; the opened tab is saved with the others like any palette-opened tab. Details: `ux-workflows.md`, workflow 0.
