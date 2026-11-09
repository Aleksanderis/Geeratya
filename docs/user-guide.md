# Geeratya user guide

Geeratya is a keyboard-first terminal client for Jira Cloud. This page gets you from nothing to a board on screen and shows the everyday keys. The scope is in the [README](../README.md).

## Install

Needs a Jira Cloud site, an Atlassian account on it and an API token (below). Any terminal works; truecolor looks best. No release is published yet (see the milestone "M8 - Release and going public"): every route below works once the first release is out.

One command installs the latest release; run it again to update.

#### Linux and macOS

In a shell (x64 or arm64 with glibc, so not Alpine; macOS on Apple silicon):

```sh
curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.sh | sh
```

The script checks the download against the release's `SHA256SUMS` and puts `geeratya` in `~/.local/bin` (no root or sudo; `GEERATYA_INSTALL_DIR` changes the folder). Failed downloads are retried. On macOS it prints the Gatekeeper hint (see [macOS (Gatekeeper)](#macos-gatekeeper)). To pin a version, set the variable on the `sh` side of the pipe:

```sh
curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.sh \
  | GEERATYA_VERSION=0.1.0 sh
```

For a native package use the `.deb`, `.rpm` or Arch package below.

#### Windows

In PowerShell (Windows PowerShell 5.1 or pwsh, x64 or arm64):

```powershell
irm https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.ps1 | iex
```

The script checks the download against the release's `SHA256SUMS`, installs to `%LOCALAPPDATA%\Geeratya` and adds it to your user `PATH`, no admin rights. A download that fails with a server error (for example `504 Gateway Time-out`) is retried up to four times with growing waits; if it still fails, run the command again in a minute. To pin a version:

```powershell
$env:GEERATYA_VERSION = '0.1.0'
irm https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.ps1 | iex
```

### Other ways

All files are on the [Releases](https://github.com/Aleksanderis/Geeratya/releases) page unless noted.

- Debian / Ubuntu: `sudo apt install ./geeratya_<version>_amd64.deb` (`_arm64.deb` on arm64).
- Fedora / RHEL: `sudo dnf install ./geeratya-<version>-1.x86_64.rpm` (`.aarch64.rpm` on arm64).
- Arch / Omarchy: `sudo pacman -U geeratya-<version>-1-x86_64.pkg.tar.zst` (`aarch64` on arm64). The AUR package `geeratya-bin` is planned, not registered yet.
- Windows, winget: `winget install Aleksanderis.Geeratya`. Not available until the package is accepted by winget after the first release; until then use the archive.
- Homebrew (macOS and Linux): `brew install aleksanderis/tap/geeratya`, once the first release is published.
- Windows and Linux archives (x64 and arm64): the `.zip` or `.tar.gz`; unpack it and put `geeratya` (`geeratya.exe`) in a folder on your `PATH`.
- macOS archive (Apple silicon): `tar xzf geeratya-<version>-osx-arm64.tar.gz`, then put `geeratya` on your `PATH`. macOS should work but is the least tested system, and the binary is unsigned (see [macOS (Gatekeeper)](#macos-gatekeeper)).
- From source: see [CONTRIBUTING.md](../CONTRIBUTING.md).

Where the token is kept: Linux uses `secret-tool` (libsecret; install it, for example the `libsecret-tools` package), Windows uses Credential Manager, macOS uses the Keychain. Without a secret store you can set the environment variable `GEERATYA_TOKEN` instead (it works while exactly one profile exists).

### macOS (Gatekeeper)

The macOS binary is **not notarised** and not signed with an Apple developer identity (at most the linker's ad-hoc signature). Homebrew (the tap `Aleksanderis/homebrew-tap` serves the Apple silicon build) downloads without the quarantine flag, so Gatekeeper does not block it. If you download the release archive in a browser instead, macOS marks it as quarantined and Gatekeeper asks you to allow it or refuses to run it; clear the flag once after unpacking:

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
