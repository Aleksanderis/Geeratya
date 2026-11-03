# Key reference

Generated from `src/Geeratya.Core/Keymap/keymap.default.json` by `scripts/gen-keymap-doc.sh`; do not edit by hand. Design rules are in `docs/ux-keymap.md`. Level: everyday actions show in help, the palette, which-key and the hints by default; advanced ones show when the command level is advanced (`<space>*` or `:set commandlevel`). A level hides, it never disables: every key works at either level.

## Normal mode

| Keys | Action | Level |
|---|---|---|
| `j` | Move down | everyday |
| `Down` | Move down | everyday |
| `k` | Move up | everyday |
| `Up` | Move up | everyday |
| `h` | Move left | everyday |
| `Left` | Move left | everyday |
| `l` | Move right | everyday |
| `Right` | Move right | everyday |
| `g g` | Go to the top | everyday |
| `Home` | Go to the top | everyday |
| `G` | Go to the bottom | everyday |
| `End` | Go to the bottom | everyday |
| `Ctrl-d` | Page down | everyday |
| `PageDown` | Page down | everyday |
| `Ctrl-u` | Page up | everyday |
| `PageUp` | Page up | everyday |
| `?` | Show or hide help | everyday |
| `:` | Open the command palette | everyday |
| `Ctrl-p` | Open the command palette | everyday |
| `q` | Quit | everyday |
| `Esc` | Cancel or close | everyday |
| `Ctrl-c` | Quit | everyday |
| `Enter` | Open the selected item | everyday |
| `0` | Go to the first column | everyday |
| `$` | Go to the last column | everyday |
| `] ]` | Next section of the issue | everyday |
| `[ [` | Previous section of the issue | everyday |
| `Tab` | Next tab | everyday |
| `g t` | Next tab | everyday |
| `Shift-Tab` | Previous tab | everyday |
| `g T` | Previous tab | everyday |
| `Alt-1` | Go to tab 1 | everyday |
| `Alt-2` | Go to tab 2 | everyday |
| `Alt-3` | Go to tab 3 | everyday |
| `Alt-4` | Go to tab 4 | everyday |
| `Alt-5` | Go to tab 5 | everyday |
| `Alt-6` | Go to tab 6 | everyday |
| `Alt-7` | Go to tab 7 | everyday |
| `Alt-8` | Go to tab 8 | everyday |
| `Alt-9` | Go to tab 9 | everyday |
| `Space t` | Open the source picker for a new tab | everyday |
| `Space c` | Close the tab | everyday |
| `Space r` | Refresh the view (also retries after an error) | everyday |
| `Space l` | Switch between list and columns | everyday |
| `Space o` | Open the issue in the browser | everyday |
| `Space p` | Open the parent issue | everyday |
| `Space a` | Open the actions menu of the selected issue | everyday |
| `Space A` | Open the actions menu of the tab and the view | everyday |
| `Space m a` | Add a comment to the issue | everyday |
| `Space m e` | Edit the comment under the cursor | everyday |
| `Space m d` | Delete the comment under the cursor | advanced |
| `Space i s` | Change the status of the issue | everyday |
| `Alt-l` | Move the card to the next column | everyday |
| `Alt-h` | Move the card to the previous column | everyday |
| `Alt-k` | Rank the issue up (a count moves it that many places) | everyday |
| `Alt-j` | Rank the issue down (a count moves it that many places) | everyday |
| `Space k t` | Rank the issue to the top of the list or column | advanced |
| `Space k b` | Rank the issue to the bottom of the list or column | advanced |
| `Space k k` | Pick up the issue, then drop it at the issue under the cursor | advanced |
| `Space i d` | Delete the issue (asks first; Jira cannot undo it) | advanced |
| `Space i c` | Create an issue (type picker, then the form) | everyday |
| `Space i C` | Create a sub-task of the selected issue | everyday |
| `Space i l` | Link the issue to another issue | advanced |
| `Space i u` | Remove the link under the cursor (asks first; Jira cannot undo it) | advanced |
| `Space i e` | Edit fields of the issue (summary, labels, priority, description) | everyday |
| `Space i a` | Assign the issue: search people, or unassign | everyday |
| `Space i m` | Assign the issue to me | everyday |
| `Space g` | Jump to an issue by key | everyday |
| `Space n` | Show the message history | everyday |
| `Space x` | Dismiss the status message | everyday |
| `Space *` | Switch the command level for the session | everyday |

## Help mode

| Keys | Action | Level |
|---|---|---|
| `j` | Move down | everyday |
| `Down` | Move down | everyday |
| `k` | Move up | everyday |
| `Up` | Move up | everyday |
| `?` | Show or hide help | everyday |
| `Space *` | Switch the command level for the session | everyday |
| `Esc` | Close help | everyday |
| `q` | Close help | everyday |
| `Ctrl-c` | Quit | everyday |

## Command mode

| Keys | Action | Level |
|---|---|---|
| `Enter` | Run the typed command | everyday |
| `Backspace` | Delete the last character | everyday |
| `Up` | Previous row in the picker | everyday |
| `Ctrl-p` | Previous row in the picker | everyday |
| `Down` | Next row in the picker | everyday |
| `Ctrl-n` | Next row in the picker | everyday |
| `Esc` | Cancel or close | everyday |
| `Ctrl-c` | Quit | everyday |

## Confirm mode

| Keys | Action | Level |
|---|---|---|
| `y` | Confirm | everyday |
| `n` | Decline | everyday |
| `Esc` | Decline | everyday |

## Insert mode

| Keys | Action | Level |
|---|---|---|
| `Enter` | Accept the field and go to the next | everyday |
| `Backspace` | Delete the last character | everyday |
| `Esc` | Cancel or close | everyday |
| `Ctrl-c` | Cancel or close | everyday |
| `Up` | Previous row in the picker | everyday |
| `Ctrl-p` | Previous row in the picker | everyday |
| `Down` | Next row in the picker | everyday |
| `Ctrl-n` | Next row in the picker | everyday |
| `Ctrl-x` | Remove the highlighted profile (asks first; the stored token is deleted) | everyday |
| `Tab` | Next field of the form | everyday |
| `Shift-Tab` | Previous field of the form | everyday |
| `Ctrl-s` | Save the form | everyday |
| `Ctrl-g` | Open the new issue after it is created (on or off) | everyday |
| `Alt-Enter` | Save the form | everyday |
| `Left` | Move the cursor left | everyday |
| `Right` | Move the cursor right | everyday |
| `Home` | Move the cursor to the start of the row | everyday |
| `End` | Move the cursor to the end of the row | everyday |
| `Delete` | Delete the character under the cursor | everyday |
| `PageUp` | Move the cursor up half a screen | everyday |
| `PageDown` | Move the cursor down half a screen | everyday |
| `Ctrl-Left` | Move the cursor to the start of the word | everyday |
| `Ctrl-Right` | Move the cursor to the end of the word | everyday |
| `Ctrl-o` | Open the issue page in the browser from the form | everyday |

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
| `:set [hints|whichkey|commandlevel|theme] [value]` | List the settings, show one, or change one (saved) |
| `:theme [name]` | Switch the colour theme (no name: list the families); saved |
| `:keys reload` | Read keymap.json again and apply it |
| `:help` | Show or hide help |
| `:quit` | Quit |
