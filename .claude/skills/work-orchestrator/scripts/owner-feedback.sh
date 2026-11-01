#!/usr/bin/env bash
# Separates the owner's own issues, PRs and comments from the agent-generated pile.
# Agents and the owner share one GitHub login, so the only signal is the agent marker: a comment or body that contains
# `<!-- agent:`, an `agent · Claude` signature, `Opened by ...` or an `Agent:` trailer was written by an agent; everything else
# by the owner (case-insensitive).
# Usage:
#   .claude/skills/work-orchestrator/scripts/owner-feedback.sh sweep       label every issue/PR `owner` (the owner created or commented on it) or `agent-created`,
#                                         then list owner comments that have no agent reply after them (the orchestrator runs this itself)
#   .claude/skills/work-orchestrator/scripts/owner-feedback.sh unanswered  only the list, no label changes
# An owner comment counts as answered once an agent-marked comment follows it; a newer owner comment brings the item back.
# On GitHub, `label:owner` finds everything the owner created or commented on; `label:agent-created` is the rest.
set -euo pipefail

mode="${1:-}"
case "$mode" in
  sweep|unanswered) ;;
  *) sed -n '2,10p' "$0" >&2; exit 2 ;;
esac

cd "$(dirname "$0")" && cd "$(git rev-parse --show-toplevel)"

login="$(gh api user --jq .login)"
repository="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
owner_label="owner"
agent_label="agent-created"

if [ "$mode" = "sweep" ]; then
  gh label create "$owner_label" --color "D93F0B" --description "Created or commented by the owner" 2>/dev/null || true
  gh label create "$agent_label" --color "BFD4F2" --description "Created by an agent, not commented by the owner" 2>/dev/null || true
fi

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

gh issue list --state all --limit 2000 --json number,title,state,author,body,labels,comments > "$work_dir/issues.json"
gh pr list --state all --limit 2000 --json number,title,state,author,body,labels,comments,reviews > "$work_dir/pulls.json"
gh api "repos/$repository/pulls/comments" --paginate --jq '[.[]|{pull:(.pull_request_url|split("/")|last|tonumber),login:.user.login,body:.body,at:.created_at}]' |
  jq -s 'add // []' > "$work_dir/inline.json"

# One record per issue or PR: owner-written body, owner comments, and owner comments without a later agent comment.
jq -n --arg login "$login" --arg ownerLabel "$owner_label" --arg agentLabel "$agent_label" \
  --slurpfile issues "$work_dir/issues.json" --slurpfile pulls "$work_dir/pulls.json" --slurpfile inline "$work_dir/inline.json" '
  # Markdown emphasis is stripped so a bold `**Agent: role**` header counts; the marker may start the body.
  def agent: (. // "" | ascii_downcase | gsub("[*_]"; "")) | (contains("<!-- agent:") or contains("agent · claude") or contains("opened by") or contains("\nagent: ") or test("^\\s*agent: "));
  def entry($author; $body; $at): {mine: ($author == $login), agent: ($body | agent), at: $at};
  [ ($issues[0][] | . + {kind: "issue", reviews: []}), ($pulls[0][] | . + {kind: "pull"}) ]
  | map(. as $item
    | ([ .comments[] | entry(.author.login; .body; .createdAt) ]
       + [ .reviews[] | select((.body // "") != "") | entry(.author.login; .body; .submittedAt) ]
       + [ $inline[0][] | select(.pull == $item.number) | entry(.login; .body; .at) ]) as $comments
    | ($comments | map(select(.mine and (.agent | not)))) as $ownerComments
    | ($comments | map(select(.agent))) as $agentComments
    | {
        kind, number, title, state,
        labels: [.labels[].name],
        ownerBody: ((.author.login == $login) and ((.body | agent) | not)),
        ownerCommentCount: ($ownerComments | length),
        unanswered: ([ $ownerComments[] | .at as $when | select(([ $agentComments[] | select(.at > $when) ] | length) == 0) ] | length)
      })
  | map(. + {wantLabel: (if (.ownerBody or .ownerCommentCount > 0) then $ownerLabel else $agentLabel end)})
' > "$work_dir/items.json"

if [ "$mode" = "sweep" ]; then
  # Exactly one of the two labels per item: add the wanted one, remove the other.
  jq -r --arg ownerLabel "$owner_label" --arg agentLabel "$agent_label" '.[] | . as $item
    | (if $item.wantLabel == $ownerLabel then $agentLabel else $ownerLabel end) as $otherLabel
    | (if ($item.labels | index($item.wantLabel)) then empty else "add\t\($item.number)\t\($item.wantLabel)" end),
      (if ($item.labels | index($otherLabel)) then "remove\t\($item.number)\t\($otherLabel)" else empty end)' "$work_dir/items.json" |
    while IFS=$'\t' read -r action number label; do
      gh issue edit "$number" "--$action-label" "$label" >/dev/null 2>&1 || echo "could not $action label $label on #$number" >&2
    done
  jq -r '"Owner items: \(map(select(.wantLabel == "owner")) | length) of \(length) issues and PRs"' "$work_dir/items.json"
fi

echo "== Owner comments with no agent reply after them"
jq -r '.[] | select(.unanswered > 0) | "#\(.number) [\(.state)] \(.unanswered) unanswered: \(.title)"' "$work_dir/items.json"
