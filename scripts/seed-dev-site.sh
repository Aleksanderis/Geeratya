#!/usr/bin/env bash
# Idempotent sample data for the dev site sandbox projects GCM, GTM (Kanban) and GCS, GTS (Scrum).
# Usage: scripts/seed-dev-site.sh [--dry-run] [--reset [--yes]] [--project GCM|GTM|GCS|GTS]
#   (default)   find-or-create every seed issue, comment, link, attachment and remote link; print counts
#   --dry-run   print the plan only; no network call at all
#   --reset     delete only issues carrying the label geeratya-seed (and, on GCS/GTS, the sprints named
#               "Seed Sprint ..."); lists them unless --yes is also given.
#               The dev account may lack delete permission (issue #19): failures are reported, not fatal.
#   --project   limit to one project
# A re-run also moves an issue still in To Do into its column (failed creation-time transition), waits
# Retry-After on 429 and prints the Jira error body when a request fails.
# GCS/GTS (Scrum) get epics, stories, tasks, bugs and sub-tasks with story points on the board's estimation
# field (read from the board configuration: customfield_10047 on GCS, customfield_10016 on GTS) and three
# sprints: "Seed Sprint 1" (closed; two issues not finished and carried over), "Seed Sprint 2" (active) and
# "Seed Sprint 3" (future), plus issues left in the backlog. Sprints are found by name, so a re-run adds nothing.
# Every seed issue carries the label `geeratya-seed` plus a unique `geeratya-seed-<project>-<slug>`;
# the slug label is how an existing issue is found, so a second run creates nothing new.
# All HTTP goes through scripts/record-fixture.sh, which refuses hosts outside GEERATYA_LIVE_HOSTS,
# refuses writes outside GCM/GTM/GCS/GTS (sprint writes only on boards 5 and 6), loads the env file itself and never prints the token.
# Needs bash, curl, jq and python3. Ranking follows creation order (new issues rank last).
# The final counts wait for the search index to catch up; a re-run seconds after a run may still miss
# issues the index has not shown yet, so give it about 30 s after an interrupted run.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
rec="$here/record-fixture.sh"
dry=0 reset=0 yes=0 only=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) dry=1 ;;
    --reset) reset=1 ;;
    --yes) yes=1 ;;
    --project) if [ $# -lt 2 ]; then echo "--project needs GCM, GTM, GCS or GTS" >&2; exit 2; fi; shift; only=$1 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done
projects=(GCM GTM GCS GTS)
if [ -n "$only" ]; then
  case "$only" in GCM | GTM | GCS | GTS) projects=("$only") ;; *) echo "project must be GCM, GTM, GCS or GTS" >&2; exit 2 ;; esac
fi
is_scrum() { case "$1" in GCS | GTS) return 0 ;; *) return 1 ;; esac; }

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
# Scrum projects: slug|type|summary|parent slug|target column|story points|sprints (1, 2, 3, "1 2" = carried over, - = none)
# Sprint 1 is completed, so its unfinished issues (story-search, task-tests) are carried into sprint 2.
scrum_spec='epic-platform|Epic|Seed: Platform||To Do|-|-
epic-insights|Epic|Seed: Insights||In Progress|-|-
story-login|Story|Log in with SSO|epic-platform|Done|5|1
story-profile|Story|Edit the profile page|epic-platform|Done|3|1
task-ci|Task|Set up the CI pipeline|epic-platform|Done|2|1
bug-session|Bug|Session expires too early|epic-platform|Done|3|1
sub-login-form|Sub|Build the login form|story-login|Done|1|1
story-search|Story|Search across projects|epic-insights|In Progress|8|1 2
task-tests|Task|Add end-to-end tests|epic-platform|To Do|5|1 2
story-notify|Story|Email notifications|epic-insights|In Progress|5|2
story-settings|Story|Settings screen|epic-platform|To Do|3|2
bug-overflow|Bug|Table overflows on narrow screens|epic-insights|Done|2|2
task-lint|Task|Enable the linter|epic-platform|Done|1|2
sub-notify-ui|Sub|Notification preferences UI|story-notify|In Progress|2|2
story-export|Story|Export a report as PDF|epic-insights|To Do|8|3
task-docs|Task|Write the release notes|epic-platform|To Do|3|3
bug-timeout|Bug|Report times out on large projects|epic-insights|To Do|2|3
backlog-1|Task|Backlog: dark mode||To Do|5|-
backlog-2|Story|Backlog: keyboard shortcuts help||To Do|3|-
backlog-3|Bug|Backlog: tooltip clipped||To Do|-|-
backlog-4|Task|Backlog: update dependencies||To Do|-|-'
# Links: slug|link type name|other slug (the first issue is posted as outwardIssue, so it reads the inward wording: story-signup "is blocked by" story-welcome; kept so the recorded fixtures stay the same)
links='story-signup|Blocks|story-welcome
bug-crash|Relates|story-signup'

