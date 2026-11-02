#!/usr/bin/env bash
# tmux smoke of open-in-browser against the fake Jira, with no usable opener (issue #134).
#   scripts/smoke-browser.sh --binary <geeratya>
# Runs the real binary in a private tmux server with private XDG directories and a PATH without xdg-open or BROWSER,
# opens a list row and its detail, presses `<space>o` and checks the status line for the failure message.
# The fake serves http on loopback, which the https-only gate (BrowserUrlCheck) refuses before any opener runs, so the
# message checked is the refusal "Cannot open the link: ..."; no browser can start. Exit 0 pass, 1 check failed,
# 2 bad arguments, 3 missing tool. Linux only.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
timeout_ms=${TIMEOUT_MS:-8000}
binary=""
[ "${1:-}" = "--binary" ] && binary=${2:-}
if [ -z "$binary" ] || [ ! -x "$binary" ]; then sed -n '2,8p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; fi
if [ "$(uname -s)" != Linux ]; then echo "smoke-browser.sh: only Linux is supported" >&2; exit 3; fi
for tool in tmux dotnet; do command -v "$tool" > /dev/null || { echo "smoke-browser.sh: missing tool: $tool" >&2; exit 3; }; done

now_ms() { echo $(($(date +%s%N) / 1000000)); }
work=$(mktemp -d)
socket=geeratya-smoke-$$
fake_pid=""
cleanup() {
  tmux -L "$socket" kill-server 2> /dev/null || true
  [ -z "$fake_pid" ] || kill "$fake_pid" 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

wait_for() {
  local text=$1 start
  start=$(now_ms)
  while ! tmux -L "$socket" capture-pane -t s -p 2> /dev/null | grep -qF -- "$text"; do
    if [ $(($(now_ms) - start)) -gt "$timeout_ms" ]; then
      echo "FAIL: '$text' did not appear within ${timeout_ms} ms; last frame:" >&2
      tmux -L "$socket" capture-pane -t s -p >&2 || true
      exit 1
    fi
    sleep 0.15
  done
  echo "ok: $text"
}

send() {
  local key
  for ((index = 0; index < ${#1}; index++)); do
    key=${1:index:1}
    if [ "$key" = "|" ]; then tmux -L "$socket" send-keys -t s Enter; else tmux -L "$socket" send-keys -t s -l "$key"; fi
    sleep 0.2
  done
}

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }
dotnet "$work/fakejira/fakejira.dll" --issues 40 > "$work/fake.url" &
fake_pid=$!
for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
fake_url=$(head -n 1 "$work/fake.url")
case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac

mkdir -p "$work/config/geeratya" "$work/state" "$work/cache"
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
  > "$work/config/geeratya/profiles.json"
dotnet_dir=$(dirname "$(command -v dotnet)")

# PATH holds tmux-free basics only: no xdg-open, no BROWSER.
tmux -L "$socket" -f /dev/null new-session -d -s s -x 120 -y 36 \
  "env -i HOME=$work PATH=$dotnet_dir:/usr/bin:/bin COLORTERM=truecolor TERM=tmux-256color \
    GEERATYA_TOKEN=fake XDG_CONFIG_HOME=$work/config XDG_STATE_HOME=$work/state XDG_CACHE_HOME=$work/cache '$binary'"

wait_for "fake: fake@example.com"
send ":jql project = GEN|"
wait_for "issues"
send " o"
wait_for "Cannot open the link"
send ":jql project = GEN|jj|"
wait_for "Generated comment"
send " o"
wait_for "Cannot open the link"
echo "PASS"
