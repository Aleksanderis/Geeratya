#!/usr/bin/env bash
# Pack a published Native AOT build into a release archive.
# Usage: scripts/package-release.sh <publish-dir> <version> <platform> <out-dir>
#   platform: linux-x64 | linux-arm64 | win-x64 | win-arm64 | osx-arm64 (a "win-*" label makes a zip, others a tar.gz)
# Runs in Git Bash on Windows runners too (the zip is made through PowerShell there).
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 <publish-dir> <version> <platform> <out-dir>" >&2
  exit 2
fi
publish=$1
version=$2
platform=$3
out=$4
root=$(cd "$(dirname "$0")/.." && pwd)
name="geeratya-$version-$platform"

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
mkdir -p "$stage/$name" "$out"

case $platform in
  win-*) bin=geeratya.exe ;;
  *) bin=geeratya ;;
esac
cp "$publish/$bin" "$stage/$name/$bin"
cp "$root/LICENSE" "$stage/$name/LICENSE"
cat > "$stage/$name/README.txt" <<TXT
Geeratya $version ($platform)

Keyboard-first terminal client for Jira Cloud.
Run: $bin    Version: $bin --version    Help: $bin --help
Create an API token for your Atlassian account and add a profile on first start.
Docs and source: https://github.com/Aleksanderis/Geeratya
TXT

case $platform in
  win-*)
    out_abs=$(cd "$out" && { pwd -W 2>/dev/null || pwd; })
    stage_abs=$(cd "$stage" && { pwd -W 2>/dev/null || pwd; })
    powershell.exe -NoProfile -Command "Compress-Archive -Path '$stage_abs\\$name' -DestinationPath '$out_abs\\$name.zip'"
    echo "$out/$name.zip"
    ;;
  *)
    tar -C "$stage" -czf "$out/$name.tar.gz" "$name"
    echo "$out/$name.tar.gz"
    ;;
esac
