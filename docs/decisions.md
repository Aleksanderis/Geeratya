# Decisions

Short dated log of live, non-obvious settled choices that cut across docs. Add an entry only when the choice is not already evident from `ux.md` and its parts (`ux-*.md`), `architecture.md` or the code, or when it overrides an earlier decision; a behaviour change documented in `ux.md` needs none. Agents follow these without asking. Open questions live in `roadmap.md`. Superseded, closed and purely historical entries are archived word for word in `history/decisions.md` (not required reading).

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Jira layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. Long detail lives in `docs/history/`, the area docs or the issue or PR. When a decision changes, append a new entry that supersedes the old one, and archive the old entry once the new one states the outcome.

## 2026-11-02

**Scope:** Scrum boards and sprints are in (milestone M3, ahead of polish), in both project kinds; Scrum reports and charts are not. Why: Scrum is common and the tool is not only for the owner's team. Dev site: `GCS` (company-managed, board 6) and `GTS` (team-managed, board 5) are sandbox projects like `GCM` and `GTM`.

**Jira layer:** Project configuration is dynamic: issue types, fields (including custom ones), required fields, allowed values, transitions, statuses and link types are read per connection and project from Jira metadata, never hard-coded from the dev projects; unsupported field types degrade to read-only with a browser link. Why: every site and project differs (custom fields, custom issue types, team-managed vs company-managed). Rule in `.claude/rules/jira-layer.md`.

**UI:** Suggestions: `@` opens people, `#` plus a project key opens issues (inside text editors, Neovim-style popup); picker fields (labels, components, assignee, selects) suggest by context as you type, fzf/zoxide `zi` style. Why: owner's choice; issues #213-#215.

**Process:** Milestones after M2 follow what Gitunia had to redo, not its order: the write pipeline and a UI-free `Geeratya.App` project come first in M2 (running operations in state, rollback, mutual exclusion), the keymap gets command levels and a completeness test (M5), themes get a contrast test each (M6), and version stamping, idle CPU and macOS come before release week (M7). Why: Gitunia's M5 keymap redesign, M7 effects-as-data refactor and M6 theme and console fixes were rework.

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

**Code structure:** Terminal column width lives in Core `DisplayWidth`; its wide-character table (`WideRanges`, East Asian Width W/F plus Emoji_Presentation, Unicode 17.0.0) is generated by `scripts/gen-display-width.py` and committed as a constant span (no runtime download, no startup cost, no dependency). Rerun the script to adopt a new Unicode version.

**Jira layer:** One `JiraTransport` per `JiraConnection`; clients take only the transport and no call takes a connection argument (supersedes the earlier "every call takes a `JiraConnection`" wording in CLAUDE.md and the rules; the "no global site" intent stands). Why: the token and `HttpClient` live in one place, and a profile switch just replaces the transport. The transport requires https (http only for loopback) and never follows redirects, so credentials cannot leak to another host.

**Stack:** The `GEERATYA_TOKEN` fallback applies only when exactly one profile exists; with several profiles a profile needs a stored token, and the status line says so (refines the entry on the secret store; issue #60). Why: the variable carries no site, so a profile switch would send one site's token to another host.

**UI:** M1 view commands and the source picker always open a new tab; `Tab`/`Shift-Tab` switch tabs (the vim jump list does not exist here); a board shows a trailing `Unmapped` column rather than hiding issues whose status maps to no column (issue #65, `ux-screens.md`). Why: no data silently hidden, tabs are cheap.

**Architecture:** The cache is a host-owned `SiteCache` (memory plus disk per profile), not part of `AppState`; the reducer asks for writes by effect and the load handler answers with cached rows first. Why: LRU order and byte counts are infrastructure, and this keeps the reducer pure and cheap. Detail: `architecture.md`, "Cache and refresh".

**Architecture:** Load results are applied only if their per-tab request id is current and the host drops results of an older `SiteConnections.Generation`; refresh is the active tab only (60 s, 30 s on switch, `AsBackground()`, backoff to 5 min, injected `Tick`). Why: pure reducer, no stale data after a tab close or profile switch. Detail: `architecture.md`, "Views and tabs", "Loading".

**Architecture:** Switching profile drops the views and memory cache but keeps that profile's disk cache folder; removing the profile deletes it. Why: views reopen instantly when the profile comes back. Ids (`TabId`, request ids) come from one counter in `AppState` that survives a switch, so an old reply never matches a new tab.
