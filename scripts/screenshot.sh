#!/usr/bin/env bash
# Screenshots of the real binary for pull requests (skill: screenshots).
#   scripts/screenshot.sh render --binary <geeratya> [--out <dir>] [--scenarios a,b]
#   scripts/screenshot.sh publish --pr <n> [--out <dir>]
#   scripts/screenshot.sh clean
# render: starts tools/FakeJira (never a real site), runs the binary in a private tmux server with private XDG
#   directories, captures the pane with colours, converts it with tools/AnsiToSvg and rsvg-convert to <dir>/<name>.png.
# publish: pushes every PNG in <dir> to pr-<n>/<short sha>/ on main of the separate screenshots repository
#   (default <owner>/<repo>-screenshots, override with GEERATYA_SCREENSHOTS_REPO=owner/name) and prints markdown.
# clean: removes pr-<n> directories whose PR is merged or closed.
# Exit codes: 2 bad arguments, 3 missing tool (named on stderr), 4 screenshots repository cannot be cloned.
# Linux only for now (GNU date, tmux, rsvg-convert); macOS and Windows are unsupported and fail at the date check.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
default_out=${TMPDIR:-/tmp}/geeratya-screenshots
timeout_ms=${TIMEOUT_MS:-5000}

# Scenario table: name;columns;rows;theme;keys;wait-for. Keys are sent to tmux one by one (`|` sends Enter, `~` Ctrl-s);
# wait-for is text that must be on screen after the keys before the frame is captured (before them the fake profile line is awaited),
# because data loads asynchronously and the settle check alone only compares two captures 0.15 s apart.
# Scenarios named detail-relations-* and comment-recorded-* run against a second fake on the recorded fixtures (no --issues), because only
# those carry sub-tasks, links, attachments and epic children; `:jql key = KEY` opens one recorded issue there.
# sprint-no-active runs against a third fake started with --no-active-sprint (the generated Scrum board without an active sprint).
# In keys, `}` sends Alt-j and `{` sends Alt-k (rank down and up) and `^` sends Alt-m (mark).
# Add a row per new screen.
profile_ready="fake: fake@example.com"
profile_line="$profile_ready on"
all_scenarios=(
  "default;120;36;dark;;Connected as"
  "help;120;36;dark;?;Help"
  "help-list;120;36;dark;:jql project = GCM|?;Help"
  "whichkey;120;36;dark;:jql project = GCM| ;Space"
  "narrow;64;30;dark;;Connected"
  "light;120;36;light;;Connected as"
  "list;120;36;dark;:jql project = GCM|;issues"
  "list-narrow;64;24;dark;:jql project = GCM|jjjj;issues"
  "list-sprint;160;36;dark;:board GEN|:layout list|;Sprint"
  "sprint-switcher;120;36;dark;:board GEN| sv;Generated Sprint"
  "sprint-columns;120;36;dark;:board GEN| sv|;Generated Sprint"
  # sprint-subtasks: the sub-task summary row on the cards (generated sub-task stubs), then one card expanded with z a
  "sprint-subtasks;120;36;dark;:board GEN| sv|;Generated Sprint"
  "sprint-subtasks-expanded;120;36;dark;:board GEN| sv|jza;Generated sub-task"
  # sprint-points reuses the columns view and waits for a card estimate
  "sprint-points;120;36;dark;:board GEN| sv|;5p"
  "sprint-no-active;120;36;dark;:board GEN|;No open sprint on this board."
  "sprint-goal;120;36;dark;:board GEN| sv| sg;Sprint goal"
  "list-selection;120;36;dark;:jql project = GCM|^^jvjj;2 selected"
  "jql-history;120;36;dark;:jql project = GCM|:jql|;Recent"
  "palette;120;36;dark;:jql project = GCM|:;Commands"
  "palette-filtered;80;24;dark;:jql project = GCM|:rank;Rank"
  "palette-run;120;36;dark;:jql project = GCM|:hide help|;Help"
  "profile-list;120;36;dark;:profile list|;Ctrl-x remove"
  "detail;120;36;dark;:jql project = GEN|jjjj|;Generated comment 3"
  "detail-narrow;64;24;dark;:jql project = GEN|jjjj|;Reporter  Generated"
  "detail-section;120;36;dark;:jql project = GEN|jjjj|]]jj;Generated comment 3"
  "comment-editor;120;36;dark;:jql project = GEN|jjjj| maLooks good to me|Second line, with wide 日本語;Second line"
  "comment-editor-mention;120;36;dark;:jql project = GEN|jjjj| maThanks @test;Tab/Enter accept"
  "comment-editor-issue;120;36;dark;:jql project = GEN|jjjj| maSee #GEN;Tab/Enter accept"
  "comment-delete;120;36;dark;:jql project = GEN|jjjj|]]]] md;Jira cannot undo"
  "comment-recorded-saved;120;36;dark;:jql key = GTM-11|| maLooks good to me~;Added a comment to"
  "comment-recorded-edit;120;36;dark;:jql key = GTM-11||]]]]]]]]]]]] me;formatting"
  "detail-relations-gtm;120;36;dark;:jql key = GTM-11||]]]];Remote"
  "detail-relations-gcm;120;36;dark;:jql key = GCM-10||]]]];Remote"
  "detail-relations-gtm-epic;120;36;dark;:jql key = GTM-9||;Epic children"
  "detail-relations-gcm-epic;120;36;dark;:jql key = GCM-8||;Epic children"
  "detail-relations-delete-confirm;120;36;dark;:jql key = GTM-9| id;Jira cannot undo"
  "detail-relations-create-type;120;36;dark;:jql key = GTM-11| ic;Choose the issue type"
  "detail-relations-create-subtask;120;36;dark;:jql key = GCM-10| iC|;Create Sub-task of GCM-10"
  "link-form;120;36;dark;:jql key = GTM-11|| il;Link GTM-11"
  "link-unlink-confirm;120;36;dark;:jql key = GTM-11||]]]]jj iu;Jira cannot undo"
  "picker;120;36;dark; t;Open a view"
  "which-key;120;36;dark;:jql project = GCM| ;+issue"
  "jump;120;36;dark;:jql project = GEN| ggen-1;Jump to an issue"
  "profile-add;120;36;dark;:profile add|work|https://example.atlassian.net|me@example.com|hidden-token;Add profile"
  "profile-edit;120;36;dark;:profile edit|;Edit profile"
  "form;120;36;dark;:jql project = GEN|:labels|urgent;Edit GEN-"
  "form-narrow;64;24;dark;:jql project = GEN|:labels|urgent;Edit GEN-"
  "form-suggest;120;36;dark;:jql project = GEN|:labels|geer;geeratya-seed"
  "form-choice;120;36;dark;:jql project = GEN|:priority|h;Edit GEN-"
  "assign;120;36;dark;:jql project = GEN| iatest;Test User 1"
  "assign-quick;120;36;dark;:jql project = GEN| ia;Me ("
  "status-picker;120;36;dark;:board GCM|j is;Change status of"
  "status-confirm;120;36;dark;:board GCM|j isDon|;Move GCM-"
  "status-moved;120;36;dark;:board GCM|j isIn P|;Moved GCM-"
  "board;120;36;dark;:board GCM|;In Progress 4"
  "board-lanes;120;36;dark;:board GCM| vs;Lane 1/1"
  "board-filter;120;36;dark;:board GCM| vf;Filter: Recently Updated"
  "board-team;120;36;dark;:board GTM|l;In Progress"
  "board-narrow;40;10;dark;:board GCM|l;To Do"
  "backlog;120;36;dark;:backlog GCM|;GCM"
  "filter;120;36;dark;:filter Filter for GCM|;GCM"
  "backlog-sprints;120;36;dark;:backlog GEN board|;Generated Sprint 3"
  "sprint-move;120;36;dark;:backlog GEN board| sm;Move to sprint"
  "backlog-sprints-collapsed;120;36;dark;:backlog GEN board|za]];Generated Sprint 3"
  "sprint-form-create;120;36;dark;:backlog GCS board| snSprint 4|Ship it|2026-12-01 09:00;Create sprint"
  "sprint-form-edit;120;36;dark;:backlog GCS board| se;Edit sprint"
  "sprint-created;120;36;dark;:backlog GCS board| snSprint 4~;Created sprint"
  "sprint-complete-form;120;36;dark;:backlog GCS board| sc;Open issues go to"
  "sprint-view-complete-form;120;36;dark;:board GEN| sv| sc;Open issues go to"
  "sprint-complete-confirm;120;36;dark;:backlog GCS board| sc~;Jira cannot undo"
  "rank-backlog;120;36;dark;:backlog GCM|};Ranked GCM-1"
  "rank-across-sections;120;36;dark;:backlog GEN board|]]^j^{;Jira has no undo"
  "rank-board;120;36;dark;:board GCM|};Ranked"
  "rank-board-team;120;36;dark;:board GTM|};Ranked"
  "theme-latte;120;36;light;:board GCM|:theme catppuccin-latte|;Theme catppuccin-latte."
  "theme-high-contrast;120;36;dark;:board GCM|:theme high-contrast|;Theme high-contrast."
  "theme-gruvbox;120;36;dark;:board GCM|:theme gruvbox-dark-medium|;Theme gruvbox-dark-medium."
  "theme-list;120;36;dark;:theme|;Themes:"
  "tabs;120;36;dark;:jql project = GCM|:jql assignee = currentUser() order by key|;[2 JQL"
)

