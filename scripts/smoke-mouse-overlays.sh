#!/usr/bin/env bash
# tmux smoke of the mouse paths of overlays against the fake Jira (issue #360, parts of #352).
#   scripts/smoke-mouse-overlays.sh --binary <geeratya>
# Runs the real binary in a private tmux server with private XDG directories, sends SGR mouse sequences with
# `send-keys -l` (press and release at a cell found by searching the captured frame for a text) and checks the result:
# source picker row (<space>t), create form (type row, field focus), jump prompt row, field form field and suggestion popup row, comment editor mention
# popup row, a row dragged onto another backlog section (asks first), a card drag to the next column, and `:profile list` (click switches, right-click asks to remove, Esc keeps the profile, the confirm buttons by click).
# Exit 0 pass, 1 check failed, 2 bad arguments, 3 missing tool. Linux only. TIMEOUT_MS (default 8000) bounds each wait.
# DUMP=1 prints the frame after every step.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
timeout_ms=${TIMEOUT_MS:-8000}
binary=""
[ "${1:-}" = "--binary" ] && binary=${2:-}
if [ -z "$binary" ] || [ ! -x "$binary" ]; then sed -n '2,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; fi
if [ "$(uname -s)" != Linux ]; then echo "smoke-mouse-overlays.sh: only Linux is supported" >&2; exit 3; fi
for tool in tmux dotnet; do command -v "$tool" > /dev/null || { echo "smoke-mouse-overlays.sh: missing tool: $tool" >&2; exit 3; }; done

now_ms() { echo $(($(date +%s%N) / 1000000)); }
work=$(mktemp -d)
socket=geeratya-mouse-$$
fake_pid=""
cleanup() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  [ -z "$fake_pid" ] || kill "$fake_pid" 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

