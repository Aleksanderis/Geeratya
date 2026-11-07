#!/usr/bin/env bash
# Idle CPU of the real binary: usage idle-cpu.sh <binary> <fake url> [start|list]. Linux only (/proc).
# Starts it in a private tmux and private XDG dirs, waits for the start screen (or, in list mode, opens the first board with Down, Enter and waits for issue keys), then prints the
# CPU ticks (1/100 s) and voluntary context switches summed over all threads in a window (WINDOW seconds, default 20).
bin=$1; url=$2; mode=${3:-start}; window=${WINDOW:-20}
w=$(mktemp -d); export XDG_CONFIG_HOME=$w/c XDG_STATE_HOME=$w/s XDG_CACHE_HOME=$w/k GEERATYA_TOKEN=fake
mkdir -p $w/c/geeratya $w/s $w/k
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"f@example.com"}]}\n' "$url" > $w/c/geeratya/profiles.json
tm="tmux -L idle$$"
$tm -f /dev/null new-session -d -s m -x 120 -y 40 "$bin"
sleep 2
# list: open the first board of the start picker (Down, Enter) as idle-frames.sh does, then check issue keys show
if [ $mode = list ]; then
  $tm send-keys -t m Down Enter
  for i in $(seq 1 20); do sleep 0.5; $tm capture-pane -p -t m | grep -q 'GCM-' && break; done
  sleep 2
  $tm capture-pane -p -t m | grep -q 'GCM-' || { echo "no issue keys on screen, the list did not load"; $tm kill-server; rm -rf $w; exit 1; }
fi
pid=$(pgrep -n -f "^$bin")
snap() { for t in /proc/$pid/task/*; do echo "$(basename $t) $(awk '{print $14+$15}' $t/stat) $(awk '/^voluntary/{print $2}' $t/status) $(cat $t/comm | tr ' ' _)"; done; }
snap > $w/a; sleep $window; snap > $w/b
join $w/a $w/b | awk -v w=$window '{c+=$5-$2; v+=$6-$3} END{printf "cpu_ticks=%d context_switches=%d (%d s)\n",c,v,w}'
$tm kill-server; rm -rf $w