usage() {
  sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

if [ "$(uname -s)" != Linux ]; then
  echo "screenshot.sh: only Linux is supported" >&2
  exit 3
fi

need() {
  for tool in "$@"; do
    if ! command -v "$tool" > /dev/null; then
      echo "screenshot.sh: required tool not found: $tool" >&2
      exit 3
    fi
  done
}

now_ms() { echo $(($(date +%s%N) / 1000000)); }

# Prints the colour capture of the pane once two consecutive captures are equal and non-empty.
settled_capture() {
  local socket=$1 previous="" current start
  start=$(now_ms)
  while true; do
    current=$(tmux -L "$socket" capture-pane -t s -p -e 2> /dev/null || true)
    if [ -n "${current//[[:space:]]/}" ] && [ "$current" = "$previous" ]; then
      printf '%s\n' "$current"
      return 0
    fi
    previous=$current
    if [ $(($(now_ms) - start)) -gt "$timeout_ms" ]; then
      echo "screenshot.sh: the frame did not settle within ${timeout_ms} ms" >&2
      return 1
    fi
    sleep 0.15
  done
}

# Waits until the pane shows the text (empty text: no wait); fails after the timeout.
wait_for_text() {
  local socket=$1 text=$2 start
  [ -n "$text" ] || return 0
  start=$(now_ms)
  while ! tmux -L "$socket" capture-pane -t s -p 2> /dev/null | grep -qF -- "$text"; do
    if [ $(($(now_ms) - start)) -gt "$timeout_ms" ]; then
      echo "screenshot.sh: '$text' did not appear within ${timeout_ms} ms" >&2
      return 1
    fi
    sleep 0.15
  done
}

# Writes the profiles file of the one active fake profile (site = the fake's loopback address) into the private
# configuration directory, so the binary connects to the fake and no real profile is read.
seed_fake_profile() {
  local config=$1 url=$2
  mkdir -p "$config/geeratya"
  printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$url" \
    > "$config/geeratya/profiles.json"
}

capture_scenario() {
  local spec=$1 binary=$2 out=$3 work=$4 ansitosvg=$5
  local name columns rows theme keys waitfor socket key
  IFS=";" read -r name columns rows theme keys waitfor <<< "$spec"
  socket=geeratya-shot-$$-$name
  tmux -L "$socket" -f /dev/null new-session -d -s s -x "$columns" -y "$rows" \
    "env -u GEERATYA_LIVE_HOSTS COLORTERM=truecolor TERM=tmux-256color \
      GEERATYA_TOKEN=fake XDG_CONFIG_HOME=$work/config XDG_STATE_HOME=$work/state \
      XDG_CACHE_HOME=$work/cache '$binary'"
  sockets+=("$socket")
  wait_for_text "$socket" "$profile_ready"
  settled_capture "$socket" > /dev/null
  for ((index = 0; index < ${#keys}; index++)); do
    key=${keys:index:1}
    if [ "$key" = "|" ]; then
      tmux -L "$socket" send-keys -t s Enter
    elif [ "$key" = "~" ]; then
      tmux -L "$socket" send-keys -t s C-s
    elif [ "$key" = "}" ]; then
      tmux -L "$socket" send-keys -t s M-j
    elif [ "$key" = "{" ]; then
      tmux -L "$socket" send-keys -t s M-k
    elif [ "$key" = "^" ]; then
      tmux -L "$socket" send-keys -t s M-m
    else
      tmux -L "$socket" send-keys -t s -l "$key"
    fi
    sleep 0.2
  done
  wait_for_text "$socket" "$waitfor"
  settled_capture "$socket" > "$work/$name.ansi"
  tmux -L "$socket" kill-server 2> /dev/null || true
  rm -f "$out/$name.png"
  dotnet "$ansitosvg/ansitosvg.dll" --cols "$columns" --rows "$rows" --theme "$theme" \
    < "$work/$name.ansi" > "$work/$name.svg"
  rsvg-convert "$work/$name.svg" -o "$out/$name.png"
  echo "wrote $out/$name.png"
}

render() {
  local binary="" out=$default_out wanted=""
  while [ $# -gt 0 ]; do
    case $1 in
      --binary) binary=${2:-}; shift 2 ;;
      --out) out=${2:-}; shift 2 ;;
      --scenarios) wanted=${2:-}; shift 2 ;;
      *) usage ;;
    esac
  done
  [ -n "$binary" ] || usage
  need tmux rsvg-convert dotnet
  if [ ! -x "$binary" ]; then echo "binary not found or not executable: $binary" >&2; exit 2; fi

  work=$(mktemp -d)
  fake_pid=""
  recorded_pid=""
  noactive_pid=""
  sockets=()
  trap stop_render EXIT
  mkdir -p "$out" "$work/config" "$work/state" "$work/cache"

  dotnet build "$root/tools/AnsiToSvg" -c Release -o "$work/ansitosvg" -v q --nologo > "$work/build.log" 2>&1 \
    || { tail -20 "$work/build.log" >&2; exit 1; }
  dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo >> "$work/build.log" 2>&1 \
    || { tail -20 "$work/build.log" >&2; exit 1; }

  # The deterministic dataset is the generated one; only the detail-relations scenarios use the scrubbed recorded fixtures.
  dotnet "$work/fakejira/fakejira.dll" --issues 40 > "$work/fake.url" &
  fake_pid=$!
  for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
  fake_url=$(head -n 1 "$work/fake.url")
  case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac

  # The recorded dataset is scrubbed fixtures; only the relations scenarios use it.
  # Started only when a selected scenario needs it.
  if [ -z "$wanted" ] || [[ $wanted == *detail-relations-* || $wanted == *comment-recorded-* ]]; then
    dotnet "$work/fakejira/fakejira.dll" > "$work/fake-recorded.url" &
    recorded_pid=$!
    for _ in $(seq 1 50); do [ -s "$work/fake-recorded.url" ] && break; sleep 0.1; done
    recorded_url=$(head -n 1 "$work/fake-recorded.url")
    case $recorded_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac
  fi

  # The generated Scrum board without an active sprint; started only for the scenario that needs it.
  if [ -z "$wanted" ] || [[ $wanted == *sprint-no-active* ]]; then
    dotnet "$work/fakejira/fakejira.dll" --issues 40 --no-active-sprint > "$work/fake-noactive.url" &
    noactive_pid=$!
    for _ in $(seq 1 50); do [ -s "$work/fake-noactive.url" ] && break; sleep 0.1; done
    noactive_url=$(head -n 1 "$work/fake-noactive.url")
    case $noactive_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac
  fi

  local rendered=0
  for spec in "${all_scenarios[@]}"; do
    name=${spec%%;*}
    if [ -n "$wanted" ] && [[ ",$wanted," != *",$name,"* ]]; then continue; fi
    if [[ $name == detail-relations-* || $name == comment-recorded-* ]]; then
      seed_fake_profile "$work/config" "$recorded_url"
    elif [[ $name == sprint-no-active ]]; then
      seed_fake_profile "$work/config" "$noactive_url"
    else
      seed_fake_profile "$work/config" "$fake_url"
    fi
    capture_scenario "$spec" "$binary" "$out" "$work" "$work/ansitosvg"
    rendered=$((rendered + 1))
  done
  if [ "$rendered" -eq 0 ]; then echo "no scenario matches '$wanted'" >&2; exit 2; fi
}

# Stops everything render started, also after a failure.
stop_render() {
  local socket
  for socket in "${sockets[@]}"; do tmux -L "$socket" kill-server 2> /dev/null || true; done
  if [ -n "$fake_pid" ]; then kill "$fake_pid" 2> /dev/null || true; fi
  if [ -n "$recorded_pid" ]; then kill "$recorded_pid" 2> /dev/null || true; fi
  if [ -n "${noactive_pid:-}" ]; then kill "$noactive_pid" 2> /dev/null || true; fi
  rm -rf "$work"
}

screenshots_repo() {
  cd "$root"
  if [ -n "${GEERATYA_SCREENSHOTS_REPO:-}" ]; then echo "$GEERATYA_SCREENSHOTS_REPO"; return; fi
  echo "$(gh repo view --json nameWithOwner -q .nameWithOwner)-screenshots"
}

clone_repo() {
  local repo=$1 target=$2
  if ! gh repo clone "$repo" "$target" -- --quiet 2> /dev/null; then
    echo "screenshot.sh: cannot clone $repo (create it first; that is the owner's call)" >&2
    exit 4
  fi
}

commit_and_push() {
  local checkout=$1 message=$2 role=${GEERATYA_AGENT_ROLE:-implementer}
  git -C "$checkout" add -A
  if git -C "$checkout" diff --cached --quiet; then return 0; fi
  git -C "$checkout" -c "user.name=Claude ($role)" commit --quiet -m "$message" -m "Agent: $role"
  if ! git -C "$checkout" push --quiet origin HEAD:main 2> /dev/null; then
    git -C "$checkout" pull --quiet --rebase origin main
    git -C "$checkout" push --quiet origin HEAD:main
  fi
}

publish() {
  local pr="" out=$default_out
  while [ $# -gt 0 ]; do
    case $1 in
      --pr) pr=${2:-}; shift 2 ;;
      --out) out=${2:-}; shift 2 ;;
      *) usage ;;
    esac
  done
  [[ $pr =~ ^[0-9]+$ ]] || usage
  need gh git
  compgen -G "$out/*.png" > /dev/null || { echo "no PNG files in $out (run render first)" >&2; exit 2; }

  local repo sha directory commit file
  repo=$(screenshots_repo)
  sha=$(git -C "$root" rev-parse --short HEAD)
  checkout=$(mktemp -d) # Global on purpose: the EXIT trap reads it after the function returns
  trap 'rm -rf "$checkout"' EXIT
  clone_repo "$repo" "$checkout"
  directory=pr-$pr/$sha
  mkdir -p "$checkout/$directory"
  cp "$out"/*.png "$checkout/$directory/"
  commit_and_push "$checkout" "screenshots for PR #$pr @ $sha"
  commit=$(git -C "$checkout" rev-parse HEAD)
  for file in "$out"/*.png; do
    name=$(basename "$file" .png)
    echo "![$name](https://github.com/$repo/raw/$commit/$directory/$name.png)"
  done
}

clean() {
  need gh git
  local repo directory number state removed=0
  cd "$root"
  repo=$(screenshots_repo)
  checkout=$(mktemp -d) # Global on purpose: the EXIT trap reads it after the function returns
  trap 'rm -rf "$checkout"' EXIT
  clone_repo "$repo" "$checkout"
  for directory in "$checkout"/pr-*; do
    [ -d "$directory" ] || continue
    number=${directory##*/pr-}
    state=$(gh pr view "$number" --json state -q .state 2> /dev/null || echo UNKNOWN)
    if [ "$state" = MERGED ] || [ "$state" = CLOSED ]; then
      git -C "$checkout" rm -rq -- "pr-$number"
      removed=$((removed + 1))
    fi
  done
  commit_and_push "$checkout" "clean screenshots of finished PRs"
  echo "removed $removed finished PR directories"
}

[ $# -ge 1 ] || usage
command=$1
shift
case $command in
  render) render "$@" ;;
  publish) publish "$@" ;;
  clean) clean ;;
  *) usage ;;
esac
