#!/usr/bin/env bash
# tmux smoke of the field behaviour contract against the fake Jira (issue #1022).
#   scripts/smoke-field-contract.sh --binary <geeratya>
# Runs the real binary in a private tmux server with private XDG directories and drives the same key script through
# the form presentations the fake's data reaches, checking the rendered frame and the status line each time:
#   create (issue and sub-task): the type chooser, typed j/k as text, the Esc ladder (editing, navigate, discard question),
#       q in the navigate state, a date field (format error as you type), a choice field (popup rows, click takes one);
#   dropdown (Details of the issue view): a choice field popup, a typed filter, Esc clears it first and closes next,
#       a click on a row writes at once;
#   in place (Summary of the issue view): typed j/k as text, Esc asks before discarding, y discards;
#   link form (box): the choice field and the issue field, typed text, the Esc ladder, a click on a row;
#   transition fields (the Done transition of GCM-10 asks for Resolution and Components, that of GTM-20 for a text and
#       Labels): typed filter, Esc clears it first, a click takes a row, q like Esc in the navigate state, the discard
#       question names the issue and a yes returns to the status picker;
#   Estimate (Details dropdown of GCS-8 on the Scrum backlog): the typed number field says "Enter save" (no rows to
#       choose) and a wrong text shows the format error;
#   sprint forms (Scrum backlog of the fake's GCS board): create (typed text, the Esc ladder, the discard question names
#       the new sprint), start (the date format error, the discard question names the sprint), complete (the choice
#       popup opens at once, typed filter, Esc clears it, a click asks to complete) and the sprint of the Details
#       dropdown (a click on a row writes at once).
# Exit 0 pass, 1 check failed, 2 bad arguments, 3 missing tool. Linux only. TIMEOUT_MS (default 8000) bounds each wait.
# DUMP=1 prints the frame after every step.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
timeout_ms=${TIMEOUT_MS:-8000}
binary=""
[ "${1:-}" = "--binary" ] && binary=${2:-}
if [ -z "$binary" ] || [ ! -x "$binary" ]; then sed -n '2,24p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; fi
if [ "$(uname -s)" != Linux ]; then echo "smoke-field-contract.sh: only Linux is supported" >&2; exit 3; fi
for tool in tmux dotnet; do command -v "$tool" > /dev/null || { echo "smoke-field-contract.sh: missing tool: $tool" >&2; exit 3; }; done

now_ms() { echo $(($(date +%s%N) / 1000000)); }
work=$(mktemp -d)
socket=geeratya-fields-$$
fake_pid=""
cleanup() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  [ -z "$fake_pid" ] || kill "$fake_pid" 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

