# Regenerates docs/keymap.md from src/Geeratya.Core/Keymap/keymap.default.json.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path (Join-Path $root 'docs') 'keymap.md'
$project = Join-Path (Join-Path $root 'src') 'Geeratya.Tui'
$temporary = New-TemporaryFile

try {
    dotnet run --project $project -c Release --nologo -v q -- --print-keymap-doc | Set-Content -Path $temporary -Encoding utf8NoBOM
    if ($LASTEXITCODE -ne 0) { throw 'keymap generation failed' }
    Move-Item -Force -Path $temporary -Destination $target
    Write-Host "Wrote $target"
}
finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $temporary
}
