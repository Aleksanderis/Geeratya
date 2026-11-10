# Installs or updates Geeratya from a GitHub release (#860).
#
#   irm https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.ps1 | iex
#
# Windows (x64, arm64), Windows PowerShell 5.1 and pwsh. Downloads the release zip geeratya-<version>-win-<arch>.zip,
# checks it against the release's SHA256SUMS, puts geeratya.exe in %LOCALAPPDATA%\Geeratya and adds that folder to your
# user PATH. No admin rights, no telemetry (the only requests go to GitHub). Running it again updates; it does nothing
# when the installed version is already the wanted one. To pin a version in the one-liner:
#   $env:GEERATYA_VERSION = '1.2.3'; irm https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.ps1 | iex
# Uninstall: delete the install folder and remove it from your user PATH (the script prints both).
# Later updates can also run `geeratya update` (the script leaves a geeratya.install-source marker next to the exe).
# Linux and macOS: use install.sh.
#
# Environment (all optional):
#   GEERATYA_VERSION       install this version (1.2.3 or v1.2.3) instead of the latest
#   GEERATYA_INSTALL_DIR   install folder (default: %LOCALAPPDATA%\Geeratya)
#   GEERATYA_FORCE         1 = install even when that version is already installed
#   GEERATYA_NO_PATH       1 = do not touch the user PATH
#   GEERATYA_RELEASES_URL  release API base (default: the GitHub API's releases of the repository; "latest" and
#                          "tags/v<version>" are appended; a local folder path works, for tests)
#   GEERATYA_ARCH          x64 | arm64 (default: detected; for tests)
#   GEERATYA_RETRY_DELAY   seconds before the first download retry, doubled each time (default 2)
#
# Everything is in functions and one call on the last line, so a cut-off download runs nothing.
function Get-GeeratyaFile([string]$Source, [string]$Destination) {
    # A URL is downloaded; anything else is a local path (tests).
    if ($Source -notmatch '^https?://') {
        Copy-Item -LiteralPath $Source -Destination $Destination
        return
    }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    # GitHub's servers now and then answer 502/503/504 (#872): try up to four times, waiting 2, 4 and 8 seconds.
    # Other 4xx answers (not 408 or 429) will not get better, so they fail at once.
    $attempts = 4
    $delay = if ($env:GEERATYA_RETRY_DELAY) { [int]$env:GEERATYA_RETRY_DELAY } else { 2 }
    $previous = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        for ($attempt = 1; ; $attempt++) {
            try {
                Invoke-WebRequest -UseBasicParsing -Uri $Source -OutFile $Destination
                return
            } catch {
                $status = $null
                if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
                $transient = (-not $status) -or $status -ge 500 -or $status -eq 408 -or $status -eq 429
                $reason = if ($status) { "HTTP $status" } else { $_.Exception.Message }
                if (-not $transient -or $attempt -ge $attempts) {
                    $tries = if ($attempt -gt 1) { " after $attempt attempts" } else { '' }
                    $again = if ($transient) { ' This is usually a temporary problem on GitHub''s side: run the command again in a minute.' } else { '' }
                    throw "download failed$tries ($reason): $Source.$again"
                }
                $wait = $delay * [math]::Pow(2, $attempt - 1)
                Write-Host "Download failed ($reason), retrying in $wait s (attempt $($attempt + 1) of $attempts)..."
                if ($wait -gt 0) { Start-Sleep -Seconds $wait }
            }
        }
    } finally { $ProgressPreference = $previous }
}

