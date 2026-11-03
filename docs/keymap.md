# Key reference

Generated from `src/Geeratya.Core/Keymap/keymap.default.json` by `scripts/gen-keymap-doc.sh`; do not edit by hand. The Id column is the action id to use in `keymap.json` (`docs/customising-keys.md`). Design rules are in `docs/ux-keymap.md`. Level: everyday actions show in help, the palette, which-key and the hints by default; advanced ones show when the command level is advanced (`<space>*` or `:set commandlevel`). A level hides, it never disables: every key works at either level.

## Normal mode

| Keys | Action | Id | Level |
|---|---|---|---|
| `j` | Move down | `nav.down` | everyday |
| `Down` | Move down | `nav.down` | everyday |
| `k` | Move up | `nav.up` | everyday |
| `Up` | Move up | `nav.up` | everyday |
| `h` | Move left | `nav.left` | everyday |
| `Left` | Move left | `nav.left` | everyday |
| `l` | Move right | `nav.right` | everyday |
| `Right` | Move right | `nav.right` | everyday |
| `gg` | Go to the top | `nav.top` | everyday |
| `Home` | Go to the top | `nav.top` | everyday |
| `G` | Go to the bottom | `nav.bottom` | everyday |
| `End` | Go to the bottom | `nav.bottom` | everyday |
| `Ctrl-d` | Page down | `nav.page-down` | everyday |
| `PageDown` | Page down | `nav.page-down` | everyday |
| `Ctrl-u` | Page up | `nav.page-up` | everyday |
| `PageUp` | Page up | `nav.page-up` | everyday |
| `?` | Show or hide help | `help.toggle` | everyday |
| `:` | Open the command palette | `palette.open` | everyday |
| `Ctrl-p` | Open the command palette | `palette.open` | everyday |
| `q` | Quit | `app.quit` | everyday |
| `Esc` | Cancel or close | `mode.cancel` | everyday |
| `Ctrl-c` | Quit | `app.quit` | everyday |
| `Enter` | Open the selected item | `item.open` | everyday |
| `0` | Go to the first column | `nav.first-column` | everyday |
| `$` | Go to the last column | `nav.last-column` | everyday |
| `]]` | Next section of the issue or the Scrum backlog | `detail.section-next` | everyday |
| `[[` | Previous section of the issue or the Scrum backlog | `detail.section-previous` | everyday |
| `za` | Expand or collapse sub-tasks of a card, or fold a backlog section | `section.toggle` | everyday |
| `Tab` | Next tab | `tab.next` | everyday |
| `gt` | Next tab | `tab.next` | everyday |
| `Shift-Tab` | Previous tab | `tab.previous` | everyday |
| `gT` | Previous tab | `tab.previous` | everyday |
| `Ctrl-^` | Previously active tab | `tab.last-active` | everyday |
| `<space><Tab>` | Previously active tab | `tab.last-active` | everyday |
| `Alt-1` | Go to tab 1 | `tab.go-to.1` | everyday |
| `Alt-2` | Go to tab 2 | `tab.go-to.2` | everyday |
| `Alt-3` | Go to tab 3 | `tab.go-to.3` | everyday |
| `Alt-4` | Go to tab 4 | `tab.go-to.4` | everyday |
| `Alt-5` | Go to tab 5 | `tab.go-to.5` | everyday |
| `Alt-6` | Go to tab 6 | `tab.go-to.6` | everyday |
| `Alt-7` | Go to tab 7 | `tab.go-to.7` | everyday |
| `Alt-8` | Go to tab 8 | `tab.go-to.8` | everyday |
| `Alt-9` | Go to tab 9 | `tab.go-to.9` | everyday |
| `<space>t` | Open the source picker for a new tab | `tab.new` | everyday |
| `<space>c` | Close the tab | `tab.close` | everyday |
| `<space>r` | Refresh the view (also retries after an error) | `view.refresh` | everyday |
| `<space>l` | Switch between list and columns | `view.layout-toggle` | everyday |
| `<space>vf` | Cycle the quick filter of the board (off, then each filter in turn) | `view.quick-filter` | everyday |
| `<space>vs` | Group the board columns into lanes (none, assignee, epic) | `view.swimlane` | everyday |
| `<space>o` | Open the issue in the browser | `issue.open-in-browser` | everyday |
| `<space>p` | Open the parent issue | `issue.open-parent` | everyday |
| `<space>a` | Open the actions menu of the selected issue | `menu.context` | everyday |
| `<space>A` | Open the actions menu of the tab and the view | `menu.view` | everyday |
| `<space>,` | Open or close the settings screen | `settings.open` | everyday |
| `Alt-r` | Reset the highlighted setting to its default (in the settings screen) | `settings.reset` | everyday |
| `<space>ma` | Add a comment to the issue | `comment.add` | everyday |
| `<space>me` | Edit the comment under the cursor | `comment.edit` | everyday |
| `<space>md` | Delete the comment under the cursor | `comment.delete` | advanced |
| `<space>is` | Change the status of the issue | `issue.transition` | everyday |
| `Alt-l` | Move the card to the next column | `issue.transition-next` | everyday |
| `Alt-h` | Move the card to the previous column | `issue.transition-previous` | everyday |
| `Alt-k` | Rank the issue up (a count moves it that many places) | `rank.up` | everyday |
| `Alt-j` | Rank the issue down (a count moves it that many places) | `rank.down` | everyday |
| `v` | Start or end Visual mode (a range of rows) | `selection.visual` | everyday |
| `Alt-m` | Mark or unmark the selected row and move down | `selection.mark` | everyday |
| `<space>sv` | Switch the sprint shown (active, future, closed) | `sprint.switch` | everyday |
| `<space>vv` | Open a saved view in a new tab | `view.saved-open` | everyday |
| `<space>vw` | Save this view under a name (opens :view save) | `view.saved-save` | everyday |
| `<space>sm` | Move the selected issues to a sprint | `sprint.move-issues` | everyday |
| `<space>sb` | Move the selected issues to the backlog | `sprint.move-to-backlog` | everyday |
| `<space>sn` | Create a sprint (name, goal, dates) | `sprint.create` | everyday |
| `<space>se` | Edit the sprint (name, goal, dates) | `sprint.edit` | advanced |
| `<space>ss` | Start the sprint (confirms dates and issue count) | `sprint.start` | everyday |
| `<space>sc` | Complete the sprint (confirms where the open issues go) | `sprint.complete` | everyday |
| `<space>sg` | Show the full goal of the sprint | `sprint.goal` | everyday |
| `<space>kt` | Rank the issue to the top of the list or column | `rank.top` | advanced |
| `<space>kb` | Rank the issue to the bottom of the list or column | `rank.bottom` | advanced |
| `<space>kk` | Pick up the issue, then drop it at the issue under the cursor | `rank.to` | advanced |
| `<space>id` | Delete the issue (asks first; Jira cannot undo it) | `issue.delete` | advanced |
| `<space>ic` | Create an issue (type picker, then the form) | `issue.create` | everyday |
| `<space>iC` | Create a sub-task of the selected issue | `issue.create-subtask` | everyday |
| `<space>il` | Link the issue to another issue | `issue.link` | advanced |
| `<space>iu` | Remove the link under the cursor (asks first; Jira cannot undo it) | `issue.unlink` | advanced |
| `<space>ie` | Edit fields of the issue (summary, labels, priority, description) | `issue.edit` | everyday |
| `<space>ia` | Assign the issue: search people, or unassign | `issue.assign` | everyday |
| `<space>im` | Assign the issue to me | `issue.assign-me` | everyday |
| `<space>g` | Jump to an issue by key | `issue.jump` | everyday |
| `<space>n` | Show the message history | `status.history` | everyday |
| `<space>x` | Dismiss the status message | `status.dismiss` | everyday |
| `<space>*` | Switch the command level for the session | `view.cycle-command-level` | everyday |

