# install.ps1
# Simplerr Windows Installer
# Usage: irm https://raw.githubusercontent.com/phellams/simplerr/main/install.ps1 | iex

#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Constants ────────────────────────────────────────────────────────────────

$Repo = "phellams/simplerr"
$BinaryName = "simplerr.exe"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Simplerr"
$ConfigDir = Join-Path $env:USERPROFILE  ".phellams\simplerr"

# ── Helpers ──────────────────────────────────────────────────────────────────

function Write-Header {
    Write-Host ""
    Write-Host "  ____  _                 _             " -ForegroundColor Cyan
    Write-Host " / ___|(_)_ __ ___  _ __ | | ___ _ __  " -ForegroundColor Cyan
    Write-Host " \___ \| | '_ ' _ \| '_ \| |/ _ \ '__| " -ForegroundColor Cyan
    Write-Host "  ___) | | | | | | | |_) | |  __/ |    " -ForegroundColor Cyan
    Write-Host " |____/|_|_| |_| |_| .__/|_|\___|_|    " -ForegroundColor Cyan
    Write-Host "                   |_|                  " -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Media Suite Manager — Windows Installer" -ForegroundColor White
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "  --> " -ForegroundColor Cyan -NoNewline
    Write-Host $Message
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] " -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [!!] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  [XX] " -ForegroundColor Red -NoNewline
    Write-Host $Message
}

function Test-CommandExists {
    param([string]$Command)
    return [bool](Get-Command $Command -ErrorAction SilentlyContinue)
}

# ── Preflight Checks ─────────────────────────────────────────────────────────

function Invoke-PreflightChecks {
    Write-Step "Running preflight checks..."

    # OS version
    $osVersion = [System.Environment]::OSVersion.Version
    if ($osVersion.Major -lt 10) {
        Write-Fail "Windows 10 or later is required."
        exit 1
    }
    Write-Success "Windows version OK ($($osVersion.Major).$($osVersion.Minor))"

    # PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        Write-Fail "PowerShell 5.1 or later is required."
        exit 1
    }
    Write-Success "PowerShell version OK ($($PSVersionTable.PSVersion))"

    # Internet connectivity
    try {
        $null = Invoke-WebRequest -Uri "https://github.com" -UseBasicParsing -TimeoutSec 5
        Write-Success "Internet connectivity OK"
    }
    catch {
        Write-Fail "Cannot reach GitHub. Check your internet connection."
        exit 1
    }

    # Docker (warn only — not hard requirement to install)
    if (Test-CommandExists "docker") {
        Write-Success "Docker found"
    }
    else {
        Write-Warn "Docker not found on PATH. Simplerr needs Docker to show containers."
        Write-Warn "Install Docker Desktop or ensure docker.exe is on your PATH."
    }

    # WSL (warn only)
    if (Test-CommandExists "wsl") {
        $wslOutput = & wsl --list --quiet 2>$null
        if ($LASTEXITCODE -eq 0 -and $wslOutput) {
            Write-Success "WSL2 found with distros available"
        }
        else {
            Write-Warn "WSL is installed but no distros found. WSL controls will be disabled."
        }
    }
    else {
        Write-Warn "WSL not found. WSL controls will be disabled in Simplerr."
    }
}

# ── Fetch Latest Release ──────────────────────────────────────────────────────

function Get-LatestRelease {
    Write-Step "Fetching latest release from GitHub..."

    $apiUrl = "https://api.github.com/repos/$Repo/releases/latest"

    try {
        $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing -Headers @{
            "User-Agent" = "simplerr-installer"
            "Accept"     = "application/vnd.github.v3+json"
        }
    }
    catch {
        Write-Fail "Failed to fetch release info: $_"
        exit 1
    }

    $version = $release.tag_name
    Write-Success "Latest version: $version"

    # Find the windows asset
    $asset = $release.assets | Where-Object {
        $_.name -like "*win-x64*" -or $_.name -like "*windows*"
    } | Select-Object -First 1

    if (-not $asset) {
        Write-Fail "No Windows binary found in release $version."
        exit 1
    }

    return @{
        Version     = $version
        DownloadUrl = $asset.browser_download_url
        FileName    = $asset.name
    }
}

