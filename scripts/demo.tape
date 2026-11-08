# README demo (VHS): the Scrum board, keyboard navigation across it, an issue, the backlog, a JQL search and a status
# change on the invented Wayfinder project of tools/FakeJira (--demo). Short pauses: about 0.7 s on a result, 200 ms
# per navigation key, about 30 s in all. Recorded at 1100x640 pixels with font 15 (about 125 columns) so the whole
# board fits and the text stays legible when the page shows the GIF about 1000 px wide. A caption line (an
# ffmpeg overlay added by scripts/demo.sh, from the CAPTIONS list there) names the keys. Still screenshots are taken at
# the key moments.
# Run it through scripts/demo.sh, which sets DEMO_BIN, the fake Jira profile and the shots/ folder.
Output demo.webm
Set Shell "bash"
Set FontSize 15
Set Width 1100
Set Height 640
Set Padding 6
Set Framerate 20
Set TypingSpeed 50ms
Set Theme "Catppuccin Mocha"

Hide
Type 'clear; exec "$DEMO_BIN"'
Enter
Sleep 3s
Escape
Sleep 500ms
Show

# Scene 1: the Scrum board of the active sprint, then h/j/k/l across its columns and cards at a steady cadence.
Wait+Screen /In Review/
Sleep 1s
Screenshot shots/board.png
Type@200ms "jjjj"
Sleep 400ms
Type@200ms "llll"
Sleep 400ms
Type@200ms "jj"
Sleep 400ms
Type@200ms "hhh"
Sleep 400ms
Type@200ms "kkk"
Sleep 600ms
Type@200ms "l"
Sleep 500ms

# Scene 2: open the issue. Zoomed, one section at a time: the description, then the comments with a stack trace.
Enter
Wait+Screen /Comments 3/
Sleep 600ms
Type "j"
Sleep 300ms
Type " z"
Sleep 1200ms
Screenshot shots/issue-detail.png
Ctrl+L
Sleep 300ms
Ctrl+L
Sleep 500ms
Type@300ms "jj"
Sleep 300ms
Type "za"
Sleep 1200ms
Screenshot shots/issue-comments.png
Type " z"
Sleep 300ms
Escape
Sleep 500ms

# Scene 3: the backlog with the sprints as sections.
Type ":backlog"
Sleep 300ms
Enter
Wait+Screen /Sprint 16/
Sleep 1s
Screenshot shots/backlog.png
Type@250ms "]]]]"
Sleep 800ms

# Scene 4: a JQL search in a new tab.
Type@30ms ":jql assignee = currentUser() AND statusCategory != Done ORDER BY priority DESC"
Sleep 300ms
Enter
Wait+Screen /WAY-15/
Sleep 1s
Screenshot shots/jql-search.png

# Scene 5: change the status of the first result: pick the status, see the result.
Type " is"
Wait+Screen /Change status of WAY-15/
Sleep 400ms
Type "In Review"
Sleep 500ms
Screenshot shots/status-picker.png
Enter
Wait+Screen /Moved WAY-15 to In Review/
Sleep 600ms
Type " r"
Sleep 1s
Screenshot shots/status-changed.png
Sleep 700ms
