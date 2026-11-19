#!/usr/bin/env bash
# Records the README demo: the real binary against tools/FakeJira in --demo mode (an invented dataset, Wayfinder, a
# trip planning app; never a real site), scripted by scripts/demo.tape (VHS + ttyd, fixed terminal size and theme,
# short pauses). Linux only. No media is committed to this repository.
# Usage: scripts/demo.sh --binary <geeratya> --out <dir>
#   --binary  the AOT binary (dotnet publish src/Geeratya.Tui -c Release -o <outdir>)
#   --out     output directory (outside the repository): demo.gif (README), demo.webm (landing page, bigger
#             terminal) and screenshots/*.png (stills of the
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
  sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2
  exit 2
fi
[ -x "$binary" ] || { echo "binary '$binary' not found or not executable" >&2; exit 2; }
for tool in vhs ttyd ffmpeg dotnet; do
  command -v "$tool" > /dev/null 2>&1 || { echo "missing tool '$tool' (vhs needs ttyd and ffmpeg)" >&2; exit 3; }
done

STILLS_WIDTH=1650 STILLS_HEIGHT=960 STILLS_FONT=14 # same aspect as the demo (1100x640), about 190 columns

work=$(mktemp -d)
fake_pid=""
trap 'if [ -n "$fake_pid" ]; then kill "$fake_pid" 2> /dev/null || true; fi; rm -rf "$work"' EXIT
trap 'exit 130' INT; trap 'exit 143' TERM; trap 'exit 129' HUP # The EXIT trap runs on exit; bash skips it on a bare signal

dotnet build "$root/tools/FakeJira" -c Release -o "$work/fakejira" -v q --nologo > "$work/build.log" 2>&1 \
  || { tail -20 "$work/build.log" >&2; exit 1; }
mkdir -p "$out_dir" "$work/shots"

# One run of a tape: a fresh demo fake (the tape changes its in-memory data, so every run starts from the same
# state), one active fake profile and empty state and cache in private XDG directories (nothing of the owner's
# configuration or sites is read). The tape runs in $work, so its Output and shots/*.png land there.
unset GEERATYA_LIVE_HOSTS
record() {
  rm -rf "$work/config" "$work/state" "$work/cache"
  mkdir -p "$work/config/geeratya" "$work/state" "$work/cache"
  : > "$work/fake.url"
  dotnet "$work/fakejira/fakejira.dll" --demo --parent-pid $$ > "$work/fake.url" &
  fake_pid=$!
  for _ in $(seq 1 50); do [ -s "$work/fake.url" ] && break; sleep 0.1; done
  fake_url=$(head -n 1 "$work/fake.url")
  case $fake_url in http://127.0.0.1:* | http://localhost:*) ;; *) echo "unexpected fake Jira address" >&2; exit 1 ;; esac
  printf '{"activeName":"fake","profiles":[{"name":"fake","siteUrl":"%s","email":"fake@example.com"}]}\n' "$fake_url" \
    > "$work/config/geeratya/profiles.json"
  (
    cd "$work"
    DEMO_BIN="$binary" XDG_CONFIG_HOME="$work/config" XDG_STATE_HOME="$work/state" XDG_CACHE_HOME="$work/cache" \
      GEERATYA_TOKEN=fake vhs "$1" >&2
  )
  kill "$fake_pid" 2> /dev/null || true
  wait "$fake_pid" 2> /dev/null || true
  fake_pid=""
}

# Three runs of the same tape. 1: the demo terminal (1100x640, font 15) for the README GIF, which must stay legible
# at 960 px and under 4 MB. 2: the landing page video (demo.webm) in a bigger terminal (more columns and rows at a
# smaller font, so it shows the whole screen instead of a zoomed-in part; the page shows it about 1150 px wide).
# 3: the landing page stills in the same bigger terminal without the zoom key (the space z lines are dropped): there
# the whole issue view fits, which zoomed would be mostly empty. Each run overwrites shots/, so the stills are run 3's.
big_tape() { # $1 output name, $2 extra sed expression
  sed -e "s/^Set FontSize .*/Set FontSize $STILLS_FONT/" -e "s/^Set Width .*/Set Width $STILLS_WIDTH/" \
    -e "s/^Set Height .*/Set Height $STILLS_HEIGHT/" -e "s/^Output .*/Output $1.webm/" -e "$2" \
    "$script_dir/demo.tape" > "$work/$1.tape"
}
record "$script_dir/demo.tape"
big_tape page ''
record "$work/page.tape"
big_tape stills '/^Type " z"$/d'
record "$work/stills.tape"

