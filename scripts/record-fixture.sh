#!/usr/bin/env bash
# Record a Jira response from the dev site as a scrubbed fixture.
# Usage: scripts/record-fixture.sh <method> <path> [body-file]
#   env RECORD_OUT=<file>   write the scrubbed body there (default: stdout)
#   env RECORD_META=<file>  write the status line and rate-limit headers there (scrubbed)
#   env RECORD_CONTENT_TYPE=<type>  request Content-Type (default application/json)
#   env RECORD_XSRF=1       send X-Atlassian-Token: no-check (attachment upload)
#   env RECORD_RAW=1        skip scrubbing (inspection only, never commit the output)
# Writes also allowed: agile rank/backlog moves and creating issue links, only between GCM/GTM keys (no link deletes).
# Also allowed: PUT and DELETE of one comment by id on a GCM/GTM issue.
# Also allowed: PUT /assignee on a GCM/GTM issue (record the 400 for an unassignable account; use an issue the run created).
# Refuses hosts outside GEERATYA_LIVE_HOSTS. Never prints the token; the credential
# goes to curl through a private config file, not argv.
# Scrubbing (scripts/scrub-fixture.py): host -> example.atlassian.net, account ids -> fake-account-N,
# emails -> userN@example.com, display names -> Test User N, avatar URLs dropped.
set -euo pipefail