# ── Download ──────────────────────────────────────────────────────────────────

function Invoke-Download {
    param(
        [string]$Url,
        [string]$FileName,
        [string]$Version
    )

    $tempDir = Join-Path $env:TEMP "simplerr-install"
    $tempFile = Join-Path $tempDir $FileName

    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    Write-Step "Downloading $FileName..."

    try {
        $webClient = New-Object System.Net.WebClient
        $webClient.Headers.Add("User-Agent", "simplerr-installer")
        $webClient.DownloadFile($Url, $tempFile)
    }
    catch {
        Write-Fail "Download failed: $_"
        exit 1
    }

    Write-Success "Download complete"
    return $tempFile
}

# ── Install ───────────────────────────────────────────────────────────────────

function Install-Simplerr {
    param([string]$SourceFile)

    Write-Step "Installing to $InstallDir..."

    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir | Out-Null
    }

    $destination = Join-Path $InstallDir $BinaryName

    # Handle file in use
    if (Test-Path $destination) {
        try {
            Remove-Item $destination -Force
        }
        catch {
            Write-Fail "Cannot replace existing binary — is Simplerr running?"
            exit 1
        }
    }

    # If download was a zip, extract it
    if ($SourceFile -like "*.zip") {
        Write-Step "Extracting archive..."
        $extractDir = Join-Path $env:TEMP "simplerr-extract"
        Expand-Archive -Path $SourceFile -DestinationPath $extractDir -Force

        $extracted = Get-ChildItem -Path $extractDir -Filter "*.exe" -Recurse |
        Select-Object -First 1

        if (-not $extracted) {
            Write-Fail "Could not find .exe in archive."
            exit 1
        }

        Copy-Item $extracted.FullName -Destination $destination -Force
        Remove-Item $extractDir -Recurse -Force
    }
    else {
        Copy-Item $SourceFile -Destination $destination -Force
    }

    Write-Success "Binary installed"
}

# ── PATH ──────────────────────────────────────────────────────────────────────

function Add-ToUserPath {
    $currentPath = [System.Environment]::GetEnvironmentVariable(
        "PATH",
        [System.EnvironmentVariableTarget]::User
    )

    if ($currentPath -like "*$InstallDir*") {
        Write-Success "PATH already contains install directory"
        return
    }

    Write-Step "Adding $InstallDir to user PATH..."

    $newPath = "$currentPath;$InstallDir"
    [System.Environment]::SetEnvironmentVariable(
        "PATH",
        $newPath,
        [System.EnvironmentVariableTarget]::User
    )

    # Also update current session
    $env:PATH = "$env:PATH;$InstallDir"

    Write-Success "PATH updated"
}

# ── Default Config ────────────────────────────────────────────────────────────

function New-DefaultConfig {
    if (-not (Test-Path $ConfigDir)) {
        New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null
    }

    $configFile = Join-Path $ConfigDir "config.json"

    if (Test-Path $configFile) {
        Write-Success "Existing config found — leaving untouched"
        return
    }

    Write-Step "Creating default config at $configFile..."

    # Detect WSL distros for a helpful default
    $distroName = "Ubuntu"
    if (Test-CommandExists "wsl") {
        $distros = & wsl --list --quiet 2>$null |
        Where-Object { $_ -match '\w' } |
        ForEach-Object { $_.Trim("`0") } |
        Select-Object -First 1

        if ($distros) { $distroName = $distros }
    }

    $config = @{
        wsl      = @{
            enabled    = $true
            distroName = $distroName
        }
        docker   = @{
            composePath              = ""
            pollIntervalSeconds      = 5
            statsPollIntervalSeconds = 3
        }
        services = @(
            @{ name = "plex"; icon = "📺"; friendlyName = "Plex Media Server" }
            @{ name = "sonarr"; icon = "📡"; friendlyName = "Sonarr (TV)" }
            @{ name = "radarr"; icon = "🎬"; friendlyName = "Radarr (Movies)" }
            @{ name = "qbittorrent"; icon = "🔽"; friendlyName = "qBittorrent" }
            @{ name = "prowlarr"; icon = "🔍"; friendlyName = "Prowlarr" }
        )
    }

    $config | ConvertTo-Json -Depth 5 |
    Set-Content -Path $configFile -Encoding UTF8

    Write-Success "Default config created"
}