if [ "$dry" = 1 ]; then
  for p in "${projects[@]}"; do
    echo "project $p"
    if is_scrum "$p"; then
      while IFS='|' read -r slug type summary parent col pts sp; do
        printf '  %-15s %-5s %-36s parent=%-15s column=%-11s points=%-2s sprint=%s\n' "$slug" "$type" "$summary" "${parent:--}" "$col" "$pts" "$sp"
      done <<< "$scrum_spec"
      echo "  sprints: Seed Sprint 1 (closed, carries story-search and task-tests), Seed Sprint 2 (active), Seed Sprint 3 (future)"
      continue
    fi
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
# Retries on 429 up to 3 times, waiting Retry-After seconds (read through RECORD_META; capped at 60,
# 5 s times the attempt when the header is missing or not a number). On failure prints the status
# and the start of the Jira error body (errorMessages/errors, else the raw text), never the token.
api() {
  local tries=0 status delay detail
  while :; do
    RECORD_RAW=1 RECORD_META="$work/meta" "$rec" "$@" > "$work/out" 2> "$work/err" || true
    status=$(sed -n 's/^status: //p' "$work/err" | tail -n1)
    case "$status" in
      2??) cat "$work/out"; return 0 ;;
      429)
        tries=$((tries + 1))
        if [ $tries -gt 3 ]; then break; fi
        delay=$(sed -n 's/^[Rr]etry-[Aa]fter: *\([0-9][0-9]*\).*/\1/p' "$work/meta" 2> /dev/null | head -n1 || true)
        delay=${delay:-$((tries * 5))}
        if [ "$delay" -gt 60 ]; then delay=60; fi
        sleep "$delay" ;;
      *) break ;;
    esac
  done
  detail=$(jq -r '[(.errorMessages // [])[], ((.errors // {}) | to_entries[] | "\(.key): \(.value)")] | join("; ")' "$work/out" 2> /dev/null || true)
  if [ -z "$detail" ]; then detail=$(head -c 300 "$work/out" | tr '\n' ' '); fi
  echo "request failed: $1 ${2%%\?*} -> status ${status:-none} $(grep -v '^status:' "$work/err" | head -n2) ${detail:0:300}" >&2
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
declare -A statusof # "<project>/<slug>" -> current status name, from the search
declare -A pointsof # "<project>/<slug>" -> current story points ("-" when empty), from the search
pf="" # estimation field id of the project being handled (Scrum only), from the board configuration

search_seed() { # project -> $work/res.json
  jq -n --arg j "project = $1 AND labels = geeratya-seed" --arg pf "$pf" \
    '{jql:$j,maxResults:100,fields:(["labels","summary","status"] + (if $pf != "" then [$pf] else [] end))}' > "$work/q.json"
  api POST /rest/api/3/search/jql "$work/q.json" > "$work/res.json"
}

# Scrum helpers. board_of PROJECT -> board id of the project's scrum board (empty when none).
board_of() {
  api GET "/rest/agile/1.0/board?projectKeyOrId=$1" | jq -r '[.values[]|select(.type=="scrum" or .type=="simple")][0].id // empty'
}
# sprints_of BOARD -> $work/sprints.json (all states)
sprints_of() { api GET "/rest/agile/1.0/board/$1/sprint?state=active,closed,future&maxResults=50" > "$work/sprints.json"; }
sprint_field() { jq -r --arg n "$1" --arg f "$2" '[.values[]|select(.name==$n)][0][$f] // empty' "$work/sprints.json"; }
# iso DAYS -> UTC timestamp DAYS from now (negative: in the past)
iso() { python3 -c 'import sys,datetime as d;print((d.datetime.now(d.timezone.utc)+d.timedelta(days=int(sys.argv[1]))).strftime("%Y-%m-%dT%H:%M:%S.000Z"))' "$1"; }

