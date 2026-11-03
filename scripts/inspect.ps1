#requires -Version 7
# Runs JetBrains inspectcode (the Rider inspections) over the solution and prints the warnings.
# PowerShell twin of inspect.sh (Windows; also works with PowerShell 7 on Linux and macOS): same checks, same output
# lines, exit code 1 when anything is found.
# Usage: inspect.ps1 [--changed | -Changed | glob...]. No argument inspects everything (~40 s); --changed or globs
# (~10 s) limit the report to those files (--changed: files changed on the branch since origin/main, committed or not).
# `--changed` is accepted as well as `-Changed`, so the command line matches inspect.sh.
param([switch]$Changed, [Parameter(ValueFromRemainingArguments)][string[]]$Globs)

$ErrorActionPreference = 'Stop'

if ($Globs -contains '--changed') {
    $Changed = $true
    $Globs = @($Globs | Where-Object { $_ -ne '--changed' })
}

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path ([System.IO.Path]::GetTempPath()) "geeratya-inspect-$([guid]::NewGuid()).xml"

Push-Location $root
try {
    $include = @()
    if ($Changed) {
        $base = git merge-base HEAD origin/main 2>$null
        if (-not $base) { $base = git merge-base HEAD main 2>$null }
        if (-not $base) { $base = 'HEAD' }
        $files = @(git diff --name-only $base -- '*.cs')
        if ($files.Count -eq 0) { Write-Output '0 issue(s)'; exit 0 }
        $include = @("--include=$($files -join ';')")
    }
    elseif ($Globs) {
        $include = @("--include=$($Globs -join ';')")
    }

    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
    dotnet build --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    dotnet jb inspectcode Geeratya.slnx --output=$output --format=Xml --severity=WARNING --no-build --verbosity=ERROR @include | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'inspectcode failed' }

    $issues = @(([xml](Get-Content -Raw $output)).SelectNodes('//Issue'))
    foreach ($issue in $issues) {
        Write-Output "$($issue.File -replace '\\', '/'):$($issue.Line): $($issue.TypeId): $($issue.Message)"
    }
    Write-Output "$($issues.Count) issue(s)"
    if ($issues.Count -gt 0) { exit 1 }
}
finally {
    Pop-Location
    Remove-Item -Force $output -ErrorAction SilentlyContinue
}
