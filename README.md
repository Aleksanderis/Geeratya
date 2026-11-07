# Geeratya

A keyboard-first terminal client for Jira Cloud: your Kanban board, backlog and issues in the terminal, with vim-style keys. The mouse is optional but fully functional.

## Why

The Jira web UI is slow and mouse-heavy for the things done many times a day: look at the board, move a card, open an issue, write a comment, create a sub-task. Geeratya does those from the keyboard in a terminal, fast.

## Features

- Kanban board with the board's own columns
- Done cutoff of a Kanban board: follows Jira's own "Hide done work items after" (read, never written), with a quick picker per tab (1 to 4 weeks, all)
- Backlog, with ranking
- Scrum: sprint board, backlog as sprints, move issues between sprints, create, start and complete sprints
- JQL search and saved filters as issue lists
- Issue view: description and comments as formatted text, sub-tasks, an epic's children, linked issues and remote links (Confluence and others)
- Change status, also by moving a card to another column
- Create, edit and delete epics, stories, tasks, bugs and sub-tasks, with each project's required fields
- Add, edit and delete comments
- Copy text from the issue detail (Description, comments, fields) to the system clipboard: select with `v` or the mouse, yank with `y`
- Company-managed and team-managed projects alike
- Profiles: saved sites and accounts, one active at a time, quick switch
- Tabs over the active site, local cache so views open instantly
- Start target on the command line: `geeratya GEN-123`, `--board`, `--backlog`, `--jql`, `--filter` open one tab
- Command palette, configurable vim-style keys and full mouse support
- Settings screen: change the theme and the options visually, saved at once

Attachments and images are shown as links that open in the browser, never rendered in the terminal.

Not included: Scrum reports and charts (burndown, velocity), Jira Service Management, Jira Data Center or Server, several sites open at once, rendering images or files, Jira administration (projects, workflows, schemes, users, permissions), plugins.

## Status

In planning. See `docs/internal/roadmap.md`.

## Docs

- [User guide](docs/user-guide.md): install, connect, keys and everyday tasks
- [Key reference](docs/keymap.md), [customising keys](docs/customising-keys.md) and [settings](docs/settings.md)
- [Contributing](CONTRIBUTING.md) and [security policy](SECURITY.md)
- Design documents are in `docs/` (internal notes for maintainers)
- Project conventions are in [CLAUDE.md](CLAUDE.md)

## License

MIT License ([LICENSE](LICENSE)).
