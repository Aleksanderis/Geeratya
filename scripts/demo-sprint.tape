# README demo, part 1 (VHS): the Scrum sprint board and an issue, on the generated dataset of tools/FakeJira.
# Run it through scripts/demo.sh, which sets DEMO_BIN and the fake Jira profile.
Output part1.webm
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

# The sprint board of a Scrum project: the active sprint in the board's own columns.
Type ":board GEN"
Enter
Sleep 1500ms
Type " sv"
Sleep 800ms
Enter
Wait+Screen /Unmapped/
Sleep 2s
Type "hjj"
Sleep 1s
Type "l"
Sleep 1s

# An issue: open it, look around, back to the board.
Enter
Wait+Screen /Description/
Sleep 3s
Escape
Sleep 800ms
