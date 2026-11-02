# Roadmap

Milestone goals and open questions. Work is tracked as GitHub issues per milestone; this file holds goals and "Done when", not status. Features and exclusions are in the README.

## M0 - Foundation

Goal: everything the feature milestones build on, and real knowledge of how Jira behaves.

- Solution skeleton with Native AOT publish, layering and platform abstraction (`docs/architecture.md`).
- Jira survey on the dev site: recorded, scrubbed responses for boards, backlog, ranking, issue hierarchy in both project kinds, create and edit metadata, ADF, search paging and errors; findings in `docs/jira-layer.md`.
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

## M3 - Scrum and sprints

Goal: a Scrum team can run its sprint from the terminal, in both project kinds (dev site: `GCS` company-managed, board 6; `GTS` team-managed, board 5).

- Scrum survey on the dev site: sprint, board and estimation endpoints, the `Sprint` field, team-managed differences; fixtures; how to tell a Scrum board from a Kanban one when both team-managed boards say `simple`.
- Sprint client (reads), then the Scrum board: the active sprint as columns, a sprint switcher, sprint header (name, goal, dates, days left, points).
- Backlog as sprint sections (future sprints and the backlog), with counts and points per section.
- Sprint, estimate (story points or the board's estimation field) and epic in issue detail and list columns.
- Move issues to a sprint or to the backlog (one, then a selection, confirmed), rank across sprints.
- Create, edit, start and complete sprints (start and complete have side effects: confirmed, and complete says where open issues go).
- Edit the estimate on an issue.

Done when: on `GCS` and `GTS` the owner can follow the active sprint, plan the backlog into sprints and start and complete a sprint without the web UI.

## M4 - More views

Goal: views beyond boards, as new sources and layouts.

- Creating and removing issue links.
- Saved views: the owner's own combinations of source and layout.
- Quick filters and swimlanes of a board; JQL history.

## M5 - Keymap and discoverability

Goal: every command is findable and the keys are the user's.

- Command palette, quick jump to an issue key, which-key hints, status bar hints per view.
- Command levels (everyday and advanced) and a help whose completeness a test checks.
- User key configuration file (the defaults are already data in `keymap.default.json`) and a guide.

## M6 - Themes, mouse and terminals

Goal: it looks right and works by mouse in the terminals people really use.

- Built-in and user themes; a contrast test per theme (a faint selection bar is the Gitunia lesson).
- Full mouse support, every keyboard action reachable by mouse (#46 and the rest).
- Older and classic consoles, the ASCII glyph set, 16-colour fallback; Windows and macOS checks.

## M7 - Release and going public

Goal: someone else can install it and trust it.

- Packaging: archives for Linux, Windows and macOS, `.deb` and `.rpm`, AUR, winget, Homebrew; version stamped from the tag (`--version`); macOS signing.
- Name availability check, README demo recording (hosted outside the repo), landing page, contributor docs, issue templates.
- Idle CPU, startup and memory measurements on a real board and a release check on the first tag.

## Open questions

Each with the recommended default and what changes if it is wrong.

1. ADF editing in M2: edit as Markdown-like text and convert to ADF (default), or a structured editor. If wrong: round-tripping loses formatting on rich descriptions, so the app would warn before saving a description it cannot round-trip.
2. Scrum reports (burndown, velocity, sprint report) in M3: not included (default; charts do not fit the terminal and the sprint header carries the numbers that matter). If wrong: a text-only sprint report layout follows as its own issue.
3. Restore last session's tabs on start (M1 design, #64): default is no, a start has no tabs and opening a view hits the disk cache. If wrong: a small follow-up persists the tab definitions (not rows) per profile and reopens them after the first frame.
4. Telling Scrum from Kanban on team-managed projects (both say `simple`): default is to probe `GET /board/{id}/sprint` once per board and remember the answer per profile. If wrong (the probe is slow or Jira changes the answer): read the project's feature flags instead.
5. Scrum backlog size (M3, #151): the 10 000-row cap per tab stays. If real backlogs exceed it, the backlog is loaded per sprint section on demand outside `TabState` (a host-owned store), not by raising the cap. If wrong: M3 adds that store first.
6. Confirm for a single transition (M2, #151): default is no confirm, because Jira does not expose workflow post functions and a move can be reversed by another move. If wrong: confirm when the target status category is `done`, or per workflow in a setting.
