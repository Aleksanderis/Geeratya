# Geeratya

A keyboard-first terminal client for Jira Cloud: your Kanban board, backlog and issues in the terminal, with vim-style keys. The mouse is optional but fully functional.

## Why

The Jira web UI is slow and mouse-heavy for the things done many times a day: look at the board, move a card, open an issue, write a comment, create a sub-task. Geeratya does those from the keyboard in a terminal, fast.

## Features

- Kanban board with the board's own columns
- Backlog, with ranking
- Scrum: sprint board, backlog as sprints, move issues between sprints, create, start and complete sprints
- JQL search and saved filters as issue lists
- Issue view: description and comments as formatted text, sub-tasks, an epic's children, linked issues and remote links (Confluence and others)
- Change status, also by moving a card to another column
- Create, edit and delete epics, stories, tasks, bugs and sub-tasks, with each project's required fields
- Add, edit and delete comments
- Company-managed and team-managed projects alike
- Profiles: saved sites and accounts, one active at a time, quick switch
- Tabs over the active site, local cache so views open instantly
- Command palette, configurable vim-style keys and full mouse support
- Settings screen: change the theme and the options visually, saved at once

Attachments and images are shown as links that open in the browser, never rendered in the terminal.

Not included: Scrum reports and charts (burndown, velocity), Jira Service Management, Jira Data Center or Server, several sites open at once, rendering images or files, Jira administration (projects, workflows, schemes, users, permissions), plugins.

## Status

In planning. See `docs/roadmap.md`.

## Docs

- Design documents are in `docs/`
- Project conventions are in [CLAUDE.md](CLAUDE.md)

## License

MIT License ([LICENSE](LICENSE)).
