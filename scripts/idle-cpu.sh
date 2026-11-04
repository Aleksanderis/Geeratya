#!/usr/bin/env bash
# Idle CPU of the real binary: usage idle-cpu.sh <binary> <fake url> [start|list]. Linux only (/proc).
# Starts it in a private tmux and private XDG dirs, waits for the start screen (or a loaded list), then prints the
# CPU ticks (1/100 s) and voluntary context switches summed over all threads in a 20 s window.
bin=$1; url=$2; mode=${3:-start}
w=$(mktemp -d); export XDG_CONFIG_HOME=$w/c XDG_STATE_HOME=$w/s XDG_CACHE_HOME=$w/k GEERATYA_TOKEN=fake
mkdir -p $w/c/geeratya $w/s $w/k
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"f@example.com"}]}\n' "$url" > $w/c/geeratya/profiles.json
tm="tmux -L idle$$"
$tm -f /dev/null new-session -d -s m -x 120 -y 40 "$bin"
sleep 2
# list: open the first board of the start picker (Down, Enter) as idle-frames.sh does, then check issue keys show
if [ $mode = list ]; then
  $tm send-keys -t m Down Enter; sleep 4
  $tm capture-pane -p -t m | grep -q 'GCM-' || echo "WARNING: no issue keys on screen, the list did not load"
fi
pid=$(pgrep -n -f "^$bin")
snap() { for t in /proc/$pid/task/*; do echo "$(basename $t) $(awk '{print $14+$15}' $t/stat) $(awk '/^voluntary/{print $2}' $t/status) $(cat $t/comm | tr ' ' _)"; done; }
snap > $w/a; sleep 20; snap > $w/b
join $w/a $w/b | awk '{c+=$5-$2; v+=$6-$3} END{printf "cpu_ticks=%d context_switches=%d (20 s)\n",c,v}'
$tm kill-server; rm -rf $w
