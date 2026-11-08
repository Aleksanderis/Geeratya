<h1 align="center">Geeratya</h1>

<p align="center">
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT license"></a>
<img src="https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-informational" alt="Platforms: Linux, Windows, macOS">
<img src="https://img.shields.io/badge/.NET-10%20Native%20AOT-512BD4?logo=dotnet" alt=".NET 10 Native AOT">
</p>

A keyboard-first terminal client for Jira Cloud: board, backlog and issues in a terminal, driven by vim-style keys. The mouse is optional but fully functional.

[![Geeratya demo: a sprint board, an issue, a Kanban board and a status change](https://geeratya.github.io/media/demo.gif)](https://geeratya.github.io/)

Video version: [demo.webm](https://geeratya.github.io/media/demo.webm).

## Why

The Jira web UI is slow and mouse-heavy for what you do many times a day: check the board, move a card, open an issue, comment, add a sub-task. Geeratya does all of that from the keyboard, in a terminal, quickly.

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

## Screenshots

![Kanban board with the board's own columns](https://geeratya.github.io/media/board.webp)

![Issue detail with description, fields and sub-tasks](https://geeratya.github.io/media/issue-detail.webp)

![Backlog and sprints](https://geeratya.github.io/media/backlog.webp)

![JQL search as an issue list](https://geeratya.github.io/media/jql-search.webp)

![An issue after its status was changed](https://geeratya.github.io/media/status-changed.webp)

More on the [website](https://geeratya.github.io/).

## Install

Needs a Jira Cloud site, an Atlassian account on it and an API token. The connect steps are in the [user guide](docs/user-guide.md).

```
# macOS (Apple silicon), Homebrew
brew install aleksanderis/tap/geeratya

# any platform, from source
dotnet publish src/Geeratya.Tui -c Release -o publish && publish/geeratya
```

Other ways: release archives for Linux, Windows and macOS on the [Releases](https://github.com/Aleksanderis/Geeratya/releases) page. The macOS binary is not notarised, see the [user guide](docs/user-guide.md#macos-homebrew) if macOS refuses to run it. No release is published yet, so for now build from source; the brew and archive routes work once the first release is out (see `docs/internal/roadmap.md`).

## Quick start

```
geeratya                    # restore the last session, or pick a board
geeratya GEN-123            # open one issue in a tab
geeratya --board <name>     # also --backlog, --jql, --filter, --profile
```

`?` shows the keys of the current screen, `:` opens the command palette, `q` quits. The full list is in the [key reference](docs/keymap.md).

## Docs

- [User guide](docs/user-guide.md), [key reference](docs/keymap.md), [customising keys](docs/customising-keys.md) and [settings](docs/settings.md)
- Website: <https://geeratya.github.io/>
- Design documents are in `docs/internal/` (maintainer notes)
- Project conventions are in [CLAUDE.md](CLAUDE.md)

## Contributing, security, license

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Licensed under the MIT License ([LICENSE](LICENSE)).

## Support

If Geeratya saves you time, you can [buy me a coffee](https://buymeacoffee.com/aleksanderis).

## Disclaimer

Geeratya is an independent project and is not affiliated with or endorsed by Atlassian. Jira is a trademark of Atlassian.
