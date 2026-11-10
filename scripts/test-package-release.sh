#!/usr/bin/env bash
# Tests scripts/package-release.sh offline with a fake published binary: archive name, layout, content and
# the usage error. Only the tar.gz platforms run here; the win-* zip is made by PowerShell on the Windows runner.
# Run: scripts/test-package-release.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

mkdir -p "$tmp/publish"
printf '#!/bin/sh\necho 1.2.3\n' > "$tmp/publish/geeratya"; chmod +x "$tmp/publish/geeratya"

for platform in linux-x64 linux-arm64 osx-arm64; do
  name="geeratya-1.2.3-$platform"
  printed=$("$root/scripts/package-release.sh" "$tmp/publish" 1.2.3 "$platform" "$tmp/out")
  [ "$printed" = "$tmp/out/$name.tar.gz" ] || fail "$platform: printed path '$printed'"
  [ -f "$tmp/out/$name.tar.gz" ] || fail "$platform: no archive"
  list=$(tar -tzf "$tmp/out/$name.tar.gz" | LC_ALL=C sort | tr '\n' ' ')
  [ "$list" = "$name/ $name/LICENSE $name/README.txt $name/geeratya " ] || fail "$platform: entries '$list'"
  tar -xzOf "$tmp/out/$name.tar.gz" "$name/README.txt" | grep -q "Geeratya 1.2.3 ($platform)" || fail "$platform: readme header"
  [ "$(tar -xzOf "$tmp/out/$name.tar.gz" "$name/geeratya")" = "$(cat "$tmp/publish/geeratya")" ] || fail "$platform: binary differs"
  tar -tvzf "$tmp/out/$name.tar.gz" "$name/geeratya" | grep -q '^-rwx' || fail "$platform: binary not executable"
done

"$root/scripts/package-release.sh" a b c >/dev/null 2>&1 && fail "wrong argument count accepted"
"$root/scripts/package-release.sh" "$tmp/nope" 1.2.3 linux-x64 "$tmp/out2" >/dev/null 2>&1 && fail "missing binary accepted"
echo "ok"