frame() { tmux -L "$socket" capture-pane -t s -p 2> /dev/null; }
status() { frame | sed '$!d'; }
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
# wait_status <text>: the status line (last line) holds <text>.
wait_status() {
  local text=$1 start
  start=$(now_ms)
  while ! status | grep -qF -- "$text"; do
    [ $(($(now_ms) - start)) -le "$timeout_ms" ] || fail "status line has no '$text' within ${timeout_ms} ms"
    sleep 0.15
  done
  echo "ok: status: $text"
  dump
}
key() { tmux -L "$socket" send-keys -t s "$1"; sleep 0.25; }
text() { tmux -L "$socket" send-keys -t s -l "$1"; sleep 0.25; }
# click <text>: press and release (SGR, left) on the first cell of the last frame line holding <text> above the status line.
click() {
  local text=$1 line row col prefix seq
  line=$(frame | sed '$d' | grep -nF -- "$text" | tail -n 1) || fail "no line with '$text' to click"
  row=${line%%:*}
  line=${line#*:}
  prefix=${line%%"$text"*}
  col=$((${#prefix} + 2))
  printf -v seq '\033[<0;%d;%dM\033[<0;%d;%dm' "$col" "$row" "$col" "$row"
  tmux -L "$socket" send-keys -t s -l "$seq"
  sleep 0.4
  echo "clicked '$text' at $col,$row"
}
dismiss_start_picker() {
  local waited=0
  while ! frame | grep -qF -- "Open a view"; do
    waited=$((waited + 1))
    [ "$waited" -le 15 ] || return 0
    sleep 0.15
  done
  tmux -L "$socket" send-keys -t s Escape
  sleep 0.4
}

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }
(cd "$root" && dotnet "$work/fakejira/fakejira.dll" > "$work/fake.url") &
fake_pid=$!
for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
fake_url=$(head -n 1 "$work/fake.url")
case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac

mkdir -p "$work/config/geeratya"
# Row two (GCM-6) is the issue the fake serves a detail for, so the steps below move onto it first.
# start: a fresh binary on the GCM board in list layout (a company-managed project, so the create and edit metadata
# carry labels, parent, components and dates), with the start picker closed.
launch() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  sleep 0.5
  rm -rf "$work/state" "$work/cache"
  mkdir -p "$work/state" "$work/cache"
  printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
    > "$work/config/geeratya/profiles.json"
  tmux -L "$socket" -f /dev/null new-session -d -s s -x 120 -y 36 \
    "env -i HOME=$work PATH=$PATH COLORTERM=truecolor TERM=tmux-256color \
      GEERATYA_TOKEN=fake XDG_CONFIG_HOME=$work/config XDG_STATE_HOME=$work/state XDG_CACHE_HOME=$work/cache '$binary'"
  wait_for "fake: fake@example.com"
  dismiss_start_picker
}

# start_scrum: a fresh binary on the Scrum backlog of the fake's GCS board (company-managed, with sprints), cursor on
# the first issue of the active sprint.
start_scrum() {
  launch
  text ":backlog GCS"
  key Enter
  wait_for "Seed Sprint 2"
}

# open_issue <key>: a fresh binary with the issue open in the detail (a search of that one key, then Enter on its row).
open_issue() {
  launch
  text ":jql key = $1"
  key Enter
  wait_for "1 issue"
  key Enter
  wait_for "Details"
}

start() {
  launch
  key C-t
  text "GCM board"
  key Enter
  wait_for "GCM-8"
  text " l"
  wait_for "<space>l columns"
}

# ---- Create form: the issue and the sub-task --------------------------------------------------------------------
# create_form <keys that open it>: the type chooser answers with its own hints, Task is the fourth row.
create_form() {
  text "$1"
  wait_for "Choose the issue type"
  wait_status "Esc close"
  key Down
  key Down
  key Down
  key Enter
  wait_for "What needs to be done?"
  wait_status "Esc stop editing"
}

# The one key script of the create form (decision 2 and 11): j/k are text, Esc steps to navigate, q acts like Esc there,
# the discard question names the new issue, a date field checks its format as you type, a choice field lists its rows.
create_script() {
  text "jk"
  wait_for "jk"
  key Escape
  wait_status "[navigate]"
  wait_status "Enter edit"
  key q
  wait_for "Discard the new issue?"
  key n
  wait_status "[navigate]"
  text "l"
  text "jjjj"
  wait_status "Due date (yyyy-MM-dd)"
  key Enter
  text "2026-1"
  wait_for "Use yyyy-MM-dd"
  key C-u
  wait_gone "Use yyyy-MM-dd"
  key Escape
  wait_status "[navigate]"
  key C-f
  wait_for "Fix versions"
  text "jjj"
  wait_status "Priority"
  key Enter
  wait_for "Highest"
  wait_for "Enter choose"
  click "Medium"
  wait_gone "Highest"
  wait_for "Medium"
  key Escape
  wait_for "Discard the new issue?"
  key y
  wait_gone "Create · GCM"
}

echo "== create issue"
start
create_form " ic"
create_script

echo "== create sub-task"
start
create_form " iC"
wait_for "Parent *"
create_script

# ---- Details dropdown --------------------------------------------------------------------------------------------
echo "== dropdown (Details of the issue view)"
start
text "j"
key Enter
wait_for "Details"
key C-l
text "j"
key Enter
wait_for "Highest"
wait_status "Enter choose"
# The same script as the create form's choice field: a typed filter narrows, Esc clears it first, the next Esc closes.
text "Hi"
wait_gone "Low"
key Escape
wait_for "Low"
key Escape
wait_gone "Highest"
# The popup again: a click on a row takes it and writes at once.
key Enter
wait_for "Highest"
click "High"
wait_gone "Highest"
wait_for "High ▾"

# ---- In place (Summary) -----------------------------------------------------------------------------------------
echo "== in place (Summary)"
start
text "j"
text ":edit"
key Enter
wait_for "editing"
text "jk"
wait_for "jk"
key Escape
wait_for "Discard the changes to"
key n
wait_for "jk"
key Escape
wait_for "Discard the changes to"
key y
wait_gone "editing"

# ---- Link form (box) ---------------------------------------------------------------------------------------------
echo "== link form (box)"
start
text "j"
text " il"
wait_for "Link type"
wait_for "blocks"
text "jk"
wait_for "No match"
key Escape
wait_for "blocks"
wait_status "Esc stop editing"
key Escape
wait_status "[navigate]"
key Enter
click "blocks"
wait_for "Link type"
key Escape
wait_for "Discard the changes to GCM-6?"
key y
wait_gone "Link GCM"

# ---- Transition fields -------------------------------------------------------------------------------------------
# status_form <key> <field label>: the status picker of the open issue, Done chosen (the fourth row), its fields form.
status_form() {
  text " is"
  wait_for "Move to"
  key Down
  key Down
  key Down
  key Enter
  wait_for "Move $1 to Done"
  wait_for "$2"
  wait_status "Esc stop editing"
}

# leave_transition <key>: from the navigate state of the transition fields: q acts like Esc (discard question, no),
# Esc asks again, yes goes back to the status picker, Esc closes it.
leave_transition() {
  wait_status "Enter edit"
  key q
  wait_for "Discard the changes to $1?"
  key n
  wait_status "[navigate]"
  key Escape
  wait_for "Discard the changes to $1?"
  key y
  wait_for "Move to"
  key Escape
  wait_gone "Move to"
}

echo "== transition fields (company-managed, GCM-10)"
open_issue GCM-10
status_form GCM-10 "Resolution"
text "jk"
wait_for "No match"
wait_status "Esc clear filter"
key Escape
wait_for "Duplicate"
wait_status "Esc stop editing"
click "Duplicate"
wait_status "[navigate]"
wait_for "Duplicate"
leave_transition GCM-10

echo "== transition fields (team-managed, GTM-20)"
open_issue GTM-20
status_form GTM-20 "Release note"
text "jk"
wait_for "jk"
key Escape
wait_status "[navigate]"
leave_transition GTM-20

# ---- Estimate ----------------------------------------------------------------------------------------------------
echo "== Estimate (Details dropdown of a Scrum issue)"
start_scrum
key Enter
wait_for "Details"
text ":estimate"
key Enter
wait_for "Story Points"
wait_status "Enter save"
text "x"
wait_for "Not a number"
wait_status "Esc close"
key Escape
wait_for "Discard the changes to GCS-8?"
key y
wait_gone "Not a number"

# ---- Sprint forms ------------------------------------------------------------------------------------------------
echo "== sprint create"
start_scrum
text " sn"
wait_for "Create sprint"
wait_status "Esc stop editing"
text "jk"
wait_for "jk"
key Escape
wait_status "[navigate]"
wait_status "Enter edit"
key q
wait_for "Discard the new sprint?"
key n
wait_status "[navigate]"
key Escape
wait_for "Discard the new sprint?"
key y
wait_gone "Create sprint"

echo "== sprint start"
start_scrum
key g
key g
text "jjjjjjj"
text " ss"
wait_for "Start sprint"
wait_status "Esc stop editing"
text "jk"
wait_for "Use yyyy-MM-dd HH:mm"
key Escape
wait_status "Enter edit"
key q
wait_for "Discard the changes to GCS Sprint 1?"
key n
wait_status "Enter edit"
key Escape
wait_for "Discard the changes to GCS Sprint 1?"
key y
wait_gone "Start sprint"

echo "== sprint complete"
start_scrum
text " sc"
wait_for "Complete sprint Seed Sprint 2"
wait_for "Next sprint"
wait_status "Enter choose"
text "jk"
wait_for "No match"
wait_status "Esc clear filter"
key Escape
wait_for "Next sprint"
click "Next sprint"
wait_for 'will be moved to sprint "GCS Sprint 1"'
key n
wait_gone "Complete sprint"

echo "== sprint of the Details dropdown"
start_scrum
key Enter
wait_for "Details"
key C-l
text "jjjjjj"
key Enter
wait_status "Sprint of GCS-8"
wait_status "Enter choose"
click "Seed Sprint 3"
wait_status 'Moved GCS-8 to sprint "Seed Sprint 3"'

echo "PASS: every scenario ran"
