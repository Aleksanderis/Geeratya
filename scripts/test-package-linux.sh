#!/usr/bin/env bash
# Tests scripts/package-linux.sh offline with a fake linux archive: the Arch package name, .PKGINFO and payload
# (the .deb/.rpm are built too, so rpmbuild is needed; skipped with a note when it is missing).
# Run: scripts/test-package-linux.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
command -v rpmbuild >/dev/null || { echo "skip: rpmbuild not found"; exit 0; }
command -v zstd >/dev/null || fail "zstd not found"

check() { # <rid> <arch> <version> <pkgver>
  local d="$tmp/geeratya-$3-$1"
  mkdir -p "$d"
  printf '#!/bin/sh\necho %s\n' "$3" > "$d/geeratya"; chmod +x "$d/geeratya"; echo MIT > "$d/LICENSE"
  tar -czf "$tmp/geeratya-$3-$1.tar.gz" -C "$tmp" "geeratya-$3-$1"
  "$root/scripts/package-linux.sh" "$tmp/geeratya-$3-$1.tar.gz" "$3" "$tmp/out" >/dev/null
  local pkg="$tmp/out/geeratya-$4-1-$2.pkg.tar.zst"
  [ -f "$pkg" ] || fail "missing $pkg"
  local list; list=$(zstd -dc "$pkg" | tar -tf -)
  [ "$(echo "$list" | head -1)" = ".PKGINFO" ] || fail ".PKGINFO is not the first entry"
  echo "$list" | grep -qx 'usr/bin/geeratya' || fail "no binary in the package"
  echo "$list" | grep -qx 'usr/share/licenses/geeratya/LICENSE' || fail "no license in the package"
  local info; info=$(zstd -dc "$pkg" | tar -xOf - .PKGINFO)
  grep -qx "pkgver = $4-1" <<<"$info" || fail "pkgver: $info"
  grep -qx "arch = $2" <<<"$info" || fail "arch: $info"
  grep -qx 'depend = openssl' <<<"$info" || fail "depends"
  [ "$(zstd -dc "$pkg" | tar -xOf - usr/bin/geeratya)" = "$(cat "$d/geeratya")" ] || fail "binary differs"
}
check linux-x64 x86_64 1.2.3 1.2.3
check linux-arm64 aarch64 1.2.3 1.2.3
check linux-x64 x86_64 1.2.3-rc.1 1.2.3_rc.1
echo ok
