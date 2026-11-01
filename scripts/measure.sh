#!/usr/bin/env bash
# Measure cold start to first frame and key-to-frame of the real binary in tmux.
# Usage: scripts/measure.sh <binary> <fake url> [runs]
#   <binary>    the Native AOT geeratya binary (never dotnet run: JIT numbers differ)
#   <fake url>  base URL printed by tools/FakeJira (exported as GEERATYA_FAKE_URL; the binary does not read it
#               until the Jira-backed views exist, see docs/performance.md)
#   [runs]      sessions to start (default 20); key-to-frame takes KEYS toggles per session (default 5)
# Needs tmux and GNU date (Linux first; macOS BSD date lacks %N, Windows has no equivalent yet).
# Prints median and p95 in milliseconds. A private tmux server and private XDG directories are used, so the
# owner's profiles, tokens and cache are never read. The numbers include the cost of each tmux capture call
# (a few ms); compare runs with each other, not with a stopwatch in the app.
set -euo pipefail

if [ $# -lt 2 ]; then
  echo "usage: $0 <binary> <fake url> [runs]" >&2
  exit 2
fi
binary=$1
fake_url=$2
runs=${3:-20}
keys=${KEYS:-5}
timeout_ms=${TIMEOUT_MS:-5000}

if [ ! -x "$binary" ]; then echo "binary not found or not executable: $binary" >&2; exit 2; fi
if ! command -v tmux > /dev/null; then echo "tmux is required" >&2; exit 2; fi

work=$(mktemp -d)
socket_base="geeratya-measure-$$"
socket=$socket_base
cleanup() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

export XDG_CONFIG_HOME=$work/config XDG_STATE_HOME=$work/state XDG_CACHE_HOME=$work/cache
export GEERATYA_FAKE_URL=$fake_url
mkdir -p "$XDG_CONFIG_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

now_ms() { echo $(($(date +%s%N) / 1000000)); }
frame() { tmux -L "$socket" capture-pane -t m -p 2> /dev/null || true; }

# Waits until the frame differs from $1 (empty means: until it has any text); prints the elapsed ms.
wait_frame() {
  local before=$1 start current
  start=$(now_ms)
  while true; do
    current=$(frame)
    if [ -n "${current//[[:space:]]/}" ] && [ "$current" != "$before" ]; then
      echo $(($(now_ms) - start))
      return 0
    fi
    if [ $(($(now_ms) - start)) -gt "$timeout_ms" ]; then
      echo "timed out waiting for a frame" >&2
      return 1
    fi
  done
}

# Prints "median p95" of the numbers on stdin.
summarise() {
  sort -n | awk '{ v[NR] = $1 } END {
    if (NR == 0) { print "n/a n/a"; exit }
    m = int((NR + 1) / 2); p = int(NR * 0.95 + 0.999); if (p < 1) p = 1
    print v[m], v[p] }'
}

# Starts the command in the tmux server named by $socket and prints the ms until its first frame.
start_session() {
  tmux -L "$socket" -f /dev/null new-session -d -s m -x 120 -y 40 "$1"
  wait_frame ""
}

# The floor: a shell that prints one character, so the numbers can be read net of tmux and polling cost.
floor=()
for ((run = 1; run <= runs; run++)); do
  tmux -L "$socket" kill-server 2> /dev/null || true
  socket=$socket_base-f$run
  floor+=("$(start_session "printf x; sleep 30")")
done

cold=()
keyed=()
for ((run = 1; run <= runs; run++)); do
  tmux -L "$socket" kill-server 2> /dev/null || true
  socket=$socket_base-$run
  cold+=("$(start_session "$binary")")
  for ((press = 1; press <= keys; press++)); do
    before=$(frame)
    tmux -L "$socket" send-keys -t m '?'
    keyed+=("$(wait_frame "$before")")
  done
done

read -r floor_median floor_p95 <<< "$(printf '%s\n' "${floor[@]}" | summarise)"
read -r cold_median cold_p95 <<< "$(printf '%s\n' "${cold[@]}" | summarise)"
read -r key_median key_p95 <<< "$(printf '%s\n' "${keyed[@]}" | summarise)"
echo "binary:        $binary"
echo "runs:          $runs sessions, $keys key presses each"
echo "tmux floor, first frame of a trivial command (ms): median $floor_median, p95 $floor_p95"
echo "cold start to first frame (ms):                    median $cold_median, p95 $cold_p95"
echo "key to frame (ms):                                 median $key_median, p95 $key_p95"
