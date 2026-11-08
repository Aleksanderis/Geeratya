#!/usr/bin/env bash
# Tests scripts/gen-manifests.sh with placeholder hashes (no release exists to test against, #821).
# Run: scripts/test-gen-manifests.sh   (also parses the formula with ruby -c when ruby exists)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
l=$(printf 'a%.0s' $(seq 64)); w=$(printf 'b%.0s' $(seq 64)); o=$(printf 'c%.0s' $(seq 64))
printf '%s  geeratya-1.2.3-linux-x64.tar.gz\n%s  geeratya-1.2.3-win-x64.zip\n%s  geeratya-1.2.3-osx-arm64.tar.gz\n' "$l" "$w" "$o" > "$tmp/SHA256SUMS"

"$root/scripts/gen-manifests.sh" 1.2.3 "$tmp/SHA256SUMS" "$tmp/out" >/dev/null
f="$tmp/out/homebrew-tap/Formula/geeratya.rb"
[ -f "$f" ] || fail "no tap formula"
cmp -s "$f" "$tmp/out/homebrew/geeratya.rb" || fail "tap formula differs from homebrew/geeratya.rb"
grep -q 'download/v1.2.3/geeratya-1.2.3-osx-arm64.tar.gz"$' "$f" || fail "macOS url"
grep -q "sha256 \"$o\"" "$f" || fail "macOS hash"
grep -q 'download/v1.2.3/geeratya-1.2.3-linux-x64.tar.gz"$' "$f" || fail "linux url"
grep -q "sha256 \"$l\"" "$f" || fail "linux hash"
grep -q 'geeratya --version' "$f" || fail "test block"
grep -q 'v1.2.3' "$tmp/out/homebrew-tap/README.md" || fail "tap README version"
grep -q 'brew install geeratya/tap/geeratya' "$tmp/out/homebrew-tap/README.md" || fail "tap README install line"
if grep -rq '@[A-Z0-9]*@' "$tmp/out"; then fail "placeholder left"; fi
if command -v ruby >/dev/null; then ruby -c "$f" >/dev/null || fail "ruby syntax"; else echo "ruby not found: syntax not checked"; fi

printf '%s  geeratya-1.2.3-linux-x64.tar.gz\n' "$l" > "$tmp/short"
if "$root/scripts/gen-manifests.sh" 1.2.3 "$tmp/short" "$tmp/o2" 2>/dev/null; then fail "missing checksum accepted"; fi
echo "ok"
