#!/usr/bin/env bash
# Idempotent sample data for the dev site sandbox projects GCM and GTM.
# Usage: scripts/seed-dev-site.sh [--dry-run] [--reset [--yes]] [--project GCM|GTM]
#   (default)   find-or-create every seed issue, comment, link, attachment and remote link; print counts
#   --dry-run   print the plan only; no network call at all
#   --reset     delete only issues carrying the label geeratya-seed; lists them unless --yes is also given.
#               The dev account may lack delete permission (issue #19): failures are reported, not fatal.
#   --project   limit to one project
# Every seed issue carries the label `geeratya-seed` plus a unique `geeratya-seed-<project>-<slug>`;
# the slug label is how an existing issue is found, so a second run creates nothing new.
# All HTTP goes through scripts/record-fixture.sh, which refuses hosts outside GEERATYA_LIVE_HOSTS,
# refuses writes outside GCM/GTM, loads the env file itself and never prints the token.
# Needs bash, curl, jq and python3. Ranking follows creation order (new issues rank last).
# Jira's search index can lag a few seconds: wait a moment before re-running right after a run.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
rec="$here/record-fixture.sh"
dry=0 reset=0 yes=0 only=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) dry=1 ;;
    --reset) reset=1 ;;
    --yes) yes=1 ;;
    --project) shift; only=${1:-} ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done
projects=(GCM GTM)
if [ -n "$only" ]; then
  case "$only" in GCM | GTM) projects=("$only") ;; *) echo "project must be GCM or GTM" >&2; exit 2 ;; esac
fi

# slug|type|summary|parent slug|target column. type: Epic, Story, Task, Bug or Sub (the project's sub-task type).
spec='epic-onboarding|Epic|Seed: Onboarding flow||To Do
epic-reporting|Epic|Seed: Reporting||In Progress
story-signup|Story|Sign up with email|epic-onboarding|In Progress
story-welcome|Story|Welcome screen|epic-onboarding|To Do
story-export|Story|Export report as CSV|epic-reporting|Done
task-copy|Task|Review onboarding copy|epic-onboarding|To Do
task-charts|Task|Pick a chart library|epic-reporting|In Progress
task-docs|Task|Write the setup guide||Done
bug-crash|Bug|App crashes on empty profile|epic-onboarding|In Progress
bug-date|Bug|Wrong date format in report|epic-reporting|To Do
sub-form|Sub|Build the form|story-signup|Done
sub-validate|Sub|Validate the email|story-signup|To Do
backlog-1|Task|Backlog: dark mode||Backlog
backlog-2|Story|Backlog: keyboard shortcuts help||Backlog
backlog-3|Bug|Backlog: tooltip clipped||Backlog
backlog-4|Task|Backlog: update dependencies||Backlog'
# Links: slug|link type name|other slug (the first issue is the outward side)
links='story-signup|Blocks|story-welcome
bug-crash|Relates|story-signup'

if [ "$dry" = 1 ]; then
  for p in "${projects[@]}"; do
    echo "project $p"
    while IFS='|' read -r slug type summary parent col; do
      printf '  %-14s %-5s %-34s parent=%-16s column=%s\n' "$slug" "$type" "$summary" "${parent:--}" "$col"
    done <<< "$spec"
    echo "  comments: rich ADF (list, code, mention, table) on story-signup; plain on story-welcome"
    echo "  attachment: seed-notes.txt on story-signup; remote link on story-signup"
    while IFS='|' read -r a t b; do echo "  link: $a $t $b"; done <<< "$links"
  done
  echo "dry run: no request sent"
  exit 0
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
umask 077

# api METHOD PATH [BODY-FILE]: prints the response body, fails on a non-2xx status.
# Retries on 429 (the transport prints the status to stderr).
api() {
  local tries=0 status
  while :; do
    RECORD_RAW=1 "$rec" "$@" > "$work/out" 2> "$work/err" || true
    status=$(sed -n 's/^status: //p' "$work/err" | tail -n1)
    case "$status" in
      2??) cat "$work/out"; return 0 ;;
      429) tries=$((tries + 1)); if [ $tries -gt 3 ]; then break; fi; sleep $((tries * 5)) ;;
      *) break ;;
    esac
  done
  echo "request failed: $1 ${2%%\?*} -> status ${status:-none} $(grep -v '^status:' "$work/err" | head -n2)" >&2
  return 1
}
write() { sleep 0.3; api "$@"; }

myself=$(api GET /rest/api/3/myself | jq -r .accountId)

adf_text() { jq -n --arg t "$1" '{type:"doc",version:1,content:[{type:"paragraph",content:[{type:"text",text:$t}]}]}'; }

