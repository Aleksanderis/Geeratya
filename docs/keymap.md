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
| `Space a` | Open the actions menu of the selected issue |
| `Space A` | Open the actions menu of the tab and the view |
| `Space m a` | Add a comment to the issue |
| `Space m e` | Edit the comment under the cursor |
| `Space m d` | Delete the comment under the cursor |
| `Space i s` | Change the status of the issue |
| `Alt-l` | Move the card to the next column |
| `Alt-h` | Move the card to the previous column |
| `Alt-k` | Rank the issue up (a count moves it that many places) |
| `Alt-j` | Rank the issue down (a count moves it that many places) |
| `Space k t` | Rank the issue to the top of the list or column |
| `Space k b` | Rank the issue to the bottom of the list or column |
| `Space k k` | Pick up the issue, then drop it at the issue under the cursor |
| `Space i d` | Delete the issue (asks first; Jira cannot undo it) |
| `Space i c` | Create an issue (type picker, then the form) |
| `Space i C` | Create a sub-task of the selected issue |
| `Space i l` | Link the issue to another issue |
| `Space i u` | Remove the link under the cursor (asks first; Jira cannot undo it) |
| `Space i e` | Edit fields of the issue (summary, labels, priority, description) |
| `Space i a` | Assign the issue: search people, or unassign |
| `Space i m` | Assign the issue to me |
| `Space g` | Jump to an issue by key |
| `Space n` | Show the message history |
| `Space x` | Dismiss the status message |

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
| `Up` | Previous row in the picker |
| `Ctrl-p` | Previous row in the picker |
| `Down` | Next row in the picker |
| `Ctrl-n` | Next row in the picker |
| `Ctrl-x` | Remove the highlighted profile (asks first; the stored token is deleted) |
| `Tab` | Next field of the form |
| `Shift-Tab` | Previous field of the form |
| `Ctrl-s` | Save the form |
| `Ctrl-g` | Open the new issue after it is created (on or off) |
| `Alt-Enter` | Save the form |
| `Left` | Move the cursor left |
| `Right` | Move the cursor right |
| `Home` | Move the cursor to the start of the row |
| `End` | Move the cursor to the end of the row |
| `Delete` | Delete the character under the cursor |
| `PageUp` | Move the cursor up half a screen |
| `PageDown` | Move the cursor down half a screen |
| `Ctrl-Left` | Move the cursor to the start of the word |
| `Ctrl-Right` | Move the cursor to the end of the word |
| `Ctrl-o` | Open the issue page in the browser from the form |

## Palette commands

| Command | What it does |
|---|---|
| `:jql [query]` | Open a JQL query in a new tab (no query: pick from recent ones) |
| `:filter [name]` | Open a saved filter in a new tab |
| `:board [name]` | Open a board as columns in a new tab |
| `:backlog [name]` | Open a board's backlog in a new tab |
| `:history clear` | Forget the recent JQL queries and issues of this profile |
| `:tab new|close|next|prev|<n>` | Open the source picker, close or switch tabs |
| `:edit` | Edit the fields of the selected or shown issue (field form) |
| `:status [next|prev]` | Change the status of the selected or shown issue; next or prev moves the card a column |
| `:labels` | Edit the labels of the selected or shown issue (field form) |
| `:create [subtask]` | Create an issue, or a sub-task of the selected issue (field form) |
| `:priority` | Change the priority of the selected or shown issue (field form) |
| `:assign` | Assign the selected or shown issue: search people, or unassign |
| `:assign-me` | Assign the selected or shown issue to me |
| `:profile add|list|switch <name>|remove <name>|check` | Manage Jira profiles |
| `:set [hints|whichkey|theme] [value]` | List the settings, show one, or change one (saved) |
| `:theme [name]` | Switch the colour theme (no name: list the families); saved |
| `:keys reload` | Read keymap.json again and apply it |
| `:help` | Show or hide help |
| `:quit` | Quit |
