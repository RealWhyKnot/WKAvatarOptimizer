<#
.SYNOPSIS
    Downloads the reference repository (d4rkAvatarOptimizer) into a 'Reference' folder.
#>

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$ReferenceDir = Join-Path $ScriptDir "Reference"
$TempDir = Join-Path $ScriptDir "Temp_Ref"
$RepoUrl = "https://github.com/d4rkc0d3r/d4rkAvatarOptimizer/archive/refs/heads/main.zip"

# --- Cleanup Existing Reference ---
if (Test-Path $ReferenceDir) {
    Write-Host "Removing existing Reference directory..." -ForegroundColor Yellow
    Remove-Item $ReferenceDir -Recurse -Force
}

# --- Setup Directories ---
if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
New-Item -ItemType Directory -Path $ReferenceDir -Force | Out-Null

# --- Download ---
$ZipPath = Join-Path $TempDir "repo.zip"
Write-Host "Downloading reference repository from $RepoUrl..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri $RepoUrl -OutFile $ZipPath -UseBasicParsing
} catch {
    Write-Error "Failed to download repository: $_"
}

# --- Extract ---
Write-Host "Extracting..." -ForegroundColor Cyan
Expand-Archive -Path $ZipPath -DestinationPath $TempDir -Force

# --- Move Content ---
# GitHub zips usually contain a root folder like 'd4rkAvatarOptimizer-main'
$ExtractedRoot = Get-ChildItem -Path $TempDir -Directory | Select-Object -First 1
if ($ExtractedRoot) {
    Write-Host "Moving content to Reference..." -ForegroundColor Cyan
    Get-ChildItem -Path $ExtractedRoot.FullName | Move-Item -Destination $ReferenceDir -Force
} else {
    Write-Error "Could not find extracted root folder."
}

# --- Cleanup Temp ---
Remove-Item $TempDir -Recurse -Force
Write-Host "Done. Reference code is in: $ReferenceDir" -ForegroundColor Green