#!/usr/bin/env bash
# Records the README demo: the real binary against tools/FakeJira (never a real site), scripted by
# scripts/demo-sprint.tape and scripts/demo-kanban.tape (VHS + ttyd, fixed terminal size and theme). Linux only.
# No media is committed to this repository.
# Usage: scripts/demo.sh --binary <geeratya> --out <dir>
#   --binary  the AOT binary (dotnet publish src/Geeratya.Tui -c Release -o <outdir>)
#   --out     output directory (outside the repository): demo.gif and demo.webm
# Two parts, joined: the generated dataset (sprint board, issue) and the recorded one (Kanban board, status change;
# the recorded fake also answers transitions since #818, but the tapes stay as recorded). The files are hosted on the project's GitHub Pages site: copy them to the media/
# folder of the geeratya.github.io repository (docs/internal/testing.md, "README demo").
# Needs vhs, ttyd, ffmpeg, dotnet. Nothing here runs in CI. Exit codes: 2 bad arguments, 3 missing tool.
set -euo pipefail

case "$(uname -s)" in
  Linux*) ;;
  *) echo "demo.sh is supported on Linux only" >&2; exit 3 ;;
esac
script_dir=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$script_dir/.." && pwd)
binary="" out_dir=""
while [ $# -gt 0 ]; do
  case $1 in
    --binary) binary=${2:-}; shift 2 ;;
    --out) out_dir=${2:-}; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done
if [ -z "$binary" ] || [ -z "$out_dir" ]; then
  sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2
  exit 2
fi
[ -x "$binary" ] || { echo "binary '$binary' not found or not executable" >&2; exit 2; }
for tool in vhs ttyd ffmpeg dotnet; do
  command -v "$tool" > /dev/null 2>&1 || { echo "missing tool '$tool' (vhs needs ttyd and ffmpeg)" >&2; exit 3; }
done

work=$(mktemp -d)
fake_pid=""
trap 'if [ -n "$fake_pid" ]; then kill "$fake_pid" 2> /dev/null || true; fi; rm -rf "$work"' EXIT

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }
mkdir -p "$out_dir"

# record <tape> <fake arguments...>: starts a fake, seeds one active fake profile in private XDG directories (nothing of
# the owner's configuration or sites is read), records the tape in $work and stops the fake.
record() {
  local tape=$1 fake_url
  shift
  rm -rf "$work/config" "$work/state" "$work/cache"
  mkdir -p "$work/config/geeratya" "$work/state" "$work/cache"
  : > "$work/fake.url"
  dotnet "$work/fakejira/fakejira.dll" "$@" > "$work/fake.url" &
  fake_pid=$!
  for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
  fake_url=$(head -n 1 "$work/fake.url")
  case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac
  printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
    > "$work/config/geeratya/profiles.json"
  (
    cd "$work"
    DEMO_BIN="$binary" XDG_CONFIG_HOME="$work/config" XDG_STATE_HOME="$work/state" XDG_CACHE_HOME="$work/cache" \
      GEERATYA_TOKEN=fake vhs "$script_dir/$tape" >&2
  )
  kill "$fake_pid" 2> /dev/null || true
  wait "$fake_pid" 2> /dev/null || true
  fake_pid=""
}

unset GEERATYA_LIVE_HOSTS
record demo-sprint.tape --issues 40
record demo-kanban.tape

# Join the parts; the GIF (for the README) comes from the joined video with a palette of its own.
ffmpeg -v error -y -i "$work/part1.webm" -i "$work/part2.webm" \
  -filter_complex "[0:v][1:v]concat=n=2:v=1:a=0[v]" -map "[v]" -c:v libvpx-vp9 -crf 32 -b:v 0 "$out_dir/demo.webm"
ffmpeg -v error -y -i "$out_dir/demo.webm" \
  -vf "fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=4" \
  "$out_dir/demo.gif"
ls -l "$out_dir/demo.gif" "$out_dir/demo.webm" >&2
