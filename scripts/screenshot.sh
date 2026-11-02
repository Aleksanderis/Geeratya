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

# Scenario table: name;columns;rows;theme;keys;wait-for. Keys are sent to tmux one by one (`|` sends Enter); wait-for is text that
# must be on screen after the keys before the frame is captured (before them the fake profile line is awaited),
# because data loads asynchronously and the settle check alone only compares two captures 0.15 s apart.
# Add a row per new screen.
profile_ready="fake: fake@example.com"
profile_line="$profile_ready on"
all_scenarios=(
  "default;120;36;dark;;$profile_line"
  "help;120;36;dark;?;Help"
  "narrow;64;30;dark;;$profile_ready"
  "light;120;36;light;;$profile_line"
  "list;120;36;dark;:jql project = GCM|;issues"
  "list-narrow;64;24;dark;:jql project = GCM|jjjj;issues"
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
  sockets=()
  trap stop_render EXIT
  mkdir -p "$out" "$work/config" "$work/state" "$work/cache"

  dotnet build "$root/tools/AnsiToSvg" -c Release -o "$work/ansitosvg" -v q --nologo > "$work/build.log" 2>&1 \
    || { tail -20 "$work/build.log" >&2; exit 1; }
  dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo >> "$work/build.log" 2>&1 \
    || { tail -20 "$work/build.log" >&2; exit 1; }

  # The deterministic dataset is the generated one, so no recorded name or key reaches an image.
  dotnet "$work/fakejira/fakejira.dll" --issues 40 > "$work/fake.url" &
  fake_pid=$!
  for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
  fake_url=$(head -n 1 "$work/fake.url")
  case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac

  seed_fake_profile "$work/config" "$fake_url"

  local rendered=0
  for spec in "${all_scenarios[@]}"; do
    name=${spec%%;*}
    if [ -n "$wanted" ] && [[ ",$wanted," != *",$name,"* ]]; then continue; fi
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
