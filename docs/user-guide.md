# Geeratya user guide

Geeratya is a keyboard-first terminal client for Jira Cloud. This page gets you from nothing to a board on screen and shows the everyday keys. The scope is in the [README](../README.md).

## Install

Needs a Jira Cloud site, an Atlassian account on it and an API token (below). Any terminal works; truecolor looks best. No release is published yet (see the milestone "M8 - Release and going public"), so for now build from source (once releases exist, macOS installs with Homebrew, see below):

- Prerequisites: the .NET SDK version from `global.json` and the Native AOT prerequisites.
  - Linux (Debian/Ubuntu): `clang` and `zlib1g-dev`. Arch: `sudo pacman -S --needed clang zlib dotnet-sdk`.
  - Windows: "Desktop development with C++".
  - macOS: Xcode command line tools.
- Build: `dotnet publish src/Geeratya.Tui -c Release -o publish`
- Run: `publish/geeratya`, or copy the binary to a folder on your `PATH`.

Releases will add archives for Linux, Windows and macOS and system packages; this section will then list them. macOS is the least tested system.

Where the token is kept: Linux uses `secret-tool` (libsecret; install it, for example the `libsecret-tools` package), Windows uses Credential Manager, macOS uses the Keychain. Without a secret store you can set the environment variable `GEERATYA_TOKEN` instead (it works while exactly one profile exists).

### macOS (Homebrew)

```sh
brew install geeratya/tap/geeratya
```

The tap `Geeratya/homebrew-tap` serves the Apple silicon (arm64) build. The macOS binary is **not notarised** and not signed with an Apple developer identity (at most the linker's ad-hoc signature). Homebrew downloads without the quarantine flag, so Gatekeeper does not block it. If you download the release archive in a browser instead, macOS marks it as quarantined and refuses to run it; clear the flag once after unpacking:

```sh
xattr -d com.apple.quarantine geeratya
```

## Start and connect

1. Create an API token at <https://id.atlassian.com/manage-profile/security/api-tokens> (a plain token without scopes; scoped tokens are not supported yet). The token is a secret: never paste it into an issue, a screenshot or a shared terminal log.
2. Run `geeratya`. Add a profile with `:profile add` (or `Ctrl-a` in the profile manager): a name, the site (`your-site.atlassian.net`), your account email and the token. `Alt-t` tests the connection before saving. The token goes to the system secret store; `profiles.json` holds only site and email.
3. Open a board: `:board` (a name picks it), or `geeratya --board <name>`. After the connection check, a first start shows the board picker.

Start from the command line (opens as a tab on top of the restored ones): `geeratya GEN-123`, `--board <name>`, `--backlog <name>`, `--jql <query>`, `--filter <name>`, and `--profile <name>` to use another saved profile for the run. `geeratya --help` lists everything, `--version` prints the version.

Lost? `?` shows the keys of the current screen, `:` opens the command palette, `q` quits.

## The screen

- A tab row on top (one tab per view of the active site), the view in the middle, a status bar with hints below.
- A view is a source (board, backlog, JQL, saved filter) shown as columns or a list. `Enter` opens an issue; the detail shows description, comments, sub-tasks, links and attachments (attachments are listed with a link that opens in the browser, never drawn).
- `Ctrl-w h/j/k/l` (or `Ctrl-h/j/k/l`) moves between panes, `<space>z` zooms one. `Tab` and `Shift-Tab` switch tabs.

| Key | Does |
|---|---|
| `?` | Keys of the current screen |
| `:` or `Ctrl-p` | Command palette |
| `q` | Close an overlay, or quit |
| `<space>,` | Settings |
| `<space>*` | Show or hide the rarer commands |

Movement is vim-style: `h j k l`, `gg`, `G`, `Ctrl-d`, `Ctrl-u`. Full table: [keymap.md](keymap.md). To rebind: [customising-keys.md](customising-keys.md).

## Everyday tasks

- **Move a card:** `Alt-l` / `Alt-h` to the next or previous column, or `<space>is` to pick a status. A transition that asks for fields shows a form.
- **Open an issue:** `Enter`; `<space>g` jumps to a key; `<space>o` opens it in the browser; `<space>p` opens its parent.
- **Search:** `:jql <query>`, `:filter <name>`; saved views: `<space>vw` saves the tab, `<space>vv` opens one.
- **Tabs:** `<space>t` opens a new one, `gt` / `gT` or `Alt-1`..`Alt-9` switch, `<space>c` closes. The tabs are restored on the next start.
- **Create:** `<space>ic` an issue (type, then the form with the project's required fields), `<space>iC` a sub-task.
- **Edit:** `i` edits the title or description in place, `<space>ie` the title, `:edit` the fields, `:assign-me`, `:labels`, `:priority`.
- **Comments:** `<space>ma` adds, `<space>me` edits, `<space>md` deletes.
- **Backlog and ranking:** `:backlog`, then `Alt-k` / `Alt-j` rank up and down.
- **Scrum:** `<space>sm` moves the selected issues to a sprint, `<space>sb` to the backlog, `<space>sn` creates a sprint, `<space>ss` starts it, `<space>sc` completes it (the confirmation says where open issues go), `<space>sv` switches the sprint shown.
- **Kanban Done cutoff:** the Done column follows Jira's "Hide done work items after"; `<space>vd` picks 1 to 4 weeks or all for the tab.
- **Copy text:** in the issue detail, `v` (or the mouse) selects, `y` copies to the system clipboard.
- **Several sites:** `:profile switch <name>`. One site is active at a time; switching drops the other site's views.
- **Undo:** Jira has no undo. Deleting an issue or a comment, completing a sprint and similar actions ask first and say what changes.

The mouse works everywhere but is optional.

## Configuration

| What | Linux and macOS | Windows |
|---|---|---|
| Config (`profiles.json`, `settings.json`, `keymap.json`, `themes/`) | `$XDG_CONFIG_HOME/geeratya`, else `~/.config/geeratya` | `%APPDATA%\Geeratya` |
| State (open tabs, history, saved views) | `$XDG_STATE_HOME/geeratya`, else `~/.local/state/geeratya` | `%LOCALAPPDATA%\Geeratya\State` |
| Cache | `$XDG_CACHE_HOME/geeratya`, else `~/.cache/geeratya` | `%LOCALAPPDATA%\Geeratya\Cache` |

The settings screen (`<space>,`) and `:set` change the options; see [settings.md](settings.md). Deleting the cache folder is always safe.

## Trouble

- `401` in the banner: the token or the email is wrong (they look the same to Jira). `:profile edit <name>` and type the token again.
- Rate limited (`429`): Geeratya waits as Jira asks.
- Report a bug with the issue template; it asks for `geeratya --version`, your OS and terminal. Remove the site name, emails and any token from pasted text. Security problems: [SECURITY.md](../SECURITY.md).
