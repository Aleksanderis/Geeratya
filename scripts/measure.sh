#!/usr/bin/env bash
# Measure cold start to first frame and key-to-frame of the real binary in tmux.
# Usage: [SCENARIO=list|views|detail|scrum] [KEYS=n] [TIMEOUT_MS=n] [ROWS=n] [JQL=...] scripts/measure.sh <binary> <fake url> [runs]
#   <binary>    the Native AOT geeratya binary (never dotnet run: JIT numbers differ)
#   <fake url>  base URL printed by tools/FakeJira; written as the site of one active fake profile into the
#               private configuration directory (token: GEERATYA_TOKEN=fake), so the binary talks to the fake
#   [runs]      sessions to start (default 20); key-to-frame takes KEYS toggles per session (default 5)
# SCENARIO=views measures, per session: opening an issue detail from the list (Enter to the header without
#   "Loading…"), the second start of a view from the disk cache (a new session on the same private cache: time
#   from `:jql` to the first rows, compare it with the first open; start the fake with a latency well above
#   the floor, for example 300 ms, so a cache hit shows), and 10 tabs (`:jql` x10): RSS after them and the
#   time of KEYS `Tab` presses.
# SCENARIO=detail measures only the first issue-detail open of a session: per session (fresh private cache) it
#   types `:jql $JQL`, waits for the list, presses Enter at once (no settling sleep; the list header shows
#   after the connect, so the site-fields warm-up has normally answered) and times Enter to the detail header without "Loading…".
# SCENARIO=list also measures a loaded view (no fixed sleeps, every wait polls the frame): per session it types
#   `:jql $JQL` (default `project = GCM`), times the wait for the list header (open list: includes the fake's
#   latency), then times KEYS presses of `j` (key to frame in a loaded list), then loads pages with `G` until
#   the header shows "$ROWS issues" (default 10000, needs a fake started with --issues $ROWS) and prints the RSS of
#   the process, and the time of KEYS `j` presses at the end of the full list.
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
export GEERATYA_TOKEN=fake
mkdir -p "$XDG_CONFIG_HOME/geeratya" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"
case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "the fake url must be a loopback address" >&2; exit 2 ;; esac
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
  > "$XDG_CONFIG_HOME/geeratya/profiles.json"

# Load average (1 minute) so a noisy run can be recognised; "n/a" where /proc/loadavg does not exist.
load_now() { if [ -r /proc/loadavg ]; then cut -d' ' -f1 /proc/loadavg; else echo n/a; fi; }
load_before=$(load_now)

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

case ${SCENARIO:-} in '' | list | views | detail | scrum) ;; *) echo "unknown SCENARIO: ${SCENARIO} (use list, views, detail or scrum)" >&2; exit 2 ;; esac
base_runs=$runs
if [ -n "${SCENARIO:-}" ]; then base_runs=0; fi
# The floor: a shell that prints one character, so the numbers can be read net of tmux and polling cost.
floor=()
for ((run = 1; run <= base_runs; run++)); do
  tmux -L "$socket" kill-server 2> /dev/null || true
  socket=$socket_base-f$run
  floor+=("$(start_session "printf x; sleep 30")")
done

cold=()
keyed=()
for ((run = 1; run <= base_runs; run++)); do
  tmux -L "$socket" kill-server 2> /dev/null || true
  socket=$socket_base-$run
  cold+=("$(start_session "$binary")")
  for ((press = 1; press <= keys; press++)); do
    before=$(frame)
    tmux -L "$socket" send-keys -t m '?'
    keyed+=("$(wait_frame "$before")")
  done
done

# Polls until the frame matches the extended regex $1; prints the elapsed ms.
wait_match() {
  local start current
  start=$(now_ms)
  while true; do
    current=$(frame)
    if printf '%s\n' "$current" | grep -Eq -- "$1"; then
      echo $(($(now_ms) - start))
      return 0
    fi
    if [ $((($(now_ms) - start))) -gt "$timeout_ms" ]; then
      echo "timed out waiting for /$1/; frame:" >&2; printf "%s\n" "$current" | head -6 >&2
      return 1
    fi
  done
}

