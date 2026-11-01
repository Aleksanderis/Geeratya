# Decisions

Short dated log of live, non-obvious settled choices that cut across docs. Add an entry only when the choice is not already evident from `ux.md` and its parts (`ux-*.md`), `architecture.md` or the code, or when it overrides an earlier decision; a behaviour change documented in `ux.md` needs none. Agents follow these without asking. Open questions live in `roadmap.md`. Superseded, closed and purely historical entries are archived word for word in `history/decisions.md` (not required reading).

Format: dated heading, entries appended at the end, each starting with a bold topic tag (**Stack**, **Jira layer**, **UI**, **Testing**, **Process**, **Code structure**), one or two lines (decision and why), a blank line between entries. Long detail lives in `docs/history/`, the area docs or the issue or PR. When a decision changes, append a new entry that supersedes the old one, and archive the old entry once the new one states the outcome.

## 2026-11-01

**Stack:** Same as Gitunia: C# on .NET 10 with Native AOT, Terminal.Gui v2, xUnit, JetBrains inspectcode. Why: the owner is a .NET developer, and the agent routines, style rules and tooling carry over.

**Process:** The agent routines (CLAUDE.md, reviewer agent, rules, the work-task, work-orchestrator, work-readiness, retrospective, testing, performance and screenshots skills) are ported from Gitunia and adapted to Jira. Ideas carry over freely; Gitunia code is rewritten, not copied.

**Jira layer:** Jira Cloud only, REST API v3 over `HttpClient`, Basic auth with email plus API token (Cloud has no Data Center style PATs). Why: the owner's target is Cloud.

**Testing:** Live Jira work happens only on the owner's free dev site, guarded by `GEERATYA_LIVE_HOSTS`; the company site is never used by agents or tests. Default tests are offline against recorded, scrubbed fixtures.

**Process:** CI never runs automatically; local build and tests are the gate, and CI is dispatched by hand (before closing a milestone). Why: CI minutes are scarce (carried over from Gitunia).