if [ $# -lt 2 ]; then
  echo "usage: $0 <method> <path> [body-file]" >&2
  exit 2
fi
method=$1
path=$2
body=${3:-}

env_file=${GEERATYA_LIVE_ENV:-$HOME/.config/geeratya/dev-site.env}
# Load the env file for variables that are not set yet; explicit values win.
if [ -f "$env_file" ]; then
  saved=$(declare -p GEERATYA_LIVE_HOSTS GEERATYA_LIVE_EMAIL GEERATYA_LIVE_TOKEN 2> /dev/null || true)
  # shellcheck disable=SC1090
  . "$env_file"
  eval "$saved"
fi
for v in GEERATYA_LIVE_HOSTS GEERATYA_LIVE_EMAIL GEERATYA_LIVE_TOKEN; do
  if [ -z "${!v:-}" ]; then echo "$v is not set" >&2; exit 2; fi
done

site=${GEERATYA_LIVE_SITE:-${GEERATYA_LIVE_HOSTS%%,*}}
site=${site#https://}
site=${site%/}

allowed=0
IFS=',' read -ra hosts <<< "$GEERATYA_LIVE_HOSTS"
for h in "${hosts[@]}"; do
  h=${h//[[:space:]]/}
  h=${h#https://}
  h=${h%/}
  if [ "$h" = "$site" ]; then allowed=1; fi
done
if [ "$allowed" -ne 1 ]; then echo "host not in GEERATYA_LIVE_HOSTS: refused" >&2; exit 3; fi

case "$path" in /*) ;; *) echo "path must start with /" >&2; exit 2 ;; esac
case "$method" in GET | POST | PUT | DELETE) ;; *) echo "unsupported method" >&2; exit 2 ;; esac

# Writes: only issues in the sandbox projects GCM and GTM (testing skill, "Live dev site").
if [ "$method" != GET ]; then
  # Exact shapes only: no "/" beyond one sub-resource, no dot segments.
  issue_re='^/rest/api/3/issue/G[CT]M-[0-9]+(/(comment(/[0-9]+)?|transitions|remotelink|attachments|assignee))?(\?[A-Za-z0-9=&,._-]*)?$'
  case "$path" in
    /rest/api/3/issue | /rest/api/3/search/jql | /rest/api/3/search/approximate-count) ;;
    # Ranking, moving to the backlog and issue links name issues in the body; checked below.
    /rest/agile/1.0/issue/rank | /rest/agile/1.0/backlog/issue | /rest/api/3/issueLink) ;;
    *)
      if ! [[ $path =~ $issue_re ]]; then
        echo "write refused: only issues in GCM/GTM (and search POST) may be written" >&2; exit 3
      fi ;;
  esac
  if [ "$method" = DELETE ] && [ "$path" = /rest/api/3/issueLink ]; then echo "write refused" >&2; exit 3; fi
  if [ -n "$body" ] && [ "${RECORD_CONTENT_TYPE:-application/json}" = application/json ] \
    && ! jq -e '((.fields | objects | .project.key) // "GCM") | IN("GCM","GTM")' "$body" > /dev/null 2>&1; then
    echo "write refused: body names a project other than GCM/GTM" >&2; exit 3
  fi
fi

# Bodies that name issues: every key must be in GCM/GTM.
case "$path" in
  /rest/agile/1.0/issue/rank | /rest/agile/1.0/backlog/issue | /rest/api/3/issueLink)
    case "$path" in
      /rest/api/3/issueLink)
        jq_check='(.inwardIssue.key | type == "string" and test("^G[CT]M-[0-9]+$")) and (.outwardIssue.key | type == "string" and test("^G[CT]M-[0-9]+$")) and (.inwardIssue.id == null) and (.outwardIssue.id == null)' ;;
      *)
        jq_check='(.issues | type == "array" and length > 0 and all(type == "string" and test("^G[CT]M-[0-9]+$"))) and ([.rankBeforeIssue, .rankAfterIssue] | all(. == null or (type == "string" and test("^G[CT]M-[0-9]+$"))))' ;;
    esac
    if [ -z "$body" ] || ! jq -e "$jq_check" "$body" > /dev/null 2>&1; then
      echo "write refused: body must name only GCM/GTM issue keys" >&2; exit 3
    fi ;;
esac

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
umask 077
printf 'user = "%s:%s"\n' "$GEERATYA_LIVE_EMAIL" "$GEERATYA_LIVE_TOKEN" > "$tmp/auth"

args=(-sS -X "$method" -D "$tmp/headers" -o "$tmp/body" -w '%{http_code}'
  -H 'Accept: application/json' --config "$tmp/auth")
if [ "${RECORD_XSRF:-}" = 1 ]; then args+=(-H 'X-Atlassian-Token: no-check'); fi
if [ -n "$body" ]; then
  args+=(-H "Content-Type: ${RECORD_CONTENT_TYPE:-application/json}" --data-binary "@$body")
fi
status=$(curl "${args[@]}" "https://$site$path")

scrub() {
  if [ "${RECORD_RAW:-}" = 1 ]; then cat; return; fi
  python3 -I "$(dirname "$0")/scrub-fixture.py" "$site"
}

if [ -n "${RECORD_META:-}" ]; then
  {
    head -n1 "$tmp/headers" | tr -d '\r'
    grep -iE '^(retry-after|x-ratelimit[a-z-]*|ratelimit[a-z-]*|beta-retry-after|www-authenticate|content-type):' "$tmp/headers" | tr -d '\r' || true
  } | sed "s/$site/example.atlassian.net/g" > "$RECORD_META"
fi

echo "status: $status" >&2
if [ -s "$tmp/body" ]; then
  if jq -e . "$tmp/body" > /dev/null 2>&1; then
    out=$(scrub < "$tmp/body")
  else
    out=$(sed -E "s/$site/example.atlassian.net/g; s/(Log reference|TraceId)(: <strong>)[^<]*/\1\2fake-id/g" "$tmp/body")
  fi
  # Fail closed: nothing that still looks like site data or a credential is written.
  if [ "${RECORD_RAW:-}" != 1 ]; then
    if printf '%s' "$out" | grep -Eio '[a-z0-9-]+\.atlassian\.net' | grep -iv '^example\.atlassian\.net$' | grep -q . \
      || printf '%s' "$out" | grep -Fq -e "$GEERATYA_LIVE_TOKEN" -e "$GEERATYA_LIVE_EMAIL"; then
      echo "scrub check failed: output still contains site or credential data; nothing written" >&2
      exit 4
    fi
  fi
  if [ -n "${RECORD_OUT:-}" ]; then printf '%s\n' "$out" > "$RECORD_OUT"; else printf '%s\n' "$out"; fi
fi