if [ "$reset" = 1 ]; then
  fail=0
  for p in "${projects[@]}"; do
    search_seed "$p"
    echo "$p: $(jq '.issues|length' "$work/res.json") issue(s) labelled geeratya-seed"
    if [ "$yes" != 1 ]; then
      jq -r '.issues[]|"  "+.key+" "+.fields.summary' "$work/res.json"
      if is_scrum "$p"; then
        sb=$(board_of "$p")
        if [ -n "$sb" ]; then sprints_of "$sb"; fi
        jq -r '.values[]|select(.name|startswith("Seed Sprint "))|"  sprint "+.name+" ("+.state+")"' "$work/sprints.json"
      fi
      continue
    fi
    echo "Jira has no undo: deleting."
    # Highest keys first: sub-tasks were created after their parents.
    ok=0 bad=0
    for key in $(jq -r --arg p "$p" '.issues|map(.key|ltrimstr($p+"-")|tonumber)|sort|reverse|.[]|$p+"-"+tostring' "$work/res.json"); do
      if write DELETE "/rest/api/3/issue/$key" > /dev/null; then ok=$((ok + 1)); echo "  deleted $key"; else bad=$((bad + 1)); fi
    done
    echo "$p: $ok deleted, $bad failed"
    if [ "$bad" -gt 0 ]; then fail=1; fi
    if is_scrum "$p"; then
      sb=$(board_of "$p")
      if [ -z "$sb" ]; then echo "no scrum board for $p" >&2; fail=1; continue; fi
      sprints_of "$sb"
      for sid in $(jq -r '.values[]|select(.name|startswith("Seed Sprint "))|.id' "$work/sprints.json"); do
        if write DELETE "/rest/agile/1.0/sprint/$sid" > /dev/null; then echo "  deleted sprint $sid"; else fail=1; fi
      done
    fi
  done
  [ "$yes" = 1 ] || echo "nothing deleted; pass --yes to delete the issues above"
  exit "$fail"
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

# Story points on the board's estimation field (uses $pf), set whenever the current value differs.
ensure_points() { # project slug points
  local k="$1/$2" key
  key=${keyof[$k]}
  if [ "$3" = "-" ] || [ "${pointsof[$k]:-}" = "$3" ]; then return 0; fi
  # A company-managed project's Story Points field is on the screens of Stories only: Jira would drop the
  # value of a task, bug or sub-task silently, so those keep no estimate there (editmeta says which).
  editmeta=$(api GET "/rest/api/3/issue/$key/editmeta") || return 1
  if [ "$(jq --arg f "$pf" '.fields|has($f)' <<< "$editmeta")" != true ]; then
    echo "  points  $key: $pf is not on its edit screen, no estimate set"
    return 0
  fi
  jq -n --arg f "$pf" --argjson v "$3" '{fields:{($f):$v}}' > "$work/pt.json"
  write PUT "/rest/api/3/issue/$key" "$work/pt.json" > /dev/null
  pointsof[$k]=$3
  echo "  points  $key = $3"
}

# add_to_sprint PROJECT SPRINT-ID SPRINT-NUMBER: every spec issue whose sprint list names the number.
add_to_sprint() {
  local p=$1 sid=$2 n=$3 keys
  keys=$(while IFS='|' read -r slug _ _ _ _ _ sp; do
    for x in $sp; do if [ "$x" = "$n" ]; then echo "${keyof[$p/$slug]}"; fi; done
  done <<< "$scrum_spec" | jq -R . | jq -sc '.')
  jq -n --argjson k "$keys" '{issues:$k}' > "$work/sp.json"
  write POST "/rest/agile/1.0/sprint/$sid/issue" "$work/sp.json" > /dev/null
  echo "  sprint $sid: $(jq length <<< "$keys") issue(s)"
}

# find_or_create_sprint BOARD NAME -> sets sid and sstate (from $work/sprints.json, refreshed after a create)
find_or_create_sprint() {
  sprints_of "$1"
  sid=$(sprint_field "$2" id); sstate=$(sprint_field "$2" state)
  if [ -z "$sid" ]; then
    jq -n --arg n "$2" --argjson b "$1" '{name:$n,originBoardId:$b}' > "$work/sc.json"
    sid=$(write POST /rest/agile/1.0/sprint "$work/sc.json" | jq -r .id); sstate=future
    echo "  created sprint $sid $2"
  else
    echo "  found   sprint $sid $2 ($sstate)"
  fi
}

