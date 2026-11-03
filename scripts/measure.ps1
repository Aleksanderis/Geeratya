#requires -Version 7
# Measure cold start to first frame and key-to-frame of the real binary in a private psmux server (Windows;
# PowerShell 7 and psmux: `winget install marlocarlo.psmux`, which accepts the tmux commands used here).
# PowerShell twin of the default scenario of measure.sh. UNTESTED: written without a Windows machine; the first
# Windows run is tracked by a windows-vm issue. The scenarios list, detail, views and scrum are not ported (they
# exit 2); use measure.sh on Linux for them.
# Usage: scripts/measure.ps1 <binary> <fake url> [runs] [-Keys N] [-TimeoutMs N]
#   <binary>    the Native AOT geeratya binary (never dotnet run: JIT numbers differ)
#   <fake url>  loopback base URL printed by tools/FakeJira; written as the site of one active fake profile into
#               a private configuration directory (token: GEERATYA_TOKEN=fake)
# Prints median and p95 in milliseconds. psmux polls are expected to cost far more than tmux's few ms (to be measured), so the numbers are only
# comparable between runs of this script, never with measure.sh. Private XDG directories keep the owner's profiles,
# tokens and cache unread (no `-f` equivalent for the psmux config is known: a user psmux config could still apply). Exit 2 for bad arguments, 3 for a missing tool, 1 when a frame never appears.
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
foreach ($name in 'XDG_CONFIG_HOME', 'XDG_STATE_HOME', 'XDG_CACHE_HOME', 'GEERATYA_TOKEN') {
    $savedEnv[$name] = [Environment]::GetEnvironmentVariable($name)
}
$work = Join-Path ([System.IO.Path]::GetTempPath()) "geeratya-measure-$PID"
$baseSocket = "geeratya-measure-$PID"
$script:socket = $baseSocket
function T { psmux -L $script:socket @args }

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
    $env:XDG_CONFIG_HOME = $config
    $env:XDG_STATE_HOME = Join-Path $work 'state'
    $env:XDG_CACHE_HOME = Join-Path $work 'cache'
    $env:GEERATYA_TOKEN = 'fake'
    $profiles = [ordered]@{ activeName = 'fake'; profiles = @([ordered]@{ name = 'fake'; siteUrl = $FakeUrl; email = 'fake@example.com' }) }
    [System.IO.File]::WriteAllText((Join-Path $config 'geeratya/profiles.json'), ($profiles | ConvertTo-Json -Compress -Depth 4), [System.Text.UTF8Encoding]::new($false))

    # Which shell psmux runs the command through is unverified: a path without spaces is passed bare so it works with
    # cmd and pwsh alike; with spaces it is quoted, which pwsh would print instead of run (check on the first Windows run).
    $appCommand = if ($Binary -match '\s') { "`"$Binary`"" } else { $Binary }
    $floor = @(); $cold = @(); $keyed = @()
    # The floor: a cmd.exe command that prints one character (cheap to start, unlike pwsh), so the numbers can be read net of psmux and polling cost.
    for ($run = 1; $run -le $Runs; $run++) { $floor += Start-Session "f$run" 'cmd /c "echo x& ping -n 30 127.0.0.1 >nul"' }
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