# RSS in MB of the process under the pane (the pane process or its child), Linux only.
pane_rss_mb() {
  local pid
  pid=$(tmux -L "$socket" display -p -t m '#{pane_pid}')
  if [ ! -r "/proc/$pid/status" ]; then echo n/a; return; fi
  if ! grep -q geeratya "/proc/$pid/comm"; then pid=$(pgrep -P "$pid" | head -1); fi
  awk '/VmRSS/ { printf "%d", $2 / 1024 }' "/proc/$pid/status"
}

if [ "${SCENARIO:-}" = detail ]; then
  jql=${JQL:-project = GCM}
  detail=()
  for ((run = 1; run <= runs; run++)); do
    export XDG_CACHE_HOME=$work/cache-$run XDG_STATE_HOME=$work/state-$run
    mkdir -p "$XDG_CACHE_HOME" "$XDG_STATE_HOME"
    tmux -L "$socket" kill-server 2> /dev/null || true
    socket=$socket_base-d$run
    start_session "$binary" > /dev/null
    tmux -L "$socket" send-keys -t m ":jql $jql" Enter
    wait_match 'Generated issue 1  ' > /dev/null
    tmux -L "$socket" send-keys -t m Enter
    detail+=("$(wait_match '› GEN-1 *$')")
  done
  read -r b1 b2 <<< "$(printf '%s\n' "${detail[@]}" | summarise)"
  echo "scenario detail, JQL: $jql"
  echo "load average:  $load_before before, $(load_now) after ($(nproc 2> /dev/null || echo '?') cores)"
  echo "first Enter to issue detail loaded (ms):           median $b1, p95 $b2"
  exit 0
fi

if [ "${SCENARIO:-}" = views ]; then
  jql=${JQL:-project = GCM}
  first=()
  detail=()
  second=()
  start2=()
  tabkey=()
  rss=()
  for ((run = 1; run <= runs; run++)); do
    export XDG_CACHE_HOME=$work/cache-$run XDG_STATE_HOME=$work/state-$run
    mkdir -p "$XDG_CACHE_HOME" "$XDG_STATE_HOME"
    tmux -L "$socket" kill-server 2> /dev/null || true
    socket=$socket_base-v$run
    start_session "$binary" > /dev/null
    tmux -L "$socket" send-keys -t m ":jql $jql" Enter
    first+=("$(wait_match 'Generated issue 1  ')")
    sleep 0.5
    tmux -L "$socket" send-keys -t m Enter
    detail+=("$(wait_match '› GEN-1 *$')")
    sleep 2 # the cache write is debounced; leave time for it before the first session is killed
    tmux -L "$socket" kill-server 2> /dev/null || true
    socket=$socket_base-w$run
    start2+=("$(start_session "$binary")")
    tmux -L "$socket" send-keys -t m ":jql $jql" Enter
    second+=("$(wait_match 'Generated issue 1  ')")
    for ((tab = 2; tab <= 10; tab++)); do
      tmux -L "$socket" send-keys -t m ":jql $jql" Enter
      wait_match "\\[$tab " > /dev/null
    done
    sleep 1
    rss+=("$(pane_rss_mb)")
    for ((press = 1; press <= keys; press++)); do
      before=$(frame)
      tmux -L "$socket" send-keys -t m Tab
      tabkey+=("$(wait_frame "$before")")
    done
  done
  read -r a1 a2 <<< "$(printf '%s\n' "${first[@]}" | summarise)"
  read -r b1 b2 <<< "$(printf '%s\n' "${detail[@]}" | summarise)"
  read -r c1 c2 <<< "$(printf '%s\n' "${second[@]}" | summarise)"
  read -r d1 d2 <<< "$(printf '%s\n' "${start2[@]}" | summarise)"
  read -r e1 e2 <<< "$(printf '%s\n' "${tabkey[@]}" | summarise)"
  read -r f1 f2 <<< "$(printf '%s\n' "${rss[@]}" | summarise)"
  echo "scenario views, JQL: $jql"
  echo "load average:  $load_before before, $(load_now) after ($(nproc 2> /dev/null || echo '?') cores)"
  echo "first open of the list, no cache (ms):             median $a1, p95 $a2"
  echo "Enter to issue detail loaded (ms):                 median $b1, p95 $b2"
  echo "second start, cold start to first frame (ms):      median $d1, p95 $d2"
  echo "second start, :jql to list header, cache present:  median $c1, p95 $c2"
  echo "Tab key to frame with 10 tabs open (ms):           median $e1, p95 $e2"
  echo "RSS with 10 tabs of one page each (MB):            median $f1, p95 $f2"
  exit 0