# A caption bar under the terminal names the pressed keys (VHS has no key overlay): "start end text" in seconds of
# the recording, matched to the tape's pacing (a changed tape needs new times; the bar is skipped without a font).
CAPTIONS=(
  "1.0 2.1 j  next card" "2.1 3.3 l  next column" "3.3 4.1 j  next card" "4.1 5.3 h  previous column"
  "5.3 6.5 k  previous card" "6.5 7.1 l  next column" "7.1 8.1 Enter  open the issue" "8.1 9.3 j  a section,  space z  zoom"
  "9.3 12.4 Ctrl-l  next section" "12.4 13.3 Esc  back to the board" "13.3 14.1 :backlog" "14.1 17.0 ]]  next sprint"
  "17.0 21.2 :jql  search in a new tab" "21.2 22.7 space i s  change status" "22.7 24.0 Enter  apply" "24.0 25.0 space r  refresh"
)
font=$(fc-match -f '%{file}' monospace 2> /dev/null || true)
# grep without -q reads all of ffmpeg's output: with -q it can exit first, ffmpeg dies of SIGPIPE and pipefail fails
# the check at random (which silently dropped the captions).
has_drawtext=0
if [ -n "$font" ] && [ -f "$font" ] && ffmpeg -filters 2> /dev/null | grep drawtext > /dev/null; then has_drawtext=1; fi
[ "$has_drawtext" = 1 ] || echo "warning: no monospace font or no ffmpeg drawtext, the videos have no captions" >&2
captions() { # $1 bar height, $2 font size: prints the ffmpeg filter for the caption bar
  local filter="pad=iw:ih+$1:0:0:color=0x11111b" n=0 c t0 t1 text
  if [ "$has_drawtext" = 1 ]; then
    for c in "${CAPTIONS[@]}"; do
      read -r t0 t1 text <<< "$c"
      n=$((n + 1))
      printf '%s' "$text" > "$work/cap$n.txt"
      filter+=",drawtext=fontfile=$font:textfile=$work/cap$n.txt:fontcolor=0xcdd6f4:fontsize=$2:x=16:y=h-$(($1 - 6)):enable='between(t,$t0,$t1)'"
    done
  fi
  printf '%s' "$filter"
}
for v in demo page; do
  duration=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$work/$v.webm")
  awk -v d="$duration" -v v="$v" 'BEGIN { if (d < 23 || d > 27) print "warning: " v " recording is " d " s, the CAPTIONS times were tuned for about 25 s" > "/dev/stderr" }'
done
# The page video is re-encoded for the web with the caption bar; the GIF (for the README) comes from the small run
# and is small on purpose (960 px, 48 colours, under 4 MB: GitHub's image proxy refuses big images).
ffmpeg -v error -y -i "$work/page.webm" -vf "$(captions 56 32)" -c:v libvpx-vp9 -crf 32 -b:v 0 "$out_dir/demo.webm"
ffmpeg -v error -y -i "$work/demo.webm" -vf "$(captions 44 26)" -c:v libvpx-vp9 -crf 32 -b:v 0 "$work/small.webm"
ffmpeg -v error -y -i "$work/small.webm" \
  -vf "fps=10,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=48:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" \
  "$out_dir/demo.gif"
rm -rf "$out_dir/screenshots"
cp -r "$work/shots" "$out_dir/screenshots"
ls -l "$out_dir/demo.gif" "$out_dir/demo.webm" "$out_dir/screenshots" >&2
