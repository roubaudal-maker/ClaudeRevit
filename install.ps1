# Claude Revit installer / updater
# Downloads the latest GitHub release zip and unpacks into the user's Revit Addins folder.
# Re-run any time to update.
#
# Usage:
#   iwr https://raw.githubusercontent.com/roubaudal-maker/ClaudeRevit/main/install.ps1 | iex
# Or to install a specific version:
#   $env:CLAUDEREVIT_VERSION = "v1.3"
#   iwr https://raw.githubusercontent.com/roubaudal-maker/ClaudeRevit/main/install.ps1 | iex

param(
    [string]$Repo = "roubaudal-maker/ClaudeRevit",
    [string]$RevitVersion = "2027",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# Allow override via env var (handy for iex pipeline)
if ([string]::IsNullOrEmpty($Version) -and -not [string]::IsNullOrEmpty($env:CLAUDEREVIT_VERSION)) {
    $Version = $env:CLAUDEREVIT_VERSION
}

Write-Host "Claude Revit installer" -ForegroundColor Cyan
Write-Host "Target: Revit $RevitVersion (repo: $Repo)" -ForegroundColor DarkGray

# Resolve release
$apiBase = "https://api.github.com/repos/$Repo"
if ([string]::IsNullOrEmpty($Version)) {
    Write-Host "Querying latest release..." -ForegroundColor DarkGray
    $release = Invoke-RestMethod "$apiBase/releases/latest"
} else {
    Write-Host "Querying release '$Version'..." -ForegroundColor DarkGray
    $release = Invoke-RestMethod "$apiBase/releases/tags/$Version"
}
$asset = $release.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
if (-not $asset) {
    throw "No .zip asset found in release '$($release.tag_name)'"
}

Write-Host "Found $($asset.name) ($([math]::Round($asset.size / 1KB, 1)) KB)" -ForegroundColor Green

# Download
$tmp = Join-Path $env:TEMP "ClaudeRevit-$($release.tag_name).zip"
Invoke-WebRequest $asset.browser_download_url -OutFile $tmp

# Check if Revit is running — must close before install
$revit = Get-Process -Name Revit -ErrorAction SilentlyContinue
if ($revit) {
    Write-Host ""
    Write-Host "Revit is running (PID $($revit.Id))." -ForegroundColor Yellow
    Write-Host "Close Revit completely, then press Enter to continue." -ForegroundColor Yellow
    Read-Host
}

# Install
$target = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
New-Item -ItemType Directory -Force -Path $target | Out-Null

Expand-Archive -Path $tmp -DestinationPath $target -Force
Remove-Item $tmp -Force

Write-Host ""
Write-Host "Installed Claude Revit $($release.tag_name) to:" -ForegroundColor Green
Write-Host "  $target" -ForegroundColor White
Write-Host ""

# API key check
$apiKey = [Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")
if ([string]::IsNullOrEmpty($apiKey)) {
    Write-Host "No ANTHROPIC_API_KEY set yet." -ForegroundColor Yellow
    Write-Host "Either set it now:" -ForegroundColor DarkGray
    Write-Host '  [Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-...", "User")' -ForegroundColor White
    Write-Host "or click the gear icon in the Claude chat pane after launching Revit." -ForegroundColor DarkGray
} else {
    Write-Host "ANTHROPIC_API_KEY is set (length $($apiKey.Length))." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Launch Revit and look for the Claude tab." -ForegroundColor Cyan