rich_comment() {
  jq -n --arg id "$myself" '{body:{type:"doc",version:1,content:[
    {type:"heading",attrs:{level:2},content:[{type:"text",text:"Seed comment with rich content"}]},
    {type:"paragraph",content:[{type:"text",text:"Hello "},{type:"mention",attrs:{id:$id,text:"@seed-user"}},
      {type:"text",text:", this has "},{type:"text",text:"bold",marks:[{type:"strong"}]},{type:"text",text:" and "},
      {type:"text",text:"inline code",marks:[{type:"code"}]},{type:"text",text:"."}]},
    {type:"bulletList",content:[
      {type:"listItem",content:[{type:"paragraph",content:[{type:"text",text:"first bullet"}]}]},
      {type:"listItem",content:[{type:"paragraph",content:[{type:"text",text:"second bullet"}]}]}]},
    {type:"orderedList",content:[
      {type:"listItem",content:[{type:"paragraph",content:[{type:"text",text:"step one"}]}]},
      {type:"listItem",content:[{type:"paragraph",content:[{type:"text",text:"step two"}]}]}]},
    {type:"codeBlock",attrs:{language:"csharp"},content:[{type:"text",text:"var x = 1;\nConsole.WriteLine(x);"}]},
    {type:"table",attrs:{isNumberColumnEnabled:false,layout:"default"},content:[
      {type:"tableRow",content:[
        {type:"tableHeader",attrs:{},content:[{type:"paragraph",content:[{type:"text",text:"Key"}]}]},
        {type:"tableHeader",attrs:{},content:[{type:"paragraph",content:[{type:"text",text:"Value"}]}]}]},
      {type:"tableRow",content:[
        {type:"tableCell",attrs:{},content:[{type:"paragraph",content:[{type:"text",text:"a"}]}]},
        {type:"tableCell",attrs:{},content:[{type:"paragraph",content:[{type:"text",text:"1"}]}]}]}]}]}}'
}

declare -A keyof # "<project>/<slug>" -> issue key
declare -A cnt_new cnt_found

search_seed() { # project -> $work/res.json
  jq -n --arg j "project = $1 AND labels = geeratya-seed" '{jql:$j,maxResults:100,fields:["labels","summary","status"]}' > "$work/q.json"
  api POST /rest/api/3/search/jql "$work/q.json" > "$work/res.json"
}

if [ "$reset" = 1 ]; then
  for p in "${projects[@]}"; do
    search_seed "$p"
    echo "$p: $(jq '.issues|length' "$work/res.json") issue(s) labelled geeratya-seed"
    if [ "$yes" != 1 ]; then jq -r '.issues[]|"  "+.key+" "+.fields.summary' "$work/res.json"; continue; fi
    echo "Jira has no undo: deleting."
    # Highest keys first: sub-tasks were created after their parents.
    for key in $(jq -r --arg p "$p" '.issues|map(.key|ltrimstr($p+"-")|tonumber)|sort|reverse|.[]|$p+"-"+tostring' "$work/res.json"); do
      if write DELETE "/rest/api/3/issue/$key?deleteSubtasks=true" > /dev/null; then echo "  deleted $key"; fi
    done
  done
  [ "$yes" = 1 ] || echo "nothing deleted; pass --yes to delete the issues above"
  exit 0
fi

transition_to() {
  local key=$1 col=$2 cur tid
  cur=$(api GET "/rest/api/3/issue/$key?fields=status" | jq -r .fields.status.name)
  if [ "$cur" = "$col" ]; then return 0; fi
  tid=$(api GET "/rest/api/3/issue/$key/transitions" | jq -r --arg c "$col" '[.transitions[]|select(.to.name==$c)][0].id // empty')
  if [ -z "$tid" ]; then echo "  no transition $cur -> $col for $key" >&2; return 0; fi
  jq -n --arg t "$tid" '{transition:{id:$t}}' > "$work/t.json"
  write POST "/rest/api/3/issue/$key/transitions" "$work/t.json" > /dev/null
}

