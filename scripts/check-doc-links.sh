#!/usr/bin/env bash
# Link check over the Markdown docs: every relative link target in README.md,
# CONTRIBUTING.md, SECURITY.md and docs/**/*.md must exist, and every
# `docs/...` path written in backticks anywhere in tracked text must exist.
# Usage: scripts/check-doc-links.sh   (run from anywhere; exits 1 on any miss)
set -u
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root" || exit 1
bad=0

while IFS= read -r file; do
  dir="$(dirname "$file")"
  # [text](target) with a relative target; drop #anchor, skip URLs and mailto.
  grep -oE '\]\([^)[:space:]]+\)' "$file" | sed -E 's/^\]\((.*)\)$/\1/' | while IFS= read -r target; do
    case "$target" in
      http://*|https://*|mailto:*|\#*|mention:*|url|site/*) continue ;;
    esac
    path="${target%%#*}"
    [ -z "$path" ] && continue
    if [ ! -e "$dir/$path" ]; then
      echo "BROKEN LINK: $file -> $target"
      echo x >> "$root/.doc-links-bad.tmp"
    fi
  done
done < <(git ls-files 'README.md' 'CONTRIBUTING.md' 'SECURITY.md' 'docs/*.md' 'docs/**/*.md')

# docs/... paths in backticks or prose anywhere in tracked files.
while IFS= read -r hit; do
  file="${hit%%:*}"
  rest="${hit#*:}"
  for p in $(printf '%s' "$rest" | grep -oE 'docs/[A-Za-z0-9_./-]+\.(md|json)'); do
    # Known non-paths: Gitunia's own doc, and the retro skill's file (created on first use).
    case "$p" in docs/ux.md|docs/internal/retrospectives.md) continue ;; esac
    if [ ! -e "$p" ]; then
      echo "BROKEN PATH: $file mentions $p"
      echo x >> "$root/.doc-links-bad.tmp"
    fi
  done
done < <(git grep -nIE 'docs/[A-Za-z0-9_./-]+\.(md|json)' -- . ':!docs/internal/history' ':!.github')

if [ -e "$root/.doc-links-bad.tmp" ]; then
  rm -f "$root/.doc-links-bad.tmp"
  exit 1
fi
echo "doc links ok"
