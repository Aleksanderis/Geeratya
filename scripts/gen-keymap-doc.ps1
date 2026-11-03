# Regenerates docs/keymap.md from src/Geeratya.Core/Keymap/keymap.default.json.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path (Join-Path $root 'docs') 'keymap.md'
$project = Join-Path (Join-Path $root 'src') 'Geeratya.Tui'
$temporary = New-TemporaryFile

try {
    $output = dotnet run --project $project -c Release -v q -- --print-keymap-doc
    if ($LASTEXITCODE -ne 0) { throw 'keymap generation failed' }
    [IO.File]::WriteAllText($temporary, (($output -join "`n") + "`n"))
    Move-Item -Force -Path $temporary -Destination $target
    Write-Host "Wrote $target"
}
finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $temporary
}
