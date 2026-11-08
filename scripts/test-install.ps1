# Tests scripts/install.ps1 against a fake GitHub releases API on disk (local paths, no network, no real GitHub, user PATH
# and install folder in a temp dir): install, skip when current, update, pinned version, checksum mismatch, missing
# SHA256SUMS or entry, missing asset, unsupported architecture (#860). Needs Windows PowerShell or pwsh. The fake
# geeratya.exe is a text file and cannot answer --version, so an update is the same as a first install here.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("geeratya-install-test-" + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $tmp)
function Fail([string]$Message) { throw "FAIL: $Message" }

# A fake release: <api>\tags\v<version> holds the release JSON, SHA256SUMS lists the zip (the names release.yml produces).
function New-Release([string]$Version, [string]$Api, [string]$Architecture) {
    $assets = Join-Path $Api "assets-$Version"
    [void](New-Item -ItemType Directory -Force -Path $assets, (Join-Path $Api 'tags'))
    $name = "geeratya-$Version-win-$Architecture"
    $stage = Join-Path $tmp "stage-$name"
    [void](New-Item -ItemType Directory -Force -Path (Join-Path $stage $name))
    Set-Content -LiteralPath (Join-Path $stage "$name\geeratya.exe") -Value "fake exe $Version"
    $zip = Join-Path $assets "$name.zip"
    Compress-Archive -Path (Join-Path $stage $name) -DestinationPath $zip
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $sums = Join-Path $assets 'SHA256SUMS'
    Set-Content -LiteralPath $sums -Value "$hash  $name.zip"
    @{ tag_name = "v$Version"; assets = @(@{ browser_download_url = $zip }, @{ browser_download_url = $sums }) } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Api "tags\v$Version")
}
function Set-Latest([string]$Version, [string]$Api) { Copy-Item -LiteralPath (Join-Path $Api "tags\v$Version") -Destination (Join-Path $Api 'latest') -Force }

function Invoke-Install([string]$Api, [string]$Architecture, [string]$Version = '') {
    $env:GEERATYA_RELEASES_URL = $Api
    $env:GEERATYA_INSTALL_DIR = Join-Path $tmp 'bin'
    $env:GEERATYA_NO_PATH = '1'
    $env:GEERATYA_ARCH = $Architecture
    if ($Version) { $env:GEERATYA_VERSION = $Version } else { Remove-Item Env:GEERATYA_VERSION -ErrorAction SilentlyContinue }
    & (Join-Path $root 'scripts\install.ps1') 6>&1 | Out-String
}
function Expect-Failure([scriptblock]$Action, [string]$Pattern, [string]$What) {
    $failed = $false
    try { [void](& $Action) } catch { $failed = $_.Exception.Message -like $Pattern }
    if (-not $failed) { Fail $What }
}

try {
    $exe = Join-Path $tmp 'bin\geeratya.exe'
    $api = Join-Path $tmp 'api'
    New-Release '1.2.3' $api 'x64'
    New-Release '1.2.3' $api 'arm64'
    New-Release '1.3.0' $api 'x64'
    Set-Latest '1.2.3' $api

    $out = Invoke-Install $api 'x64'
    if (-not (Test-Path -LiteralPath $exe)) { Fail "binary not installed: $out" }
    if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.2.3') { Fail 'wrong binary installed' }
    if ($out -notlike '*To uninstall*') { Fail "no uninstall hint: $out" }

    Set-Latest '1.3.0' $api
    $out = Invoke-Install $api 'x64'
    if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.3.0') { Fail "not updated: $out" }
    if (Test-Path -LiteralPath "$exe.old") { Fail 'left the old binary behind' }

    $out = Invoke-Install $api 'x64' 'v1.2.3'
    if ((Get-Content -LiteralPath $exe -Raw).Trim() -ne 'fake exe 1.2.3') { Fail "pinned version not installed: $out" }
    Expect-Failure { Invoke-Install $api 'x64' '9.9.9' } '*download failed*' 'unknown version accepted'

    Remove-Item -LiteralPath $exe
    $out = Invoke-Install $api 'arm64' '1.2.3'
    if (-not (Test-Path -LiteralPath $exe)) { Fail "arm64 not installed: $out" }
    Remove-Item -LiteralPath $exe
    Expect-Failure { Invoke-Install $api 'arm64' } '*has no win-arm64 zip*' 'missing arm64 asset accepted'

    $sums = Join-Path $api 'assets-1.2.3\SHA256SUMS'
    $good = Get-Content -LiteralPath $sums
    Set-Latest '1.2.3' $api
    Set-Content -LiteralPath $sums -Value (('0' * 64) + '  geeratya-1.2.3-win-x64.zip')
    Expect-Failure { Invoke-Install $api 'x64' } '*checksum mismatch*' 'checksum mismatch accepted'
    if (Test-Path -LiteralPath $exe) { Fail 'binary installed despite a bad checksum' }
    Set-Content -LiteralPath $sums -Value (('0' * 64) + '  geeratya-1.2.3-win-arm64.zip')
    Expect-Failure { Invoke-Install $api 'x64' } '*no entry for geeratya-1.2.3-win-x64.zip*' 'missing checksum entry accepted'
    Set-Content -LiteralPath $sums -Value $good

    $json = Get-Content -LiteralPath (Join-Path $api 'tags\v1.2.3') -Raw | ConvertFrom-Json
    @{ tag_name = $json.tag_name; assets = @($json.assets | Where-Object { $_.browser_download_url -notlike '*SHA256SUMS' }) } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $api 'latest')
    Expect-Failure { Invoke-Install $api 'x64' } '*no SHA256SUMS*' 'release without SHA256SUMS accepted'
    if (Test-Path -LiteralPath $exe) { Fail 'binary installed without a checksum file' }

    Set-Latest '1.2.3' $api
    Expect-Failure { Invoke-Install $api 'riscv64' } '*unsupported architecture*' 'unsupported architecture accepted'
    Write-Host 'ok'
} finally {
    Remove-Item Env:GEERATYA_RELEASES_URL, Env:GEERATYA_INSTALL_DIR, Env:GEERATYA_NO_PATH, Env:GEERATYA_ARCH, Env:GEERATYA_VERSION -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
