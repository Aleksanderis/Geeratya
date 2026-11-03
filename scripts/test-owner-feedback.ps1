# Windows variant of test-owner-feedback.sh: same cases against the real `def agent` line of owner-feedback.sh (needs jq on PATH).
# Usage: scripts/test-owner-feedback.ps1   (exit code 1 on failure)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root '.claude/skills/work-orchestrator/scripts/owner-feedback.sh'
$m = [regex]::Match((Get-Content -Raw $script), 'def agent:.*?;')
if (-not $m.Success) { Write-Host "FAIL: 'def agent' line not found in owner-feedback.sh"; exit 1 }
$def = $m.Value
$fail = $false

function Test-Case([string]$Name, [string]$Body, [string]$Want) {
    $got = (& jq -n --arg body $Body "$def `$body | agent").Trim()
    if ($got -ne $Want) { Write-Host "FAIL: $Name (got $got, want $Want)"; $script:fail = $true }
    else { Write-Host "ok: $Name" }
}

Test-Case 'html marker' '<!-- agent: reviewer -->' 'true'
Test-Case 'marker inside body' "### Findings`n<!-- agent: implementer -->`n`nDone." 'true'
Test-Case 'signature line' "Fixed.`n`n---`n<sub>Implementer agent · Claude · PR #1 @ abc</sub>" 'true'
Test-Case 'pr footer' "Closes #1`n`nOpened by orchestrator agent" 'true'
Test-Case 'agent line after newline' "Fixed.`n`nAgent: implementer" 'true'
Test-Case 'agent line at start' "Agent: orchestrator`n`nDone." 'true'
Test-Case 'bold agent line at start' '**Agent: implementer**' 'true'
Test-Case 'leading blank lines' "`n`nAgent: x" 'true'
Test-Case 'owner text' 'looks good, agent: nope inline' 'false'
Test-Case 'owner plain sentence' 'Please make the agent faster' 'false'
if ($fail) { exit 1 }