# seed_scrum PROJECT BOARD: story points, then the three sprints, each step guarded by the sprint's state
# (future -> add issues, start; active -> complete) so an interrupted run resumes and a finished one adds nothing.
seed_scrum() {
  local p=$1 board=$2 slug type summary parent col pts sp
  while IFS='|' read -r slug type summary parent col pts sp; do ensure_points "$p" "$slug" "$pts"; done <<< "$scrum_spec"
  # Sprint 1: closed.
  find_or_create_sprint "$board" "Seed Sprint 1"
  if [ "$sstate" = future ]; then
    add_to_sprint "$p" "$sid" 1
    jq -n --arg s "$(iso -14)" --arg e "$(iso -1)" '{state:"active",startDate:$s,endDate:$e}' > "$work/su.json"
    write POST "/rest/agile/1.0/sprint/$sid" "$work/su.json" > /dev/null; sstate=active; echo "  started sprint $sid"
  fi
  if [ "$sstate" = active ]; then
    jq -n --arg s "$(iso -14)" --arg e "$(iso -1)" '{state:"closed",startDate:$s,endDate:$e}' > "$work/su.json"
    write POST "/rest/agile/1.0/sprint/$sid" "$work/su.json" > /dev/null; echo "  completed sprint $sid"
  fi
  # Sprint 2: active (gets the issues sprint 1 did not finish).
  find_or_create_sprint "$board" "Seed Sprint 2"
  if [ "$sstate" = future ]; then
    add_to_sprint "$p" "$sid" 2
    jq -n --arg s "$(iso -3)" --arg e "$(iso 11)" '{state:"active",startDate:$s,endDate:$e}' > "$work/su.json"
    write POST "/rest/agile/1.0/sprint/$sid" "$work/su.json" > /dev/null; echo "  started sprint $sid"
  fi
  # Sprint 3: future.
  find_or_create_sprint "$board" "Seed Sprint 3"
  if [ "$sstate" = future ] && [ "$(api GET "/rest/agile/1.0/sprint/$sid/issue?maxResults=1&fields=summary" | jq .total)" = 0 ]; then
    add_to_sprint "$p" "$sid" 3
  fi
}

for p in "${projects[@]}"; do
  echo "== $p"
  cnt_new[$p]=0 cnt_found[$p]=0
  board="" pf="" cur_spec=$spec
  if is_scrum "$p"; then
    cur_spec=$scrum_spec
    board=$(board_of "$p")
    if [ -z "$board" ]; then echo "no scrum board for $p" >&2; exit 1; fi
    pf=$(api GET "/rest/agile/1.0/board/$board/configuration" | jq -r '.estimation.field.fieldId // empty')
    if [ -z "$pf" ]; then echo "board $board has no estimation field" >&2; exit 1; fi
    echo "  board $board, story points field $pf"
  fi
  search_seed "$p"
  while IFS=$'\t' read -r key lab st pts; do
    keyof["$p/${lab#geeratya-seed-"$p"-}"]=$key
    statusof["$p/${lab#geeratya-seed-"$p"-}"]=$st
    pointsof["$p/${lab#geeratya-seed-"$p"-}"]=$pts
  done < <(jq -r --arg p "$p" --arg pf "$pf" '.issues[]|[.key, (.fields.labels[]|select(startswith("geeratya-seed-"+$p+"-"))), .fields.status.name, (if $pf == "" then "-" else (.fields[$pf] // "-" | tostring | sub("\\.0$"; "")) end)]|@tsv' "$work/res.json")
  api GET "/rest/api/3/issue/createmeta/$p/issuetypes" > "$work/types.json"
  epic_t=$(jq -r '[.issueTypes[]|select(.hierarchyLevel==1)][0].id' "$work/types.json")
  sub_t=$(jq -r '[.issueTypes[]|select(.subtask==true)][0].id' "$work/types.json")
  while IFS='|' read -r slug type summary parent col pts sp; do
    k="$p/$slug"
    if [ -n "${keyof[$k]:-}" ]; then
      cnt_found[$p]=$((cnt_found[$p] + 1))
      echo "  found   ${keyof[$k]} $slug"
      # Repair an issue whose transition failed when it was created: still in the initial status
      # (To Do) but meant for another column. Issues moved on purpose are left alone.
      if [ "$col" != Backlog ] && [ "$col" != "To Do" ] && [ "${statusof[$k]:-}" = "To Do" ]; then
        echo "  repair  ${keyof[$k]} ${statusof[$k]} -> $col"
        transition_to "${keyof[$k]}" "$col" || true
      fi
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
  done <<< "$cur_spec"

  if is_scrum "$p"; then
    seed_scrum "$p" "$board"
    continue
  fi

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
  # The search index lags: wait (up to ~30 s) until it shows every issue this run created or found.
  want=$((cnt_new[$p] + cnt_found[$p]))
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    search_seed "$p"
    if [ "$(jq '.issues|length' "$work/res.json")" -ge "$want" ]; then break; fi
    sleep 3
  done
  echo "$p: $(jq '.issues|length' "$work/res.json") total (${cnt_new[$p]} created, ${cnt_found[$p]} found this run)"
  jq -r '[.issues[].fields.status.name]|group_by(.)|map("    "+.[0]+": "+(length|tostring))|.[]' "$work/res.json"
  if is_scrum "$p"; then
    sprints_of "$(board_of "$p")"
    jq -r '[.values[]|select(.name|startswith("Seed Sprint "))]|map("    sprint "+.name+": "+.state)|.[]' "$work/sprints.json"
  fi
done
