#!/usr/bin/env bash
# Regression test of the agent-marker detection in owner-feedback.sh: agent comments (marker, signature, PR footer, "Agent:" line)
# count as agent text, plain owner text does not. Uses the real `def agent` line of the script.
# Usage: test-owner-feedback.sh   (exit code 1 on failure; needs jq)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
def="$(grep -m1 -o 'def agent:.*;' "$root/.claude/skills/work-orchestrator/scripts/owner-feedback.sh")"
[ -n "$def" ] || { echo "FAIL: 'def agent' line not found in owner-feedback.sh"; exit 1; }
fail=0

# check <name> <body> <expected true|false>
check() {
    local got
    got="$(jq -n --arg body "$2" "$def"' $body | agent')"
    if [ "$got" != "$3" ]; then echo "FAIL: $1 (got $got, want $3)"; fail=1; else echo "ok: $1"; fi
}

check "html marker" '<!-- agent: reviewer -->' true
check "marker inside body" $'### Findings\n<!-- agent: implementer -->\n\nDone.' true
check "signature line" $'Fixed.\n\n---\n<sub>Implementer agent · Claude · PR #1 @ abc</sub>' true
check "pr footer" $'Closes #1\n\nOpened by orchestrator agent' true
check "agent line after newline" $'Fixed.\n\nAgent: implementer' true
check "agent line at start" $'Agent: orchestrator\n\nDone.' true
check "bold agent line at start" '**Agent: implementer**' true
check "leading blank lines" $'\n\nAgent: x' true
check "owner text" 'looks good, agent: nope inline' false
check "owner plain sentence" 'Please make the agent faster' false
check "empty body" '' false
exit $fail
