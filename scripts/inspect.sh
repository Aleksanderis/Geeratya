#!/usr/bin/env bash
# Runs JetBrains inspectcode (the Rider inspections) over the solution and prints the warnings.
# These are quality checks (captured disposed/modified variables, unused members, ...) that .editorconfig and the
# compiler analyzers do not report. Exit code 1 when anything is found.
# Usage: inspect.sh [--changed | glob...]. No argument inspects everything (~40 s); --changed or globs (~10 s)
# limit the report to those files (--changed: files changed on the branch since origin/main, committed or not).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$(mktemp -d)/inspect.xml"

cd "$root"
include=()
if [[ "${1:-}" == "--changed" ]]; then
    base="$(git merge-base HEAD origin/main 2>/dev/null || git merge-base HEAD main 2>/dev/null || echo HEAD)"
    changed="$(git diff --name-only "$base" -- '*.cs' | paste -sd ';' -)"
    if [[ -z "$changed" ]]; then
        echo "0 issue(s)"
        exit 0
    fi
    include=(--include="$changed")
elif [[ $# -gt 0 ]]; then
    include=(--include="$(IFS=';'; echo "$*")")
fi

dotnet tool restore >/dev/null
dotnet build --nologo -v quiet >/dev/null
dotnet jb inspectcode Geeratya.slnx --output="$output" --format=Xml --severity=WARNING --no-build --verbosity=ERROR "${include[@]}" >/dev/null

python3 - "$output" <<'PY'
import sys
import xml.etree.ElementTree as ET

issues = list(ET.parse(sys.argv[1]).iter("Issue"))
for issue in issues:
    file = issue.get("File").replace("\\", "/")
    print(f"{file}:{issue.get('Line')}: {issue.get('TypeId')}: {issue.get('Message')}")
print(f"{len(issues)} issue(s)")
sys.exit(1 if issues else 0)
PY
