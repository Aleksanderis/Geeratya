#!/usr/bin/env bash
# Checks that tools/FakeJira does not outlive the script that started it (issue #1043).
# Starts a helper shell that starts the fake with --parent-pid of its own pid, kills the helper with SIGKILL (no trap can
# run) and waits for the fake to exit. Linux and macOS (kill -0); the same option works on Windows (FakeJira tests).
set -u
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
command -v dotnet > /dev/null || { echo "test-fake-lifetime.sh: dotnet is required" >&2; exit 3; }

work=$(mktemp -d)
helper_pid=""
fake_pid=""
cleanup() {
  [ -z "$fake_pid" ] || kill "$fake_pid" 2> /dev/null || true
  [ -z "$helper_pid" ] || kill -9 "$helper_pid" 2> /dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT
trap 'exit 130' INT; trap 'exit 143' TERM; trap 'exit 129' HUP # The EXIT trap runs on exit; bash skips it on a bare signal

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }

bash -c 'dotnet "$1/fakejira.dll" --parent-pid $$ > "$2/fake.url" & echo $! > "$2/fake.pid"; wait' _ "$work/fakejira" "$work" &
helper_pid=$!
for _ in $(seq 1 100); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
[ -s "$work/fake.url" ] || { echo "FAIL: the fake did not start" >&2; exit 1; }
fake_pid=$(cat "$work/fake.pid")
kill -0 "$fake_pid" 2> /dev/null || { echo "FAIL: the fake is not running before the kill" >&2; exit 1; }

kill -9 "$helper_pid"
wait "$helper_pid" 2> /dev/null || true
for _ in $(seq 1 100); do
  if ! kill -0 "$fake_pid" 2> /dev/null; then echo "ok: the fake stopped after its parent was killed"; fake_pid=""; exit 0; fi
  sleep 0.1
done
echo "FAIL: the fake is still running 10 s after its parent was killed" >&2
exit 1