frame() { tmux -L "$socket" capture-pane -t s -p 2> /dev/null; }
fail() {
  echo "FAIL: $1; last frame:" >&2
  frame >&2 || true
  exit 1
}
dump() { if [ -n "${DUMP:-}" ]; then frame; echo ----; fi; }
wait_for() {
  local text=$1 start
  start=$(now_ms)
  while ! frame | grep -qF -- "$text"; do
    [ $(($(now_ms) - start)) -le "$timeout_ms" ] || fail "'$text' did not appear within ${timeout_ms} ms"
    sleep 0.15
  done
  echo "ok: $text"
  dump
}
wait_gone() {
  local text=$1 start
  start=$(now_ms)
  while frame | grep -qF -- "$text"; do
    [ $(($(now_ms) - start)) -le "$timeout_ms" ] || fail "'$text' still on screen after ${timeout_ms} ms"
    sleep 0.15
  done
  echo "ok: gone: $text"
}
send() {
  local key
  for ((index = 0; index < ${#1}; index++)); do
    key=${1:index:1}
    if [ "$key" = "|" ]; then tmux -L "$socket" send-keys -t s Enter; else tmux -L "$socket" send-keys -t s -l "$key"; fi
    sleep 0.2
  done
}
# click <text> [button]: press and release (SGR, button 0 left, 2 right) on the first cell of the last frame line that holds
# <text>. The column is a character count, so it assumes single-width characters before the text (true for the fake's data).
click() {
  local text=$1 button=${2:-0} line row col prefix seq
  line=$(frame | grep -nF -- "$text" | tail -n 1) || fail "no line with '$text' to click"
  row=${line%%:*}
  line=${line#*:}
  prefix=${line%%"$text"*}
  col=$((${#prefix} + 2))
  printf -v seq '\033[<%d;%d;%dM\033[<%d;%d;%dm' "$button" "$col" "$row" "$button" "$col" "$row"
  tmux -L "$socket" send-keys -t s -l "$seq"
  sleep 0.4
  echo "clicked '$text' (button $button) at $col,$row"
}

# drag <text> <columns> [motion]: press on the first cell of <text>, release <columns> cells to the right on the same row
# (SGR; with a third argument a motion event, button 32, goes between), the way a terminal reports a drag of a card.
drag() {
  local text=$1 shift_by=$2 motion=${3:-} line row col prefix seq
  line=$(frame | grep -nF -- "$text" | tail -n 1) || fail "no line with '$text' to drag"
  row=${line%%:*}
  line=${line#*:}
  prefix=${line%%"$text"*}
  col=$((${#prefix} + 2))
  printf -v seq '\033[<0;%d;%dM' "$col" "$row"
  [ -z "$motion" ] || printf -v seq '%s\033[<32;%d;%dM' "$seq" $((col + shift_by)) "$row"
  printf -v seq '%s\033[<0;%d;%dm' "$seq" $((col + shift_by)) "$row"
  tmux -L "$socket" send-keys -t s -l "$seq"
  sleep 0.4
  echo "dragged '$text' by $shift_by columns from $col,$row"
}

# drag_to <from text> <to text>: press on the first cell of the line holding <from text>, release on the line holding
# <to text> (SGR, with a motion event between), the way a terminal reports a row dragged to another section.
drag_to() {
  local from=$1 to=$2 line from_row to_row col prefix seq
  line=$(frame | grep -nF -- "$from" | tail -n 1) || fail "no line with '$from' to drag"
  from_row=${line%%:*}
  line=${line#*:}
  prefix=${line%%"$from"*}
  col=$((${#prefix} + 2))
  to_row=$(frame | grep -nF -- "$to" | tail -n 1 | cut -d: -f1)
  [ -n "$to_row" ] || fail "no line with '$to' to drop on"
  printf -v seq '\033[<0;%d;%dM\033[<32;%d;%dM\033[<0;%d;%dm' "$col" "$from_row" "$col" "$to_row" "$col" "$to_row"
  tmux -L "$socket" send-keys -t s -l "$seq"
  sleep 0.4
  echo "dragged '$from' to the line of '$to'"
}

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }
dotnet "$work/fakejira/fakejira.dll" --issues 40 > "$work/fake.url" &
fake_pid=$!
for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
fake_url=$(head -n 1 "$work/fake.url")
case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac

mkdir -p "$work/config/geeratya" "$work/state" "$work/cache"
# start <profiles>: a fresh binary in a fresh tmux server. GEERATYA_TOKEN serves a single profile only, so the
# two-profile session (profile list) has no usable token for the second one: switching ends in "No API token", enough to see it ran.
start() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  sleep 0.5
  printf '{"activeName":"fake","profiles":[%s]}\n' "$1" > "$work/config/geeratya/profiles.json"
  tmux -L "$socket" -f /dev/null new-session -d -s s -x 120 -y 36 \
    "env -i HOME=$work PATH=$PATH COLORTERM=truecolor TERM=tmux-256color \
      GEERATYA_TOKEN=fake XDG_CONFIG_HOME=$work/config XDG_STATE_HOME=$work/state XDG_CACHE_HOME=$work/cache '$binary'"
}
one="{\"name\":\"fake\",\"siteUrl\":\"$fake_url\",\"email\":\"fake@example.com\"}"
two="$one,{\"name\":\"second\",\"siteUrl\":\"$fake_url\",\"email\":\"second@example.com\"}"

start "$one"
wait_for "fake: fake@example.com"

# Scrum backlog (#394): a row dragged onto another section asks first, nothing is sent before y; Esc keeps everything.
send ":backlog GEN board|"
wait_for "Generated Sprint 3"
drag_to "GEN-14 " "Generated Sprint 3"
wait_for "Jira has no undo"
wait_for "Move GEN-14"
tmux -L "$socket" send-keys -t s Escape
wait_gone "Jira has no undo"
start "$one"
wait_for "fake: fake@example.com"

# Source picker: <space>t, click a row.
send " t"
wait_for "Open a view"
click "Board   GCM board"
wait_for "In Progress"
wait_gone "Open a view"

# Card drag: a card dragged onto the next column opens the transition form (a drop never writes by itself), with the
# motion events a terminal sends (they carry the Pressed flag too) and without them. That the click after a release is
# swallowed is covered by the MouseDragTrackerTests only; this script proves the press, motion and release pairing.
drag "Story GCM-11" 30 motion
wait_for "Ctrl-s save"
tmux -L "$socket" send-keys -t s Escape
wait_gone "Ctrl-s save"
drag "Story GCM-11" 30
wait_for "Ctrl-s save"
tmux -L "$socket" send-keys -t s Escape
wait_gone "Ctrl-s save"

# Create form: the type list row is taken by a click, then a click on another field moves the focus to it.
send " ic"
wait_for "Choose the issue type"
click "│   Task  "
wait_for "> Summary"
click "  Description"
wait_for "> Description"
tmux -L "$socket" send-keys -t s Escape
wait_gone "Create Task in GCM"

# Jump prompt: type a prefix, click a result row.
send " gGEN-1"
wait_for "Jump to an issue"
click "│ > GEN-1  "
wait_gone "Jump to an issue"
wait_for "Reporter"

# Field form: :labels opens it on GEN-1's issue; click the field, then a suggestion popup row.
tmux -L "$socket" send-keys -t s Escape; sleep 0.3
send ":jql project = GEN|:labels|geer"
wait_for "geeratya-seed"
click "geeratya-seed-GCM-backlog-2"
wait_gone "Tab/Enter accept"
wait_for "geeratya-seed-GCM-backlog-2"
tmux -L "$socket" send-keys -t s Escape
wait_for "Discard the changes?"
send "y"
wait_gone "Edit GEN-1"

# Comment editor: the mention popup row is taken by a click.
send "jjjj| maThanks @test"
wait_for "Tab/Enter accept"
click "Test User 3"
wait_gone "Tab/Enter accept"
wait_for "Test User 3"

# Profile list: right-click asks to remove (Esc keeps it), click switches.
start "$two"
wait_for "fake: fake@example.com"
send ":profile list|"
wait_for "Ctrl-x remove"
click "second  " 2
wait_for "Remove profile second"
tmux -L "$socket" send-keys -t s Escape
wait_gone "Remove profile second"
send ":profile list|"
wait_for "Ctrl-x remove"

# Confirm dialog buttons (#379): a click on the Cancel row closes it and keeps the profile; a click on the answer row
# ("Remove profile", below the question that starts with the same words) confirms and removes it (the inactive profile,
# after the click-switch to the other one).
click "second  " 2
wait_for "Remove profile second"
click "Cancel"
wait_gone "Remove profile second"
send ":profile list|"
wait_for "Ctrl-x remove"
frame | grep -qF -- "second  " || fail "the profile was removed by a click on Cancel"
click "second  "
wait_for "second: second@example.com"
send ":profile list|"
wait_for "Ctrl-x remove"
click "fake  " 2
wait_for "Remove profile fake"
click "Remove profile  "
wait_gone "Remove profile fake"
send ":profile list|"
wait_for "Ctrl-x remove"
frame | grep -qF -- "fake  " && fail "the profile is still listed after a click on the yes row"
echo "ok: confirm dialog click on Cancel keeps, click on yes removes"
echo "PASS"