for p in "${projects[@]}"; do
  echo "== $p"
  cnt_new[$p]=0 cnt_found[$p]=0
  search_seed "$p"
  while IFS=$'\t' read -r key lab; do
    keyof["$p/${lab#geeratya-seed-"$p"-}"]=$key
  done < <(jq -r --arg p "$p" '.issues[]|[.key, (.fields.labels[]|select(startswith("geeratya-seed-"+$p+"-")))]|@tsv' "$work/res.json")
  api GET "/rest/api/3/issue/createmeta/$p/issuetypes" > "$work/types.json"
  epic_t=$(jq -r '[.issueTypes[]|select(.hierarchyLevel==1)][0].id' "$work/types.json")
  sub_t=$(jq -r '[.issueTypes[]|select(.subtask==true)][0].id' "$work/types.json")
  while IFS='|' read -r slug type summary parent col; do
    k="$p/$slug"
    if [ -n "${keyof[$k]:-}" ]; then
      cnt_found[$p]=$((cnt_found[$p] + 1)); echo "  found   ${keyof[$k]} $slug"
      continue
    fi
    case "$type" in
      Epic) t=$epic_t ;;
      Sub) t=$sub_t ;;
      *) t=$(jq -r --arg n "$type" '[.issueTypes[]|select(.name==$n)][0].id' "$work/types.json") ;;
    esac
    jq -n --arg p "$p" --arg t "$t" --arg s "$summary" --arg l "geeratya-seed-$p-$slug" --arg par "${keyof[$p/$parent]:-}" \
      --argjson d "$(adf_text "Sample issue created by scripts/seed-dev-site.sh ($slug).")" \
      '{fields:({project:{key:$p},issuetype:{id:$t},summary:$s,description:$d,labels:["geeratya-seed",$l]}
        + (if $par != "" then {parent:{key:$par}} else {} end))}' > "$work/c.json"
    key=$(write POST /rest/api/3/issue "$work/c.json" | jq -r .key)
    keyof[$k]=$key
    cnt_new[$p]=$((cnt_new[$p] + 1)); echo "  created $key $slug"
    transition_to "$key" "$col" || true
  done <<< "$spec"

  s=${keyof[$p/story-signup]} w=${keyof[$p/story-welcome]}
  # Comments, attachment and remote link: added once per issue, detected by content.
  if [ "$(api GET "/rest/api/3/issue/$s/comment" | jq '[.comments[]|select(.body|tostring|contains("Seed comment with rich content"))]|length')" = 0 ]; then
    rich_comment > "$work/cm.json"
    write POST "/rest/api/3/issue/$s/comment" "$work/cm.json" > /dev/null
    echo "  comment (rich) on $s"
  fi
  if [ "$(api GET "/rest/api/3/issue/$w/comment" | jq '[.comments[]|select(.body|tostring|contains("Seed plain comment"))]|length')" = 0 ]; then
    jq -n --argjson b "$(adf_text "Seed plain comment.")" '{body:$b}' > "$work/cm.json"
    write POST "/rest/api/3/issue/$w/comment" "$work/cm.json" > /dev/null
    echo "  comment (plain) on $w"
  fi
  if [ "$(api GET "/rest/api/3/issue/$s?fields=attachment" | jq '[.fields.attachment[]|select(.filename=="seed-notes.txt")]|length')" = 0 ]; then
    b=geeratyaseedboundary
    {
      printf -- '--%s\r\nContent-Disposition: form-data; name="file"; filename="seed-notes.txt"\r\nContent-Type: text/plain\r\n\r\n' "$b"
      printf 'Sample attachment from scripts/seed-dev-site.sh\r\n'
      printf -- '--%s--\r\n' "$b"
    } > "$work/att.bin"
    RECORD_XSRF=1 RECORD_CONTENT_TYPE="multipart/form-data; boundary=$b" write POST "/rest/api/3/issue/$s/attachments" "$work/att.bin" > /dev/null
    echo "  attachment on $s"
  fi
  if [ "$(api GET "/rest/api/3/issue/$s/remotelink" | jq '[.[]|select(.globalId=="geeratya-seed-remote")]|length')" = 0 ]; then
    jq -n '{globalId:"geeratya-seed-remote",object:{url:"https://example.com/geeratya-seed",title:"Seed remote link"}}' > "$work/rl.json"
    write POST "/rest/api/3/issue/$s/remotelink" "$work/rl.json" > /dev/null
    echo "  remote link on $s"
  fi
  # Issue links, skipped when the first issue already links to the second.
  while IFS='|' read -r a lt b; do
    ka=${keyof[$p/$a]} kb=${keyof[$p/$b]}
    if [ "$(api GET "/rest/api/3/issue/$ka?fields=issuelinks" | jq --arg o "$kb" '[.fields.issuelinks[]|select((.outwardIssue.key // .inwardIssue.key)==$o)]|length')" = 0 ]; then
      jq -n --arg t "$lt" --arg i "$kb" --arg o "$ka" '{type:{name:$t},inwardIssue:{key:$i},outwardIssue:{key:$o}}' > "$work/l.json"
      write POST /rest/api/3/issueLink "$work/l.json" > /dev/null
      echo "  link $ka $lt $kb"
    fi
  done <<< "$links"
done

echo "== counts (labelled geeratya-seed)"
for p in "${projects[@]}"; do
  search_seed "$p"
  echo "$p: $(jq '.issues|length' "$work/res.json") total (${cnt_new[$p]} created, ${cnt_found[$p]} found this run)"
  jq -r '[.issues[].fields.status.name]|group_by(.)|map("    "+.[0]+": "+(length|tostring))|.[]' "$work/res.json"
done