## Help mode

| Keys | Action | Id | Level |
|---|---|---|---|
| `j` | Move down | `nav.down` | everyday |
| `Down` | Move down | `nav.down` | everyday |
| `k` | Move up | `nav.up` | everyday |
| `Up` | Move up | `nav.up` | everyday |
| `?` | Show or hide help | `help.toggle` | everyday |
| `/` | Filter the help by key, title or action id | `help.filter` | everyday |
| `J` | Scroll help down a line | `help.scroll-down` | everyday |
| `K` | Scroll help up a line | `help.scroll-up` | everyday |
| `D` | Scroll help down half a page | `help.half-page-down` | everyday |
| `Ctrl-d` | Scroll help down half a page | `help.half-page-down` | everyday |
| `U` | Scroll help up half a page | `help.half-page-up` | everyday |
| `Ctrl-u` | Scroll help up half a page | `help.half-page-up` | everyday |
| `Tab` | Next help section | `help.page-next` | everyday |
| `Shift-Tab` | Previous help section | `help.page-previous` | everyday |
| `<space>*` | Switch the command level for the session | `view.cycle-command-level` | everyday |
| `Esc` | Close help | `help.close` | everyday |
| `q` | Close help | `help.close` | everyday |
| `Ctrl-c` | Quit | `app.quit` | everyday |