fi

if [ "${SCENARIO:-}" = scrum ]; then
  # Scrum paths against a fake started with --issues (the generated board GEN, project GEN). Per run, two sessions
  # share one private cache: the first has none (open the backlog, switch to a sprint, both over the network), the
  # second starts with the cache the first wrote (the same two steps from the cache). A third step presses `j` and
  # `za` (fold a section) in the loaded backlog. SPRINT is the text typed in the sprint switcher.
  sprint=${SPRINT:-28}
  s_open=()
  s_open_cached=()
  s_switch=()
  s_switch_cached=()
  s_picker=()
  s_key=()
  s_fold=()
  s_cold=()
  s_cold_cached=()
  s_rss=()
  backlog_ready='Generated Sprint [0-9]+ · (active|future)'
  sprint_ready='(Task|Bug|Story) GEN-[0-9]+'
  for ((run = 1; run <= runs; run++)); do
    export XDG_CACHE_HOME=$work/cache-s$run XDG_STATE_HOME=$work/state-s$run
    mkdir -p "$XDG_CACHE_HOME" "$XDG_STATE_HOME"
    for pass in fresh cached; do
      tmux -L "$socket" kill-server 2> /dev/null || true
      socket=$socket_base-s$run$pass
      cold_ms=$(start_session "$binary")
      tmux -L "$socket" send-keys -t m ":backlog GEN" Enter
      open_ms=$(wait_match "$backlog_ready")
      tmux -L "$socket" send-keys -t m Space s v
      picker_ms=$(wait_match 'Switch sprint')
      wait_match "Generated Sprint $sprint \\(" > /dev/null
      tmux -L "$socket" send-keys -t m "$sprint"
      sleep 0.2
      tmux -L "$socket" send-keys -t m Enter
      switch_ms=$(wait_match "$sprint_ready")
      if [ "$pass" = fresh ]; then
        s_cold+=("$cold_ms")
        s_open+=("$open_ms")
        s_switch+=("$switch_ms")
        s_picker+=("$picker_ms")
        sleep 2 # let the debounced cache write finish
      else
        s_cold_cached+=("$cold_ms")
        s_open_cached+=("$open_ms")
        s_switch_cached+=("$switch_ms")
        # Back to the backlog tab, then keys in the loaded backlog.
        tmux -L "$socket" send-keys -t m M-1
        wait_match 'GEN board · [0-9]+ issues' > /dev/null
        for ((press = 1; press <= keys; press++)); do
          before=$(frame)
          tmux -L "$socket" send-keys -t m j
          s_key+=("$(wait_frame "$before")")
        done
        before=$(frame)
        tmux -L "$socket" send-keys -t m z a
        s_fold+=("$(wait_frame "$before")")
        s_rss+=("$(pane_rss_mb)")
      fi
    done
  done
  report() { local label=$1; shift; local m p; read -r m p <<< "$(printf '%s\n' "$@" | summarise)"; printf '%-58s median %s, p95 %s\n' "$label" "$m" "$p"; }
  echo "scenario scrum: sprint typed in the switcher: $sprint"
  echo "load average:  $load_before before, $(load_now) after ($(nproc 2> /dev/null || echo '?') cores)"
  report "cold start to first frame, fresh (ms):" "${s_cold[@]}"
  report "cold start to first frame, cache present (ms):" "${s_cold_cached[@]}"
  report ":backlog to first rows, no cache (ms):" "${s_open[@]}"
  report ":backlog to first rows, cache present (ms):" "${s_open_cached[@]}"
  report "<space>sv to the switcher drawn (ms):" "${s_picker[@]}"
  report "switcher Enter to sprint rows, no cache (ms):" "${s_switch[@]}"
  report "switcher Enter to sprint rows, cache present (ms):" "${s_switch_cached[@]}"
  report "j key to frame in the loaded backlog (ms):" "${s_key[@]}"
  report "za (fold a section) to frame (ms):" "${s_fold[@]}"
  report "RSS with the backlog and a sprint tab loaded (MB):" "${s_rss[@]}"
  exit 0
