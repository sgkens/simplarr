# install.ps1
# Simplerr Windows Installer
# Usage: irm https://gitlab.com/phellams/simplerr/-/raw/main/install.ps1 | iex

#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Constants ────────────────────────────────────────────────────────────────

$RepoPath = "phellams/simplerr"
$ProjectId = "67141434" # Need to verify this ID or use path-based API
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
        $null = Invoke-WebRequest -Uri "https://gitlab.com" -UseBasicParsing -TimeoutSec 5
        Write-Success "Internet connectivity OK"
    }
    catch {
        Write-Fail "Cannot reach GitLab. Check your internet connection."
        exit 1
    }

    # Docker
    if (Test-CommandExists "docker") {
        Write-Success "Docker found"
    }
    else {
        Write-Warn "Docker not found on PATH. Simplerr needs Docker to show containers."
    }

    # WSL
    if (Test-CommandExists "wsl") {
        Write-Success "WSL found"
    }
}

# ── Fetch Latest Release ──────────────────────────────────────────────────────

function Get-LatestRelease {
    Write-Step "Fetching latest release from GitLab..."

    # First get the latest tag
    $tagsUrl = "https://gitlab.com/api/v4/projects/phellams%2Fsimplerr/repository/tags"
    try {
        $tags = Invoke-RestMethod -Uri $tagsUrl -UseBasicParsing
        $version = $tags[0].name
    }
    catch {
        Write-Fail "Failed to fetch version info: $_"
        exit 1
    }

    Write-Success "Latest version: $version"

    $fileName = "simplerr-$version-win-x64.zip"
    $downloadUrl = "https://gitlab.com/api/v4/projects/phellams%2Fsimplerr/packages/generic/simplarr/$version/$fileName"

    return @{
        Version     = $version
        DownloadUrl = $downloadUrl
        FileName    = $fileName
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

    if (Test-Path $destination) {
        try { Remove-Item $destination -Force } catch { exit 1 }
    }

    Write-Step "Extracting archive..."
    $extractDir = Join-Path $env:TEMP "simplerr-extract"
    Expand-Archive -Path $SourceFile -DestinationPath $extractDir -Force

    $extracted = Get-ChildItem -Path $extractDir -Filter "*.exe" -Recurse | Select-Object -First 1
    if (-not $extracted) { exit 1 }

    Copy-Item $extracted.FullName -Destination $destination -Force
    Remove-Item $extractDir -Recurse -Force

    Write-Success "Binary installed"
}

# ── PATH ──────────────────────────────────────────────────────────────────────

function Add-ToUserPath {
    $currentPath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User)
    if ($currentPath -like "*$InstallDir*") { return }
    [System.Environment]::SetEnvironmentVariable("PATH", "$currentPath;$InstallDir", [System.EnvironmentVariableTarget]::User)
    $env:PATH = "$env:PATH;$InstallDir"
}

# ── Default Config ────────────────────────────────────────────────────────────

function New-DefaultConfig {
    if (-not (Test-Path $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null }
    $configFile = Join-Path $ConfigDir "config.json"
    if (Test-Path $configFile) { return }

    $config = @{
        wsl = @{ enabled = $true; distroName = "Ubuntu" }
        docker = @{ pollIntervalSeconds = 5; statsPollIntervalSeconds = 3 }
        services = @(
            @{ name = "plex"; icon = "📺"; friendlyName = "Plex" }
            @{ name = "sonarr"; icon = "📡"; friendlyName = "Sonarr" }
            @{ name = "radarr"; icon = "🎬"; friendlyName = "Radarr" }
        )
    }
    $config | ConvertTo-Json -Depth 5 | Set-Content -Path $configFile -Encoding UTF8
}

# ── Main ──────────────────────────────────────────────────────────────────────

function Main {
    Write-Header
    Invoke-PreflightChecks
    Write-Host ""

    $release = Get-LatestRelease
    $tempFile = Invoke-Download -Url $release.DownloadUrl -FileName $release.FileName -Version $release.Version

    Install-Simplerr -SourceFile $tempFile
    Add-ToUserPath
    New-DefaultConfig

    Remove-Item (Split-Path $tempFile) -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Success "Simplerr $($release.Version) installed!"
    Write-Host "Run 'simplerr' in a new terminal."
}

Main
