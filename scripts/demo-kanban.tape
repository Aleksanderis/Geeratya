# README demo, part 2 (VHS): a Kanban board, an issue and a status change, on the recorded dataset of tools/FakeJira
# (only it answers transitions). Run it through scripts/demo.sh, which sets DEMO_BIN and the fake Jira profile.
Output part2.webm
Set Shell "bash"
Set FontSize 16
Set Width 1400
Set Height 760
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

# The Kanban board with the board's own columns.
Type ":board GCM"
Enter
Sleep 2s
Type "l"
Sleep 700ms
Sleep 700ms
Type "jj"
Sleep 1s

# An issue: open it, back to the board.
Enter
Sleep 3s
Escape
Sleep 800ms

# A status change: the status picker, filter, confirm.
Type " is"
Sleep 1s
Type "Done"
Sleep 800ms
Enter
Sleep 1500ms
Type "y"
Sleep 3s
