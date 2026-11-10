#!/usr/bin/env bash
# Tests scripts/winget-open-pr.sh with canned `gh pr list` output (no network, #918).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

echo '[]' > "$tmp/none.json"
[ -z "$("$root/scripts/winget-open-pr.sh" "$tmp/none.json")" ] || fail "empty list reported a PR"

cat > "$tmp/other.json" <<'J'
[{"number":1,"title":"Other.Geeratya version 1","url":"https://example.test/1"},
 {"number":2,"title":"Geeratya.GeeratyaTools version 1","url":"https://example.test/2"}]
J
[ -z "$("$root/scripts/winget-open-pr.sh" "$tmp/other.json")" ] || fail "other package matched"

cat > "$tmp/open.json" <<'J'
[{"number":1,"title":"Other.Pkg version 1","url":"https://example.test/1"},
 {"number":5,"title":"Geeratya.Geeratya version 0.1.0","url":"https://example.test/5"}]
J
[ "$("$root/scripts/winget-open-pr.sh" "$tmp/open.json")" = "https://example.test/5" ] || fail "open PR not found"

cat > "$tmp/prefix.json" <<'J'
[{"number":9,"title":"New version: Geeratya.Geeratya version 0.2.0","url":"https://example.test/9"}]
J
[ "$("$root/scripts/winget-open-pr.sh" "$tmp/prefix.json")" = "https://example.test/9" ] || fail "prefixed title not found"

if "$root/scripts/winget-open-pr.sh" "$tmp/missing.json" 2>/dev/null; then fail "missing file accepted"; fi
echo "ok"
