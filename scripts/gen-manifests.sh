#!/usr/bin/env bash
# Generate the AUR PKGBUILD, winget manifest and Homebrew formula for a release from
# packaging/**/*.in and the release's SHA256SUMS. Nothing is published: the output
# is for the owner (or a later automation) to submit. Never edit the output by hand.
# Usage: scripts/gen-manifests.sh <version> <SHA256SUMS> <out-dir>
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <version> <SHA256SUMS> <out-dir>" >&2
  exit 2
fi
version=$1
sums=$2
out=$3
root=$(cd "$(dirname "$0")/.." && pwd)
maintainer=${AUR_MAINTAINER:-Aleksandras T. <noreply@users.noreply.github.com>}

sha() {
  local v
  v=$(awk -v f="geeratya-$version-$1" '$2 == f {print $1}' "$sums")
  [ -n "$v" ] || { echo "no checksum for geeratya-$version-$1 in $sums" >&2; exit 1; }
  echo "$v"
}

# render <in> <out>; the PKGBUILD pkgver may not contain '-'.
render() {
  sed -e "s|@VERSION@|$version|g" -e "s|@PKGVER@|${version//-/_}|g" \
      -e "s|@MAINTAINER@|$maintainer|g" -e "s|@SHA_LINUX@|$linux|g" \
      -e "s|@SHA_WIN@|$win|g" -e "s|@SHA_OSX@|$osx|g" "$1" > "$2"
  echo "$2"
}

mkdir -p "$out/aur" "$out/winget" "$out/homebrew"
linux=$(sha linux-x64.tar.gz)
win=$(sha win-x64.zip)
osx=$(sha osx-arm64.tar.gz)
render "$root/packaging/aur/PKGBUILD.in" "$out/aur/PKGBUILD"
render "$root/packaging/winget/Aleksanderis.Geeratya.yaml.in" "$out/winget/Aleksanderis.Geeratya.yaml"
render "$root/packaging/homebrew/geeratya.rb.in" "$out/homebrew/geeratya.rb"
