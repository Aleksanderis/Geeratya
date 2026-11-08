#!/usr/bin/env bash
# Tests scripts/install.sh against a fake GitHub releases API on disk (file:// URLs, no network, no real GitHub, install
# folder in a temp dir): install, skip when current, update, pinned version, arm64 and macOS, checksum mismatch, missing
# SHA256SUMS or entry, missing asset, unsupported architecture and system (#860).
# Run: scripts/test-install.sh   (not part of `dotnet test`)
set -euo pipefail
unset GEERATYA_VERSION GEERATYA_FORCE GEERATYA_INSTALL_DIR GEERATYA_RELEASES_URL GEERATYA_OS GEERATYA_ARCH
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

# A fake release made with the real packer: <api dir>/tags/v<version> holds the release JSON, SHA256SUMS next to the
# archives (the names release.yml produces), and "latest" is a copy made by set_latest.
make_release() { # <version> <api dir> <rid>...
  local version="$1" api="$2"; shift 2
  local assets="$api/assets-$version"
  mkdir -p "$assets" "$api/tags" "$tmp/publish"
  printf '#!/bin/sh\necho "Geeratya %s"\n' "$version" > "$tmp/publish/geeratya"
  chmod +x "$tmp/publish/geeratya"
  local rid
  for rid in "$@"; do
    "$root/scripts/package-release.sh" "$tmp/publish" "$version" "$rid" "$assets" >/dev/null
  done
  (cd "$assets" && sha256sum geeratya-* > SHA256SUMS)
  {
    echo "{ \"tag_name\": \"v$version\", \"assets\": ["
    local file first=1
    for file in "$assets"/*; do
      [ "$first" = 1 ] || echo ","
      first=0
      printf '  { "name": "%s", "browser_download_url": "file://%s" }' "$(basename "$file")" "$file"
    done
    echo
    echo "] }"
  } > "$api/tags/v$version"
}
set_latest() { cp "$2/tags/v$1" "$2/latest"; } # <version> <api dir>

run_install() { # <api dir> [env assignments...]; prints combined output
  local api="$1"; shift
  env "$@" GEERATYA_RELEASES_URL="file://$api" GEERATYA_INSTALL_DIR="$tmp/bin" \
    PATH="$tmp/bin:$PATH" sh "$root/scripts/install.sh" 2>&1
}
installed_version() { "$tmp/bin/geeratya" --version; }

api="$tmp/api"
make_release 1.2.3 "$api" linux-x64 linux-arm64 osx-arm64
make_release 1.3.0 "$api" linux-x64
set_latest 1.2.3 "$api"

out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "install failed: $out"
[ -x "$tmp/bin/geeratya" ] || fail "binary not installed"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "wrong binary installed"
grep -q "To uninstall: rm $tmp/bin/geeratya" <<<"$out" || fail "no uninstall hint: $out"
[ ! -e "$tmp/bin/geeratya.new" ] || fail "left the temporary file behind"

out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "second run failed: $out"
grep -q "already installed" <<<"$out" || fail "no skip when current: $out"

# Update: a newer latest replaces the installed one.
set_latest 1.3.0 "$api"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "update failed: $out"
grep -q "Updating Geeratya 1.2.3 -> 1.3.0" <<<"$out" || fail "no update message: $out"
[ "$(installed_version)" = "Geeratya 1.3.0" ] || fail "not updated"

# Pinned version: goes back to 1.2.3 although 1.3.0 is the latest; "v" prefix accepted; force reinstalls the same version.
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=v1.2.3)" || fail "pinned install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "pinned version not installed: $out"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=1.2.3)" || fail "pinned rerun failed: $out"
grep -q "already installed" <<<"$out" || fail "no skip for the pinned current version: $out"
printf '#!/bin/sh\necho "Geeratya broken"\n' > "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=1.2.3 GEERATYA_FORCE=1)" || fail "forced install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "force did not reinstall"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=9.9.9)"; then fail "unknown version accepted"; fi
grep -q "download failed" <<<"$out" || fail "no message for an unknown version: $out"

# arm64 and macOS pick their own archives.
rm "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=aarch64 GEERATYA_VERSION=1.2.3)" || fail "arm64 install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "arm64 not installed"
rm "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Darwin GEERATYA_ARCH=arm64 GEERATYA_VERSION=1.2.3)" || fail "macOS install failed: $out"
grep -q "xattr -d com.apple.quarantine" <<<"$out" || fail "no quarantine note on macOS: $out"
[ -x "$tmp/bin/geeratya" ] || fail "macOS binary not installed"
rm "$tmp/bin/geeratya"

# A release without the wanted archive says which one is missing.
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=aarch64)"; then fail "missing arm64 asset accepted"; fi
grep -q "has no linux-arm64 archive" <<<"$out" || fail "no missing-asset message: $out"

# Checksum mismatch, missing entry, missing SHA256SUMS: nothing is installed.
sums="$api/assets-1.2.3/SHA256SUMS"
cp "$sums" "$tmp/SHA256SUMS.good"
printf '%064d  geeratya-1.2.3-linux-x64.tar.gz\n' 0 > "$sums"
set_latest 1.2.3 "$api"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "checksum mismatch accepted"; fi
grep -q "checksum mismatch" <<<"$out" || fail "no checksum message: $out"
[ ! -e "$tmp/bin/geeratya" ] || fail "binary installed despite a bad checksum"
printf '%064d  geeratya-1.2.3-linux-arm64.tar.gz\n' 0 > "$sums"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "missing checksum entry accepted"; fi
grep -q "no entry for geeratya-1.2.3-linux-x64.tar.gz" <<<"$out" || fail "no missing-entry message: $out"
cp "$tmp/SHA256SUMS.good" "$sums"
sed 's|, *{ "name": "SHA256SUMS"[^}]*}||; /"name": "SHA256SUMS"/d' "$api/tags/v1.2.3" > "$api/latest"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "release without SHA256SUMS accepted"; fi
grep -q "no SHA256SUMS" <<<"$out" || fail "no missing-sums message: $out"
[ ! -e "$tmp/bin/geeratya" ] || fail "binary installed without a checksum file"

# Unsupported platforms.
set_latest 1.2.3 "$api"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=riscv64)"; then fail "unsupported arch accepted"; fi
grep -q "unsupported architecture" <<<"$out" || fail "no arch message: $out"
if out="$(run_install "$api" GEERATYA_OS=FreeBSD GEERATYA_ARCH=x86_64)"; then fail "unsupported system accepted"; fi
grep -q "unsupported system" <<<"$out" || fail "no system message: $out"
if out="$(run_install "$api" GEERATYA_OS=Darwin GEERATYA_ARCH=x86_64)"; then fail "macOS x64 accepted"; fi
grep -q "Apple silicon only" <<<"$out" || fail "no macOS x64 message: $out"
echo "ok"
