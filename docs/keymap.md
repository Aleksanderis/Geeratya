# Key reference

Generated from `src/Geeratya.Core/Keymap/keymap.default.json` by `scripts/gen-keymap-doc.sh`; do not edit by hand. Design rules are in `docs/ux-keymap.md`.

## Normal mode

| Keys | Action |
|---|---|
| `j` | Move down |
| `Down` | Move down |
| `k` | Move up |
| `Up` | Move up |
| `h` | Move left |
| `Left` | Move left |
| `l` | Move right |
| `Right` | Move right |
| `g g` | Go to the top |
| `Home` | Go to the top |
| `G` | Go to the bottom |
| `End` | Go to the bottom |
| `Ctrl-d` | Page down |
| `PageDown` | Page down |
| `Ctrl-u` | Page up |
| `PageUp` | Page up |
| `?` | Show or hide help |
| `:` | Open the command palette |
| `q` | Quit |
| `Esc` | Cancel or close |
| `Ctrl-c` | Quit |
| `Enter` | Open the selected item |
| `0` | Go to the first column |
| `$` | Go to the last column |
| `] ]` | Next section of the issue |
| `[ [` | Previous section of the issue |
| `Tab` | Next tab |
| `g t` | Next tab |
| `Shift-Tab` | Previous tab |
| `g T` | Previous tab |
| `Alt-1` | Go to tab 1 |
| `Alt-2` | Go to tab 2 |
| `Alt-3` | Go to tab 3 |
| `Alt-4` | Go to tab 4 |
| `Alt-5` | Go to tab 5 |
| `Alt-6` | Go to tab 6 |
| `Alt-7` | Go to tab 7 |
| `Alt-8` | Go to tab 8 |
| `Alt-9` | Go to tab 9 |
| `Space t` | Open the source picker for a new tab |
| `Space c` | Close the tab |
| `Space r` | Refresh the view (also retries after an error) |
| `Space l` | Switch between list and columns |
| `Space o` | Open the issue in the browser |
| `Space p` | Open the parent issue |
| `Space m a` | Add a comment to the issue |
| `Space m e` | Edit the comment under the cursor |
| `Space m d` | Delete the comment under the cursor |

## Help mode

| Keys | Action |
|---|---|
| `j` | Move down |
| `Down` | Move down |
| `k` | Move up |
| `Up` | Move up |
| `?` | Show or hide help |
| `Esc` | Close help |
| `q` | Close help |
| `Ctrl-c` | Quit |

## Command mode

| Keys | Action |
|---|---|
| `Enter` | Run the typed command |
| `Backspace` | Delete the last character |
| `Esc` | Cancel or close |
| `Ctrl-c` | Quit |

## Confirm mode

| Keys | Action |
|---|---|
| `y` | Confirm |
| `n` | Decline |
| `Esc` | Decline |

## Insert mode

| Keys | Action |
|---|---|
| `Enter` | Accept the field and go to the next |
| `Backspace` | Delete the last character |
| `Esc` | Cancel or close |
| `Ctrl-c` | Cancel or close |
| `Up` | Previous source in the picker |
| `Ctrl-p` | Previous source in the picker |
| `Down` | Next source in the picker |
| `Ctrl-n` | Next source in the picker |
| `Ctrl-s` | Save the comment |
| `Alt-Enter` | Save the comment |
| `Left` | Move the cursor left |
| `Right` | Move the cursor right |
| `Home` | Move the cursor to the start of the row |
| `End` | Move the cursor to the end of the row |
| `Delete` | Delete the character under the cursor |

## Palette commands

| Command | What it does |
|---|---|
| `:jql <query>` | Open a JQL query in a new tab |
| `:filter [name]` | Open a saved filter in a new tab |
| `:board [name]` | Open a board as columns in a new tab |
| `:backlog [name]` | Open a board's backlog in a new tab |
| `:tab new|close|next|prev|<n>` | Open the source picker, close or switch tabs |
| `:profile add|list|switch <name>|remove <name>|check` | Manage Jira profiles |
| `:help` | Show or hide help |
| `:quit` | Quit |
