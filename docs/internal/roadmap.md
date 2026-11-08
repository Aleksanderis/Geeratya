# Roadmap

*Internal design note for maintainers and agents. Not needed to use or contribute to Geeratya.*

Milestone goals and open questions. Work is tracked as GitHub issues per milestone; this file holds goals and "Done when", not status. Features and exclusions are in the README.

## M0 - Foundation

Goal: everything the feature milestones build on, and real knowledge of how Jira behaves.

- Solution skeleton with Native AOT publish, layering and platform abstraction (`docs/internal/architecture.md`).
- Jira survey on the dev site: recorded, scrubbed responses for boards, backlog, ranking, issue hierarchy in both project kinds, create and edit metadata, ADF, search paging and errors; findings in `docs/internal/jira-layer.md`.
- Jira transport: connection, auth, typed errors, timeouts, rate limits, live-host guard, in-process fake.
- Profiles and credential store (OS secret store, env var fallback).
- App shell: Terminal.Gui v2 startup, keymap and generated reference, palette, help.
- Fake Jira server, tmux harness, measurement and screenshot scripts.
- CI by dispatch; dev-site seed data.

Done when: the AOT binary starts into an empty shell under 100 ms, the transport is tested against the fake with recorded fixtures, and an agent can run the binary against the fake Jira in tmux.

## M1 - Read your board

Goal: everything the owner reads in Jira every day, without writes.

- Profile setup screen and connection check.
- Kanban board (columns from the board configuration), backlog, JQL and saved-filter lists, all as views (source plus layout).
- Issue view: ADF description and comments, sub-tasks, epic children, links, attachments as links, open in browser.
- Tabs, local cache, background refresh, offline and error states.

Done when: the owner can follow their real board day to day from Geeratya.

## M2 - Work your board

Goal: the everyday writes on Kanban boards in both project kinds, on a write pipeline designed for them first.

- Write pipeline before the features: running operations per tab in state, mutual exclusion with refresh and with each other, optimistic update with rollback, typed write errors, the confirm flow. The effect handlers move out of `Geeratya.Tui` into a UI-free `Geeratya.App` project, so a handler cannot draw (Gitunia did this late, as its M7 refactor of about 45 issues).
- Transitions, including moving a card to another column and transitions with required fields.
- Create epics, stories, tasks, bugs and sub-tasks with each project's required fields; edit fields and assignee; delete with confirmation.
- Add, edit and delete comments (ADF editing).
- Rank in the backlog and on the board.

Done when: the owner can do a normal working day on a Kanban board in Geeratya and open the web UI only for administration.

## M3 - Themes, mouse and terminals

Goal: it looks right and works by mouse in the terminals people really use.

