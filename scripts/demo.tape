# README demo (VHS): the Scrum board, an issue, the backlog, a JQL search and a status change on the invented
# Wayfinder project of tools/FakeJira (--demo). Paced for reading: every action is followed by a pause, every result
# stays on screen long enough to read, typing is slow. Still screenshots are taken at the key moments.
# Run it through scripts/demo.sh, which sets DEMO_BIN, the fake Jira profile and the shots/ folder.
Output demo.webm
Set Shell "bash"
Set FontSize 16
Set Width 1400
Set Height 760
Set Padding 8
Set Framerate 20
Set TypingSpeed 110ms
Set Theme "Catppuccin Mocha"

Hide
Type 'clear; exec "$DEMO_BIN"'
Enter
Sleep 3s
Escape
Sleep 500ms
Show

# Scene 1: the Scrum board of the active sprint (the app opens the project's only board), in the board's own columns.
Wait+Screen /In Review/
Sleep 5s
Screenshot shots/board.png
Sleep 1500ms
Type "l"
Sleep 2s

# Scene 2: an issue. Zoomed, one section at a time: the description with its acceptance criteria, then the comments
# with a stack trace.
Enter
Wait+Screen /Comments 3/
Sleep 4s
Type "j"
Sleep 1s
Type " z"
Sleep 7s
Screenshot shots/issue-detail.png
Sleep 1500ms
Ctrl+L
Sleep 4s
Type@800ms "jj"
Sleep 1s
Type "za"
Sleep 9s
Screenshot shots/issue-comments.png
Sleep 1500ms
Type " z"
Sleep 2s
Escape
Sleep 2s

# Scene 3: the backlog with the sprints as sections.
Type ":backlog"
Sleep 1s
Enter
Wait+Screen /Sprint 16/
Sleep 6s
Screenshot shots/backlog.png
Sleep 1500ms
Type "]]"
Sleep 3s
Type "]]"
Sleep 4s

# Scene 4: a JQL search in a new tab.
Type ":jql assignee = currentUser() AND statusCategory != Done ORDER BY priority DESC"
Sleep 1500ms
Enter
Wait+Screen /WAY-15/
Sleep 5s
Screenshot shots/jql-search.png
Sleep 1500ms

# Scene 5: change the status of the first result: pick the status, see the result.
Type " is"
Wait+Screen /Change status of WAY-15/
Sleep 2s
Type "In Review"
Sleep 2s
Screenshot shots/status-picker.png
Sleep 1500ms
Enter
Wait+Screen /Moved WAY-15 to In Review/
Sleep 3s
Type " r"
Sleep 4s
Screenshot shots/status-changed.png
Sleep 3s
