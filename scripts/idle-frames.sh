#!/usr/bin/env bash
# Idle output of the real binary: usage idle-frames.sh <binary> <fake url> [start|list] [seconds].
# Runs it in a pty of 120x36 under `script`, waits for the screen to settle (3 s, 6 s with a loaded list), then counts
# the bytes and the frames (cursor-home repaints, ESC[1;1H) it writes in the window (default 10 s). Linux only.
# Target (#744): under 1 frame per second, and for a screen that has not changed no bytes at all.
bin=$1; url=$2; mode=${3:-start}; secs=${4:-10}
w=$(mktemp -d); export XDG_CONFIG_HOME=$w/c XDG_STATE_HOME=$w/s XDG_CACHE_HOME=$w/k GEERATYA_TOKEN=fake
mkdir -p "$w/c/geeratya" "$w/s" "$w/k"
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"f@example.com"}]}\n' "$url" > "$w/c/geeratya/profiles.json"
settle=3
keys=${KEYS:-}
if [ "$mode" = list ]; then settle=6; keys="${keys}"$':jql project = GCM\r'; fi
export TERM=xterm-256color
# the binary sees a 120x36 terminal; the keys are typed once after the start, then nothing
# MOTION=1 moves the pointer (SGR any-event reports) over the body, about 10 a second, during the window
( sleep 2; printf '%s' "$keys"; sleep $((settle - 2)); \
  for i in $(seq 1 $((secs * 10))); do
    if [ -n "$MOTION" ]; then printf '\033[<35;%d;10M' $((10 + i % 60)); fi
    sleep 0.1
  done; sleep 2 ) | script -qfc "stty rows 36 cols 120; exec $bin" "$w/out" > /dev/null &
sp=$!
sleep "$settle"
a=$(stat -c %s "$w/out" 2>/dev/null || echo 0)
ca=$(grep -a -o $'\e\\[1;1H' "$w/out" | wc -l)
sleep "$secs"
b=$(stat -c %s "$w/out")
cb=$(grep -a -o $'\e\\[1;1H' "$w/out" | wc -l)
pkill -f "^$bin" 2>/dev/null
wait "$sp" 2>/dev/null
echo "idle ${secs} s: bytes=$((b - a)) frames=$((cb - ca)) cursor_hide=$(grep -a -o $'\e\\[?25l' "$w/out" | wc -l) (total since start)"
grep -a -o $'\e\\[?10[0-9][0-9]h' "$w/out" | sort -u | tr '\n' ' '; echo
[ -n "$KEEP" ] && cp "$w/out" "$KEEP"; rm -rf "$w"