# Adds a folder to the user PATH without changing how the existing value is stored: read it unexpanded (it may hold
# %VARIABLES%) and write it back as an expandable string, which Environment.SetEnvironmentVariable would not do.
function Add-ToUserPath([string]$Folder) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        $current = [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $wanted = $Folder.TrimEnd('\')
        $present = $current -split ';' | Where-Object { $_ } |
            Where-Object { [Environment]::ExpandEnvironmentVariables($_).TrimEnd('\') -ieq $wanted }
        if ($present) { return }
        $value = if ($current) { "$($current.TrimEnd(';'));$Folder" } else { $Folder }
        $key.SetValue('Path', $value, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    } finally { $key.Dispose() }
    # Writing the registry directly sends no change notice; setting and removing a variable does, so new terminals see the PATH.
    [Environment]::SetEnvironmentVariable('GEERATYA_PATH_REFRESH', '1', 'User')
    [Environment]::SetEnvironmentVariable('GEERATYA_PATH_REFRESH', $null, 'User')
    $env:Path = "$env:Path;$Folder"
    Write-Host "Added $Folder to your user PATH (open a new terminal for other windows to see it)."
}

function Install-Geeratya {
    # Under `irm | iex` this runs in the caller's session: set the preference here and put it back afterwards.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Stop'
    try { Invoke-GeeratyaInstall } finally { $ErrorActionPreference = $previousPreference }
}

function Invoke-GeeratyaInstall {
    $repository = 'Aleksanderis/Geeratya'
    $isWindowsHost = ($PSVersionTable.PSVersion.Major -lt 6) -or $IsWindows
    if (-not $isWindowsHost -and -not $env:GEERATYA_ARCH) { throw 'unsupported system: this script is for Windows (Linux and macOS: use install.sh)' }
    $base = if ($env:GEERATYA_RELEASES_URL) { $env:GEERATYA_RELEASES_URL.TrimEnd('/', '\') } else { "https://api.github.com/repos/$repository/releases" }
    $separator = if ($base -match '^https?://') { '/' } else { [string][IO.Path]::DirectorySeparatorChar }
    if ($env:GEERATYA_VERSION) {
        $releaseUrl = "$base${separator}tags${separator}v$($env:GEERATYA_VERSION.TrimStart('v'))"
    } else {
        $releaseUrl = "$base${separator}latest"
    }
    $architecture = if ($env:GEERATYA_ARCH) { $env:GEERATYA_ARCH } else {
        switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
            'X64' { 'x64' }
            'Arm64' { 'arm64' }
            default { throw "unsupported architecture: $_ (releases exist for x64 and arm64)" }
        }
    }
    if ($architecture -ne 'x64' -and $architecture -ne 'arm64') { throw "unsupported architecture: $architecture (releases exist for x64 and arm64)" }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("geeratya-install-" + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $work)
    try {
        $jsonPath = Join-Path $work 'release.json'
        try { Get-GeeratyaFile $releaseUrl $jsonPath } catch { throw "$($_.Exception.Message) (HTTP 404 means no such release; a GitHub rate limit also fails here)" }
        $release = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
        if (-not $release.tag_name) { throw "could not read the release from $releaseUrl" }
        $tag = [string]$release.tag_name
        $version = $tag.TrimStart('v')

        $target = if ($env:GEERATYA_INSTALL_DIR) { $env:GEERATYA_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'Geeratya' }
        $executable = Join-Path $target 'geeratya.exe'
        if ((Test-Path -LiteralPath $executable) -and $env:GEERATYA_FORCE -ne '1') {
            # A broken or foreign geeratya.exe must not block the update.
            try { $installed = ((& $executable --version 2>$null) -split '\s+' | Select-Object -Skip 1 -First 1) } catch { $installed = $null }
            if ($installed -eq $version) { Write-Host "Geeratya $version is already installed ($executable)."; return }
            if ($installed) { Write-Host "Updating Geeratya $installed -> $version" }
        }

        $suffix = "-win-$architecture.zip"
        $asset = @($release.assets | Where-Object { $_.browser_download_url -like "*$suffix" }) | Select-Object -First 1
        if (-not $asset) { throw "release $tag has no win-$architecture zip; see https://github.com/$repository/releases" }
        $sums = @($release.assets | Where-Object { $_.browser_download_url -like '*/SHA256SUMS' -or $_.browser_download_url -like '*\SHA256SUMS' }) | Select-Object -First 1
        if (-not $sums) { throw "release $tag has no SHA256SUMS; refusing to install an unverified download" }

        $name = ($asset.browser_download_url -split '[/\\]')[-1]
        Write-Host "Downloading $name"
        $zip = Join-Path $work $name
        Get-GeeratyaFile $asset.browser_download_url $zip
        $sumsPath = Join-Path $work 'SHA256SUMS'
        Get-GeeratyaFile $sums.browser_download_url $sumsPath
        # Lines look like "<hash>  <name>" (or "<hash> *<name>").
        $expected = $null
        foreach ($line in (Get-Content -LiteralPath $sumsPath)) {
            $parts = $line.Trim() -split '\s+', 2
            if ($parts.Count -eq 2 -and $parts[1].TrimStart('*') -eq $name) { $expected = $parts[0]; break }
        }
        if (-not $expected) { throw "SHA256SUMS has no entry for $name" }
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
        if ($expected -ne $actual) { throw "checksum mismatch for $name (expected $expected, got $actual)" }

        $unpacked = Join-Path $work 'unpacked'
        Expand-Archive -LiteralPath $zip -DestinationPath $unpacked
        $binary = Get-ChildItem -LiteralPath $unpacked -Recurse -Filter 'geeratya.exe' | Select-Object -First 1
        if (-not $binary) { throw 'the archive holds no geeratya.exe' }

        [void](New-Item -ItemType Directory -Force -Path $target)
        # A running geeratya.exe cannot be overwritten but can be renamed away, so an update works while it runs.
        $old = "$executable.old"
        Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $executable) { Move-Item -LiteralPath $executable -Destination $old -Force }
        try {
            Copy-Item -LiteralPath $binary.FullName -Destination $executable
        } catch {
            if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination $executable -Force }
            throw
        }
        Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue
        # Tells "geeratya update" that this copy is its to replace (a package manager never writes this file).
        Set-Content -LiteralPath (Join-Path $target 'geeratya.install-source') -Value 'script' -ErrorAction SilentlyContinue
        Write-Host "Installed $executable"

        if ($env:GEERATYA_NO_PATH -ne '1') { Add-ToUserPath $target }
        Write-Host "Geeratya $version installed. Run: geeratya"
        Write-Host "To uninstall: delete $target and remove it from your user PATH (your profiles and cache are kept; tokens live in the Windows credential store)."
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-Geeratya
