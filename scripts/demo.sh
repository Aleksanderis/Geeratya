#!/usr/bin/env bash
# Records the README demo: the real binary against tools/FakeJira in --demo mode (an invented dataset, Wayfinder, a
# trip planning app; never a real site), scripted by scripts/demo.tape (VHS + ttyd, fixed terminal size and theme,
# short pauses). Linux only. No media is committed to this repository.
# Usage: scripts/demo.sh --binary <geeratya> --out <dir>
#   --binary  the AOT binary (dotnet publish src/Geeratya.Tui -c Release -o <outdir>)
#   --out     output directory (outside the repository): demo.gif, demo.webm and screenshots/*.png (stills of the
#             board, an issue, the backlog, a JQL search and a status change, taken by the tape from the same data)
# The files are hosted on the project's GitHub Pages site: copy them to the media/
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
mkdir -p "$out_dir" "$work/config/geeratya" "$work/state" "$work/cache" "$work/shots"

# Start the demo fake and seed one active fake profile in private XDG directories (nothing of the owner's
# configuration or sites is read).
unset GEERATYA_LIVE_HOSTS
: > "$work/fake.url"
dotnet "$work/fakejira/fakejira.dll" --demo > "$work/fake.url" &
fake_pid=$!
for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
fake_url=$(head -n 1 "$work/fake.url")
case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac
printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
  > "$work/config/geeratya/profiles.json"

# Record the tape in $work (demo.webm and shots/*.png land there), then stop the fake.
(
  cd "$work"
  DEMO_BIN="$binary" XDG_CONFIG_HOME="$work/config" XDG_STATE_HOME="$work/state" XDG_CACHE_HOME="$work/cache" \
    GEERATYA_TOKEN=fake vhs "$script_dir/demo.tape" >&2
)
kill "$fake_pid" 2> /dev/null || true
wait "$fake_pid" 2> /dev/null || true
fake_pid=""

# A caption bar under the terminal names the pressed keys (VHS has no key overlay): "start end text" in seconds of
# the recording, matched to the tape's pacing (a changed tape needs new times; the bar is skipped without a font).
CAPTIONS=(
  "1.0 2.1 j  next card" "2.1 3.3 l  next column" "3.3 4.1 j  next card" "4.1 5.3 h  previous column"
  "5.3 6.5 k  previous card" "6.5 7.1 l  next column" "7.1 8.1 Enter  open the issue" "8.1 9.3 j  a section,  space z  zoom"
  "9.3 12.4 Ctrl-l  next section" "12.4 13.3 Esc  back to the board" "13.3 14.1 :backlog" "14.1 17.0 ]]  next sprint"
  "17.0 21.2 :jql  search in a new tab" "21.2 22.7 space i s  change status" "22.7 24.0 Enter  apply" "24.0 25.0 space r  refresh"
)
font=$(fc-match -f '%{file}' monospace 2> /dev/null || true)
filter="pad=iw:ih+44:0:0:color=0x11111b"
if [ -n "$font" ] && [ -f "$font" ] && ffmpeg -filters 2> /dev/null | grep -q drawtext; then
  n=0
  for c in "${CAPTIONS[@]}"; do
    read -r t0 t1 text <<< "$c"
    n=$((n + 1))
    printf '%s' "$text" > "$work/cap$n.txt"
    filter+=",drawtext=fontfile=$font:textfile=$work/cap$n.txt:fontcolor=0xcdd6f4:fontsize=26:x=16:y=h-38:enable='between(t,$t0,$t1)'"
  done
fi
duration=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$work/demo.webm")
awk -v d="$duration" 'BEGIN { if (d < 23 || d > 27) print "warning: recording is " d " s, the CAPTIONS times were tuned for about 25 s" > "/dev/stderr" }'
# The video is re-encoded for the web; the GIF (for the README) gets a palette of its own.
ffmpeg -v error -y -i "$work/demo.webm" -vf "$filter" -c:v libvpx-vp9 -crf 32 -b:v 0 "$out_dir/demo.webm"
ffmpeg -v error -y -i "$out_dir/demo.webm" \
  -vf "fps=10,scale=1100:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=bayer:bayer_scale=4" \
  "$out_dir/demo.gif"
rm -rf "$out_dir/screenshots"
cp -r "$work/shots" "$out_dir/screenshots"
ls -l "$out_dir/demo.gif" "$out_dir/demo.webm" "$out_dir/screenshots" >&2