## Command mode

| Keys | Action | Id | Level |
|---|---|---|---|
| `Enter` | Run the typed command | `palette.submit` | everyday |
| `Backspace` | Delete the character before the caret | `palette.backspace` | everyday |
| `Up` | Previous row in the picker | `picker.previous` | everyday |
| `Ctrl-p` | Previous row in the picker | `picker.previous` | everyday |
| `Down` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-n` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-j` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-k` | Previous row in the picker | `picker.previous` | everyday |
| `Ctrl-l` | Run the typed command | `palette.submit` | everyday |
| `Esc` | Cancel or close | `mode.cancel` | everyday |
| `Ctrl-c` | Quit | `app.quit` | everyday |
| `Alt-Left` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Alt-Right` | Move the cursor to the end of the word | `editor.word-right` | everyday |
| `Alt-b` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Alt-f` | Move the cursor to the end of the word | `editor.word-right` | everyday |
| `Ctrl-w` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Ctrl-Backspace` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Alt-Backspace` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Alt-d` | Delete the word after the cursor | `editor.delete-word-after` | everyday |
| `Ctrl-Delete` | Delete the word after the cursor | `editor.delete-word-after` | everyday |
| `Ctrl-u` | Delete from the cursor to the start of the line | `editor.delete-to-start` | everyday |
| `Left` | Move the cursor left | `editor.left` | everyday |
| `Right` | Move the cursor right | `editor.right` | everyday |
| `Home` | Move the cursor to the start of the row | `editor.row-start` | everyday |
| `End` | Move the cursor to the end of the row | `editor.row-end` | everyday |
| `Ctrl-a` | Move the cursor to the start of the row | `editor.row-start` | everyday |
| `Ctrl-e` | Move the cursor to the end of the row | `editor.row-end` | everyday |
| `Delete` | Delete the character under the cursor | `editor.delete` | everyday |
| `Ctrl-Left` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Ctrl-Right` | Move the cursor to the end of the word | `editor.word-right` | everyday |

## Confirm mode

| Keys | Action | Id | Level |
|---|---|---|---|
| `y` | Confirm | `confirm.yes` | everyday |
| `n` | Decline | `confirm.no` | everyday |
| `Esc` | Decline | `confirm.no` | everyday |
| `Ctrl-c` | Quit | `app.quit` | everyday |

## Insert mode

| Keys | Action | Id | Level |
|---|---|---|---|
| `Enter` | Accept the field and go to the next | `form.next` | everyday |
| `Backspace` | Delete the character before the caret | `form.backspace` | everyday |
| `Esc` | Cancel or close | `mode.cancel` | everyday |
| `Ctrl-c` | Cancel or close | `mode.cancel` | everyday |
| `Up` | Previous row in the picker | `picker.previous` | everyday |
| `Ctrl-p` | Previous row in the picker | `picker.previous` | everyday |
| `Down` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-n` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-t` | Open the highlighted sprint in a new tab | `picker.open-new-tab` | everyday |
| `Ctrl-j` | Next row in the picker | `picker.next` | everyday |
| `Ctrl-k` | Previous row in the picker | `picker.previous` | everyday |
| `Ctrl-l` | Accept the field and go to the next | `form.next` | everyday |
| `Ctrl-x` | Remove the highlighted profile (asks first; the stored token is deleted) | `profile.remove` | everyday |
| `Ctrl-a` | Add a profile (site, account and token in one form) | `profile.add` | everyday |
| `Ctrl-e` | Edit the highlighted profile (the stored token is kept unless replaced) | `profile.edit` | everyday |
| `Alt-t` | Test the connection of the profile form (nothing is saved) | `profile.test` | everyday |
| `Tab` | Next field of the form | `form.field-next` | everyday |
| `Shift-Tab` | Previous field of the form | `form.field-previous` | everyday |
| `Ctrl-s` | Save the form | `form.submit` | everyday |
| `Ctrl-g` | Open the new issue after it is created (on or off) | `form.open-after` | everyday |
| `Alt-Enter` | Save the form | `form.submit` | everyday |
| `Left` | Move the cursor left | `editor.left` | everyday |
| `Right` | Move the cursor right | `editor.right` | everyday |
| `Home` | Move the cursor to the start of the row | `editor.row-start` | everyday |
| `End` | Move the cursor to the end of the row | `editor.row-end` | everyday |
| `Delete` | Delete the character under the cursor | `editor.delete` | everyday |
| `PageUp` | Move the cursor up half a screen | `editor.page-up` | everyday |
| `PageDown` | Move the cursor down half a screen | `editor.page-down` | everyday |
| `Ctrl-Left` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Ctrl-Right` | Move the cursor to the end of the word | `editor.word-right` | everyday |
| `Ctrl-o` | Open the issue page in the browser from the form | `form.open-browser` | everyday |
| `Alt-Left` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Alt-Right` | Move the cursor to the end of the word | `editor.word-right` | everyday |
| `Alt-b` | Move the cursor to the start of the word | `editor.word-left` | everyday |
| `Alt-f` | Move the cursor to the end of the word | `editor.word-right` | everyday |
| `Ctrl-w` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Ctrl-Backspace` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Alt-Backspace` | Delete the word before the cursor | `editor.delete-word-before` | everyday |
| `Alt-d` | Delete the word after the cursor | `editor.delete-word-after` | everyday |
| `Ctrl-Delete` | Delete the word after the cursor | `editor.delete-word-after` | everyday |
| `Ctrl-u` | Delete from the cursor to the start of the line | `editor.delete-to-start` | everyday |

## Palette commands

| Command | What it does |
|---|---|
| `:jql [query]` | Open a JQL query in a new tab (no query: pick from recent ones) |
| `:filter [name]` | Open a saved filter in a new tab |
| `:board [name]` | Open a board as columns in a new tab |
| `:backlog [name]` | Open a board's backlog in a new tab |
| `:history clear` | Forget the recent JQL queries and issues of this profile |
| `:view [name]|save <name>|rename <old> <new>|delete <name>` | Open, save, rename or delete a saved view (source and layout) |
| `:tab new|close|next|prev|<n>` | Open the source picker, close or switch tabs |
| `:edit` | Edit the fields of the selected or shown issue (field form) |
| `:status [next|prev]` | Change the status of the selected or shown issue; next or prev moves the card a column |
| `:labels` | Edit the labels of the selected or shown issue (field form) |
| `:create [subtask]` | Create an issue, or a sub-task of the selected issue (field form) |
| `:priority` | Change the priority of the selected or shown issue (field form) |
| `:estimate` | Change the estimate (story points) of the selected or shown issue (field form) |
| `:assign` | Assign the selected or shown issue: search people, or unassign |
| `:assign-me` | Assign the selected or shown issue to me |
| `:settings` | Open the settings screen: change the theme and the options |
| `:profile [list]|add|edit [name]|switch <name>|remove <name>|check` | Open the profile manager, or add, edit, switch, remove or check a profile |
| `:set [hints.statusBar|hints.whichKey|commandlevel|theme|jump.target|startup.restore] [value]` | List the settings, show one, or change one (saved) |
| `:theme [name]` | Switch the colour theme (no name: list the families); saved |
| `:keys reload` | Read keymap.json again and apply it |
| `:help` | Show or hide help |
| `:quit` | Quit |