# ── Start Menu Shortcut ───────────────────────────────────────────────────────

function New-StartMenuShortcut {
    Write-Step "Creating Start Menu shortcut..."

    $startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    $shortcutPath = Join-Path $startMenuDir "Simplerr.lnk"
    $binaryPath = Join-Path $InstallDir $BinaryName

    try {
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $binaryPath
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.Description = "Simplerr Media Suite Manager"
        $shortcut.Save()

        Write-Success "Start Menu shortcut created"
    }
    catch {
        Write-Warn "Could not create Start Menu shortcut (non-fatal): $_"
    }
}

# ── Uninstall ─────────────────────────────────────────────────────────────────

function Invoke-Uninstall {
    Write-Header
    Write-Host "  Uninstalling Simplerr..." -ForegroundColor Yellow
    Write-Host ""

    # Remove binary
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Success "Removed $InstallDir"
    }

    # Remove from PATH
    $currentPath = [System.Environment]::GetEnvironmentVariable(
        "PATH",
        [System.EnvironmentVariableTarget]::User
    )
    $newPath = ($currentPath -split ";" |
        Where-Object { $_ -ne $InstallDir }) -join ";"

    [System.Environment]::SetEnvironmentVariable(
        "PATH",
        $newPath,
        [System.EnvironmentVariableTarget]::User
    )
    Write-Success "Removed from PATH"

    # Remove shortcut
    $shortcutPath = Join-Path $env:APPDATA `
        "Microsoft\Windows\Start Menu\Programs\Simplerr.lnk"

    if (Test-Path $shortcutPath) {
        Remove-Item $shortcutPath -Force
        Write-Success "Removed Start Menu shortcut"
    }

    Write-Host ""
    Write-Host "  Config files kept at: $ConfigDir" -ForegroundColor Grey
    Write-Host "  Delete manually if you want a clean removal." -ForegroundColor Grey
    Write-Host ""
    Write-Success "Simplerr uninstalled."
}

# ── Main ──────────────────────────────────────────────────────────────────────

function Main {
    param([string]$Mode = "install")

    Write-Header

    if ($Mode -eq "uninstall") {
        Invoke-Uninstall
        return
    }

    Invoke-PreflightChecks
    Write-Host ""

    $release = Get-LatestRelease
    $tempFile = Invoke-Download `
        -Url      $release.DownloadUrl `
        -FileName $release.FileName `
        -Version  $release.Version

    Install-Simplerr -SourceFile $tempFile
    Add-ToUserPath
    New-DefaultConfig
    New-StartMenuShortcut

    # Cleanup temp
    Remove-Item (Split-Path $tempFile) -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "  ─────────────────────────────────────────" -ForegroundColor Cyan
    Write-Success "Simplerr $($release.Version) installed!"
    Write-Host ""
    Write-Host "  Run it:" -ForegroundColor White
    Write-Host "    simplerr" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Config:" -ForegroundColor White
    Write-Host "    $ConfigDir\config.json" -ForegroundColor Cyan
    Write-Host "  ─────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host ""
    Write-Warn "Open a new terminal window for PATH changes to take effect."
    Write-Host ""
}

# ── Entry Point ───────────────────────────────────────────────────────────────

$mode = if ($args -contains "--uninstall") { "uninstall" } else { "install" }
Main -Mode $mode