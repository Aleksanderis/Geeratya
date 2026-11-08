# README demo (VHS): the Scrum board, an issue, the backlog, a JQL search and a status change on the invented
# Wayfinder project of tools/FakeJira (--demo). Paced between quick and slow: about 1 to 1.5 s on a small result,
# 3 to 4 s on the zoomed issue sections, about 50 s in all. Recorded at a large terminal so the whole board and
# detail pane fit. Still screenshots are taken at the key moments.
# Run it through scripts/demo.sh, which sets DEMO_BIN, the fake Jira profile and the shots/ folder.
Output demo.webm
Set Shell "bash"
Set FontSize 14
Set Width 1920
Set Height 1040
Set Padding 8
Set Framerate 20
Set TypingSpeed 70ms
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
Sleep 2500ms
Screenshot shots/board.png
Sleep 500ms
Type "l"
Sleep 1s

# Scene 2: an issue. Zoomed, one section at a time: the description with its acceptance criteria, then the comments
# with a stack trace.
Enter
Wait+Screen /Comments 3/
Sleep 2s
Type "j"
Sleep 500ms
Type " z"
Sleep 3500ms
Screenshot shots/issue-detail.png
Sleep 500ms
Ctrl+L
Sleep 2s
Type@500ms "jj"
Sleep 500ms
Type "za"
Sleep 4s
Screenshot shots/issue-comments.png
Sleep 500ms
Type " z"
Sleep 1s
Escape
Sleep 1s

# Scene 3: the backlog with the sprints as sections.
Type ":backlog"
Sleep 500ms
Enter
Wait+Screen /Sprint 16/
Sleep 3s
Screenshot shots/backlog.png
Sleep 500ms
Type "]]"
Sleep 1500ms
Type "]]"
Sleep 2s

# Scene 4: a JQL search in a new tab.
Type ":jql assignee = currentUser() AND statusCategory != Done ORDER BY priority DESC"
Sleep 800ms
Enter
Wait+Screen /WAY-15/
Sleep 3s
Screenshot shots/jql-search.png
Sleep 500ms

# Scene 5: change the status of the first result: pick the status, see the result.
Type " is"
Wait+Screen /Change status of WAY-15/
Sleep 1s
Type "In Review"
Sleep 1s
Screenshot shots/status-picker.png
Sleep 500ms
Enter
Wait+Screen /Moved WAY-15 to In Review/
Sleep 1500ms
Type " r"
Sleep 2s
Screenshot shots/status-changed.png
Sleep 2s
