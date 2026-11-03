#!/usr/bin/env bash
# Regenerates docs/keymap.md from src/Geeratya.Core/Keymap/keymap.default.json.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="$root/docs/keymap.md"
temporary="$(mktemp)"
trap 'rm -f "$temporary"' EXIT

dotnet run --project "$root/src/Geeratya.Tui" -c Release -v q -- --print-keymap-doc > "$temporary"
mv "$temporary" "$target"
trap - EXIT
echo "Wrote $target"
