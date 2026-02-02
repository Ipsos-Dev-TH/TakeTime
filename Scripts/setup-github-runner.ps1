# ============================================================
# Setup GitHub Actions Self-Hosted Runner for TakeTime
# ============================================================
# วิธีใช้:
#   1. เปิด PowerShell as Administrator บนเครื่อง production server
#   2. ไปที่ GitHub repo: https://github.com/Ipsos-Dev-TH/TakeTime
#      → Settings → Actions → Runners → New self-hosted runner
#   3. คัดลอก token จากหน้า GitHub (ขึ้นต้นด้วย A...)
#   4. รัน: .\setup-github-runner.ps1 -Token "YOUR_TOKEN_HERE"
# ============================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$Token,

    [string]$RunnerName = "taketime-runner",
    [string]$InstallPath = "C:\actions-runner-taketime",
    [string]$Labels = "self-hosted,Windows,X64"
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " GitHub Actions Runner Setup - TakeTime" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERROR] Please run this script as Administrator!" -ForegroundColor Red
    exit 1
}

# Step 1: Create install directory
Write-Host "[1/5] Creating install directory: $InstallPath" -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Write-Host "  Directory already exists. Checking for existing runner..." -ForegroundColor Gray
    $svcStatus = & "$InstallPath\svc.cmd" status 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Existing runner service found. Removing..." -ForegroundColor Gray
        & "$InstallPath\svc.cmd" uninstall
        & "$InstallPath\config.cmd" remove --token $Token
    }
} else {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Step 2: Download latest runner
Write-Host "[2/5] Downloading latest GitHub Actions Runner..." -ForegroundColor Yellow
$runnerVersion = "2.321.0"
$runnerArch = "win-x64"
$downloadUrl = "https://github.com/actions/runner/releases/download/v$runnerVersion/actions-runner-$runnerArch-$runnerVersion.zip"
$zipPath = Join-Path $InstallPath "actions-runner.zip"

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "  Downloaded successfully" -ForegroundColor Green
} catch {
    Write-Host "  Failed to download runner v$runnerVersion. Trying to find latest version..." -ForegroundColor Yellow
    $latestRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/actions/runner/releases/latest" -UseBasicParsing
    $asset = $latestRelease.assets | Where-Object { $_.name -match "actions-runner-win-x64" }
    if ($asset) {
        $downloadUrl = $asset.browser_download_url
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
        Write-Host "  Downloaded latest version successfully" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Could not download runner!" -ForegroundColor Red
        exit 1
    }
}

# Step 3: Extract
Write-Host "[3/5] Extracting runner..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $InstallPath)
Remove-Item $zipPath -Force
Write-Host "  Extracted successfully" -ForegroundColor Green

# Step 4: Configure runner
Write-Host "[4/5] Configuring runner..." -ForegroundColor Yellow
Write-Host "  Runner Name: $RunnerName" -ForegroundColor Gray
Write-Host "  Labels: $Labels" -ForegroundColor Gray
Write-Host ""

Push-Location $InstallPath
try {
    & .\config.cmd `
        --url "https://github.com/Ipsos-Dev-TH/TakeTime" `
        --token $Token `
        --name $RunnerName `
        --labels $Labels `
        --work "_work" `
        --runasservice `
        --replace

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Runner configuration failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Runner configured successfully" -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 5: Install and start as Windows Service
Write-Host "[5/5] Installing as Windows Service..." -ForegroundColor Yellow
Push-Location $InstallPath
try {
    & .\svc.cmd install
    & .\svc.cmd start

    Write-Host "  Service installed and started" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " Runner setup complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Runner Name: $RunnerName" -ForegroundColor White
Write-Host "Install Path: $InstallPath" -ForegroundColor White
Write-Host "Labels: $Labels" -ForegroundColor White
Write-Host ""
Write-Host "To check status:" -ForegroundColor Gray
Write-Host "  & '$InstallPath\svc.cmd' status" -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall:" -ForegroundColor Gray
Write-Host "  & '$InstallPath\svc.cmd' stop" -ForegroundColor Gray
Write-Host "  & '$InstallPath\svc.cmd' uninstall" -ForegroundColor Gray
Write-Host "  & '$InstallPath\config.cmd' remove --token TOKEN" -ForegroundColor Gray
