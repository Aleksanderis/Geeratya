#!/usr/bin/env bash
# Compact state of a milestone for the orchestrator: one line per item, no issue bodies.
# Usage: .claude/skills/work-orchestrator/scripts/orchestrator-state.sh "<milestone title>"
# Prints open issues (labels, blockers with OPEN/closed), open PRs, worktrees, and issues where the owner answered last.
set -euo pipefail

if [ $# -ne 1 ]; then
  sed -n '2,4p' "$0" >&2
  exit 2
fi
milestone="$1"

cd "$(dirname "$0")" && cd "$(git rev-parse --show-toplevel)"

open_numbers="$(gh issue list --state open --limit 500 --json number --jq '[.[].number]|join(" ")')"

echo "== Open issues in \"$milestone\" (blockers: #n=OPEN or #n=closed)"
gh issue list --state open --milestone "$milestone" --limit 200 --json number,title,labels,body |
  jq -r '.[] | "\(.number)\t\(.title)\t\([.labels[].name]|join(","))\t\(.body|split("\n")|map(select(startswith("Blocked by:")))|join(" ")|[scan("#[0-9]+")]|join(" "))"' |
  while IFS=$'\t' read -r number title labels blockers; do
    blocker_text=""
    for blocker in $blockers; do
      identifier="${blocker#\#}"
      case " $open_numbers " in
        *" $identifier "*) blocker_text="$blocker_text $blocker=OPEN" ;;
        *) blocker_text="$blocker_text $blocker=closed" ;;
      esac
    done
    echo "#$number $title [$labels]${blocker_text:+ blocked by:$blocker_text}"
  done

echo "== Open PRs"
gh pr list --state open --json number,title,headRefName,isDraft --jq '.[]|"#\(.number) \(.headRefName) \(.title)\(if .isDraft then " (draft)" else "" end)"'

echo "== Open issues without a milestone (assign each: gh issue edit <n> -m \"<milestone>\")"
gh issue list --state open --limit 500 --json number,title,milestone --jq '.[] | select(.milestone == null) | "#\(.number) \(.title)"'

echo "== Worktrees"
git worktree list

echo "== Owner answered last (no agent marker in the latest comment), label needs-owner"
for label in needs-owner; do
  gh issue list --state open --label "$label" --limit 100 --json number,title,comments |
    jq -r '.[] | select((.comments|length) > 0 and ((.comments|last|.body) | contains("<!-- agent:") | not)) | "#\(.number) \(.title)"'
done | sort -u