- Built-in and user themes; a contrast test per theme (a faint selection bar is the Gitunia lesson).
- Full mouse support, every keyboard action reachable by mouse (#46 and the rest).
- Older and classic consoles, the ASCII glyph set, 16-colour fallback; Windows and macOS checks.

## M4 - Keymap and discoverability

Goal: every command is findable and the keys are the user's.

- Command palette (a fuzzy list of every action with its keys; typed commands stay, #368), quick jump to an issue key, which-key hints with a short delay and status bar hints per view (#372).
- Command levels (everyday and advanced, `<space>*`) and a help of pages for the active view whose completeness a test checks (#196, #371).
- Status messages with severity, expiry and a history, so a late write result is not wiped by the next key (#369).
- User key configuration file (the defaults are already data in `keymap.default.json`), a guide, and one settings registry whose writer keeps the user's comments (#373).

These follow Gitunia's proven design (`docs/internal/decisions.md`, 2026-11-03). The Confirm dialog (#370) is in M3 with the mouse paths.

## M5 - Scrum and sprints

Goal: a Scrum team can run its sprint from the terminal, in both project kinds (dev site: `GCS` company-managed, board 6; `GTS` team-managed, board 5).

- Scrum survey on the dev site: sprint, board and estimation endpoints, the `Sprint` field, team-managed differences; fixtures; how to tell a Scrum board from a Kanban one when both team-managed boards say `simple`.
- Sprint client (reads), then the Scrum board: the active sprint as columns, a sprint switcher, sprint header (name, goal, dates, days left, points).
- Backlog as sprint sections (future sprints and the backlog), with counts and points per section.
- Sprint, estimate (story points or the board's estimation field) and epic in issue detail and list columns.
- Move issues to a sprint or to the backlog (one, then a selection, confirmed), rank across sprints.
- Create, edit, start and complete sprints (start and complete have side effects: confirmed, and complete says where open issues go).
- Edit the estimate on an issue.

Done when: on `GCS` and `GTS` the owner can follow the active sprint, plan the backlog into sprints and start and complete a sprint without the web UI.

## M6 - More views

Goal: views beyond boards, as new sources and layouts.

- Creating and removing issue links.
- Saved views: the owner's own combinations of source and layout (built, #191: `:view`, `<space>vv`, `<space>vw`; per profile, local file).
- Quick filters and swimlanes of a board (built, #192); JQL history.
- Kanban Done cutoff like Jira's "Hide done work items after" (built, #674, #681, #698, #699): read from the board view on Jira's GraphQL gateway (experimental), falling back to the setting `board.done-cutoff`; per-tab picker (`<space>vd`, `:donecutoff`), never written to Jira.

## M7 - Pane-based detail, cards and forms

Goal: the issue detail, board cards and the edit and create forms look and navigate like the approved mockups (`ux-screens.md`, "Pane layout for the issue detail, board cards and forms").

- Pane focus (`Ctrl-h/j/k/l`), zoom (`<space>z`) and inner scrolling.
- Detail as panes: title, Description, Sub-tasks table, Comments blocks with cut and expand, Details pane with dropdowns.
- Board card with filled label chips, full type word and aligned assignee chip.
- The detail and the create form as wide overlays; editing in place in the detail (no separate edit form); accent-blue key hints and labels.

Can run before the M8 release check; it changes no Jira calls.

## M8 - Release and going public

Goal: someone else can install it and trust it.

- Packaging: archives for Linux, Windows and macOS, `.deb` and `.rpm`, AUR, winget, Homebrew; version stamped from the tag (`--version`); no macOS signing (Homebrew tap instead, #821).
- Name availability check, README demo recording (hosted outside the repo), landing page, contributor docs, issue templates.
- Idle CPU, startup and memory measurements on a real board and a release check on the first tag.

## Open questions

Each with the recommended default and what changes if it is wrong.

1. ADF editing in M2: edit as Markdown-like text and convert to ADF (default), or a structured editor. If wrong: round-tripping loses formatting on rich descriptions, so the app would warn before saving a description it cannot round-trip. Taken in #161: the writer and `AdfRoundTrip.RoundTrips` exist (`docs/internal/jira-layer.md`, ADF); the create and comment issues show the warning.
2. Scrum reports (burndown, velocity, sprint report) in M5: not included (default; charts do not fit the terminal and the sprint header carries the numbers that matter). If wrong: a text-only sprint report layout follows as its own issue.
3. Restore last session's tabs on start (M1 design, #64): answered yes in #542 (owner request): the tab definitions (not rows) are kept per profile in `tabs.json` and reopened after the first frame; a first start opens the board picker. `startup.restore` turns it off.
4. Telling Scrum from Kanban on team-managed projects (both say `simple`): default is to probe `GET /board/{id}/sprint` once per board and remember the answer per profile. If wrong (the probe is slow or Jira changes the answer): read the project's feature flags instead. Taken in #319: the answer is stored per profile in `board-kinds.json` (`docs/internal/architecture.md`, profile storage).
5. Scrum backlog size (M5, #151): the 10 000-row cap per tab stays. If real backlogs exceed it, the backlog is loaded per sprint section on demand outside `TabState` (a host-owned store), not by raising the cap. If wrong: M5 adds that store first.
6. Confirm for a single transition (M2, #151): answered in #159 (`decisions.md`): confirm when the target status category is `done` or the workflow shows a screen with nothing to fill in; a per-workflow setting stays open.
7. Sprint issue page size (M5, #172): the sprint issue endpoint echoes any `maxResults` (1000 accepted with seven issues; the real cap was not reached because the dev data has too few issues), so the client uses 100 like the board endpoints. If wrong: a sprint with more issues than the cap returns a short page, which offset paging (`startAt` plus the returned count) already survives.
8. Completing a sprint (M5, #172): the public API cannot move unfinished issues on complete (`moveIssuesTo` is 400), so the app completes the sprint and then moves the open issues with a second call. Default: after the complete, offer "move to backlog" or "move to next sprint" in one confirm that says it is two calls. If wrong: an internal Jira endpoint would be needed, which this app does not use.
9. Reading the Done cutoff (M6, #681): the value lives in Jira's board view (GraphQL gateway, `completedIssueSearchCutOffInDays`, per user over the published setting), not in the REST API. Default: read it when a Kanban board opens and fall back to `board.done-cutoff` on any failure, so an Atlassian change never breaks a board. If wrong (the field is removed): only the manual setting and the picker remain. Still unchecked on a real board (owner only).

## Milestone order rationale

Milestones after M2 follow what Gitunia had to redo, not its order: the write pipeline and a UI-free `Geeratya.App` project come first in M2 (running operations in state, rollback, mutual exclusion), the keymap gets command levels and a completeness test, themes get a contrast test each, and version stamping, idle CPU and macOS come before release week. Gitunia's keymap redesign, effects-as-data refactor and theme and console fixes were rework. Keymap and discoverability is M4 and runs first so the palette, status messages, help and levels exist before Scrum (M5).