fi

if [ "${SCENARIO:-}" = list ]; then
  jql=${JQL:-project = GCM}
  rows=${ROWS:-10000}
  open=()
  lkey=()
  endkey=()
  rss=()
  for ((run = 1; run <= runs; run++)); do
    tmux -L "$socket" kill-server 2> /dev/null || true
    socket=$socket_base-l$run
    export XDG_CACHE_HOME=$work/cache-l$run XDG_STATE_HOME=$work/state-l$run
    mkdir -p "$XDG_CACHE_HOME" "$XDG_STATE_HOME"
    start_session "$binary" > /dev/null
    tmux -L "$socket" send-keys -t m ":jql $jql" Enter
    open+=("$(wait_match 'Generated issue 1  ')")
    for ((press = 1; press <= keys; press++)); do
      before=$(frame)
      tmux -L "$socket" send-keys -t m j
      lkey+=("$(wait_frame "$before")")
    done
    # Page in with G (the next page loads when the selection reaches the end); the header changes with each page.
    for ((page = 1; page <= rows / 100 + 5; page++)); do
      header=$(frame | sed -n 3p)
      case $header in *"· $rows issues"*) break ;; esac
      tmux -L "$socket" send-keys -t m G
      page_start=$(now_ms)
      while [ "$(frame | sed -n 3p)" = "$header" ] && [ $(($(now_ms) - page_start)) -lt "$timeout_ms" ]; do
        tmux -L "$socket" send-keys -t m G
      done
    done
    wait_match "· $rows issues" > /dev/null
    last=$(frame)
    for ((settle = 0; settle < 20; settle++)); do # queued G presses may still redraw; wait for a stable frame
      sleep 0.1
      now=$(frame)
      if [ "$now" = "$last" ]; then break; fi
      last=$now
    done
    rss+=("$(pane_rss_mb)")
    for ((press = 1; press <= keys; press++)); do
      before=$(frame)
      tmux -L "$socket" send-keys -t m k
      endkey+=("$(wait_frame "$before")")
    done
  done
  read -r o_median o_p95 <<< "$(printf '%s\n' "${open[@]}" | summarise)"
  read -r l_median l_p95 <<< "$(printf '%s\n' "${lkey[@]}" | summarise)"
  read -r e_median e_p95 <<< "$(printf '%s\n' "${endkey[@]}" | summarise)"
  read -r r_median r_p95 <<< "$(printf '%s\n' "${rss[@]}" | summarise)"
  echo "scenario list: $rows rows, JQL: $jql"
  echo "load average:  $load_before before, $(load_now) after ($(nproc 2> /dev/null || echo '?') cores)"
  echo "open list, command to first page (ms):             median $o_median, p95 $o_p95"
  echo "j key to frame in a 100-row list (ms):             median $l_median, p95 $l_p95"
  echo "k key to frame at the end of the full list (ms):   median $e_median, p95 $e_p95"
  echo "RSS with the full list loaded (MB):                median $r_median, p95 $r_p95"
  exit 0
fi

read -r floor_median floor_p95 <<< "$(printf '%s\n' "${floor[@]}" | summarise)"
read -r cold_median cold_p95 <<< "$(printf '%s\n' "${cold[@]}" | summarise)"
read -r key_median key_p95 <<< "$(printf '%s\n' "${keyed[@]}" | summarise)"
echo "binary:        $binary"
echo "runs:          $runs sessions, $keys key presses each"
echo "load average:  $load_before before, $(load_now) after ($(nproc 2> /dev/null || echo '?') cores); results on a busy machine are pessimistic"
echo "tmux floor, first frame of a trivial command (ms): median $floor_median, p95 $floor_p95"
echo "cold start to first frame (ms):                    median $cold_median, p95 $cold_p95"
echo "key to frame (ms):                                 median $key_median, p95 $key_p95"
