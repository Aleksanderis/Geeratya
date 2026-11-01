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

Goal: the everyday writes.

- Transitions, including moving a card to another column.
- Create epics, stories, tasks, bugs and sub-tasks with each project's required fields; edit fields; delete with confirmation.
- Add, edit and delete comments (ADF editing).
- Rank in the backlog and on the board.

Done when: the owner can do a normal working day in Geeratya and open the web UI only for administration.

## M3 - More views

Goal: views beyond the owner's board, as new sources and layouts.

- Scrum boards and sprints (only if needed by then).
- Creating and removing issue links.
- Saved views: the owner's own combinations of source and layout.

## M4 - Polish

- Command palette, quick jump to an issue key, full mouse support, themes.
- Release packaging (Linux, Windows, macOS archives).

## Open questions

Each with the recommended default and what changes if it is wrong.

1. ADF editing in M2: edit as Markdown-like text and convert to ADF (default), or a structured editor. If wrong: round-tripping loses formatting on rich descriptions, so the app would warn before saving a description it cannot round-trip.
2. Scrum boards: in M3 only if the owner's team moves to sprints (default: kept in M3, picked last). If wrong: it moves up to M2 next to ranking.
3. Restore last session's tabs on start (M1 design, #64): default is no, a start has no tabs and opening a view hits the disk cache. If wrong: a small follow-up persists the tab definitions (not rows) per profile and reopens them after the first frame.
