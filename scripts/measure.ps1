#requires -Version 7
# Measure cold start to first frame and key-to-frame of the real binary in a private psmux server (Windows;
# PowerShell 7 and psmux: `winget install marlocarlo.psmux`, which accepts the tmux commands used here).
# PowerShell twin of the default scenario of measure.sh (verified on Windows 11 with psmux 3.4, #632). The scenarios
# list, detail, views and scrum are not ported (they exit 2); use measure.sh on Linux for them.
# Usage: scripts/measure.ps1 <binary> <fake url> [runs] [-Keys N] [-TimeoutMs N]
#   <binary>    the Native AOT geeratya binary (never dotnet run: JIT numbers differ)
#   <fake url>  loopback base URL printed by tools/FakeJira; written as the site of one active fake profile into
#               a private configuration directory (token: GEERATYA_TOKEN=fake)
# Prints median and p95 in milliseconds. A psmux poll costs far more than tmux's few ms and psmux starts the pane
# command through pwsh (the "floor" line shows that cost), so the numbers are only comparable between runs of this
# script, never with measure.sh. Private XDG directories keep the owner's profiles, tokens and cache unread, and
# psmux reads an empty configuration file (`-f`), never the user's. Exit 2 for bad arguments, 3 for a missing tool,
# 1 when a frame never appears.
param(
    [Parameter(Position = 0)][string]$Binary,
    [Parameter(Position = 1)][string]$FakeUrl,
    [Parameter(Position = 2)][int]$Runs = 20,
    [int]$Keys = 5,
    [int]$TimeoutMs = 5000)

$ErrorActionPreference = 'Stop'
function Write-Err([string]$text) { [Console]::Error.WriteLine($text) }

if (-not $Binary -or -not $FakeUrl) { Write-Err 'usage: measure.ps1 <binary> <fake url> [runs] [-Keys N] [-TimeoutMs N]'; exit 2 }
if ($env:SCENARIO) { Write-Err "SCENARIO=$($env:SCENARIO) is not ported to measure.ps1; use measure.sh"; exit 2 }
if ($FakeUrl -notmatch '^http://(127\.0\.0\.1|localhost):') { Write-Err 'the fake url must be a loopback address'; exit 2 }
if (-not (Test-Path -PathType Leaf $Binary)) { Write-Err "binary not found: $Binary"; exit 2 }
if (-not (Get-Command psmux -ErrorAction SilentlyContinue)) { Write-Err "missing tool 'psmux': winget install marlocarlo.psmux"; exit 3 }
$Binary = (Resolve-Path $Binary).Path

$savedEnv = @{}
foreach ($name in 'XDG_CONFIG_HOME', 'XDG_STATE_HOME', 'XDG_CACHE_HOME', 'GEERATYA_TOKEN', 'GEERATYA_USE_XDG') {
    $savedEnv[$name] = [Environment]::GetEnvironmentVariable($name)
}
$work = Join-Path ([System.IO.Path]::GetTempPath()) "geeratya-measure-$PID"
$baseSocket = "geeratya-measure-$PID"
$script:socket = $baseSocket
function T { psmux -L $script:socket -f $script:emptyConfig @args }

$clock = [System.Diagnostics.Stopwatch]::StartNew()
function Get-Now { $clock.Elapsed.TotalMilliseconds }
function Get-Frame { (T capture-pane -t m -p 2>$null) -join "`n" }

# Polls until the frame differs from $Before (empty: until it has any text); returns the elapsed ms.
function Wait-Frame([string]$Before) {
    $start = Get-Now
    while ($true) {
        $current = Get-Frame
        if ($current -ne $Before -and $current -match '\S') { return (Get-Now) - $start }
        if ((Get-Now) - $start -gt $TimeoutMs) { Write-Err 'timed out waiting for a frame'; exit 1 }
    }
}

# Median and p95 like summarise in measure.sh.
function Get-Summary([double[]]$Values) {
    if ($Values.Count -eq 0) { return 'n/a', 'n/a' }
    $sorted = @($Values | Sort-Object)
    $median = $sorted[[int][Math]::Floor(($sorted.Count + 1) / 2) - 1]
    $p95 = $sorted[[Math]::Min($sorted.Count, [int][Math]::Ceiling($sorted.Count * 0.95)) - 1]
    $median.ToString('0', [cultureinfo]::InvariantCulture), $p95.ToString('0', [cultureinfo]::InvariantCulture)
}

function Start-Session([string]$Name, [string]$Command) {
    T kill-server 2>$null | Out-Null
    $script:socket = "$baseSocket-$Name"
    T new-session -d -s m -x 120 -y 40 $Command | Out-Null
    Wait-Frame ''
}

try {
    $config = Join-Path $work 'config'
    New-Item -ItemType Directory -Force (Join-Path $config 'geeratya'), (Join-Path $work 'state'), (Join-Path $work 'cache') | Out-Null
    $script:emptyConfig = Join-Path $work 'psmux.conf'
    New-Item -ItemType File -Force $script:emptyConfig | Out-Null
    $env:XDG_CONFIG_HOME = $config
    $env:XDG_STATE_HOME = Join-Path $work 'state'
    $env:XDG_CACHE_HOME = Join-Path $work 'cache'
    $env:GEERATYA_TOKEN = 'fake'
    $env:GEERATYA_USE_XDG = '1' # Windows ignores XDG_*_HOME without it and would read the real profile
    $profiles = [ordered]@{ activeName = 'fake'; profiles = @([ordered]@{ name = 'fake'; siteUrl = $FakeUrl; email = 'fake@example.com' }) }
    [System.IO.File]::WriteAllText((Join-Path $config 'geeratya/profiles.json'), ($profiles | ConvertTo-Json -Compress -Depth 4), [System.Text.UTF8Encoding]::new($false))

    # Quoted so a path with spaces works (checked on Windows: psmux starts the quoted binary, not a pwsh string).
    $appCommand = "`"$Binary`""
    $floor = @(); $cold = @(); $keyed = @()
    # The floor: a command that prints one character and then waits, so the numbers can be read net of psmux, its pwsh and the polling.
    for ($run = 1; $run -le $Runs; $run++) { $floor += Start-Session "f$run" 'Write-Host x; Start-Sleep 30' }
    for ($run = 1; $run -le $Runs; $run++) {
        $cold += Start-Session "c$run" $appCommand
        for ($press = 1; $press -le $Keys; $press++) {
            $before = Get-Frame
            T send-keys -t m '?' | Out-Null
            $keyed += Wait-Frame $before
        }
    }
    $f = Get-Summary $floor; $c = Get-Summary $cold; $k = Get-Summary $keyed
    "binary:        $Binary"
    "runs:          $Runs sessions, $Keys key presses each"
    "cores:         $([Environment]::ProcessorCount); results on a busy machine are pessimistic"
    "psmux floor, first frame of a trivial command (ms): median $($f[0]), p95 $($f[1])"
    "cold start to first frame (ms):                     median $($c[0]), p95 $($c[1])"
    "key to frame (ms):                                  median $($k[0]), p95 $($k[1])"
}
finally {
    T kill-server 2>$null | Out-Null
    foreach ($name in $savedEnv.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnv[$name]) }
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
