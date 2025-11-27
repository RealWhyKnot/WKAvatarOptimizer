<#
.SYNOPSIS
    Downloads and extracts dependencies for WKAvatarOptimizer.
#>

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$DependenciesDir = Join-Path $ScriptDir "Dependencies"
$TempDir = Join-Path $ScriptDir "Temp"

# --- Configuration ---
$VRCUrls = @{
    "Base" = "https://github.com/vrchat/packages/releases/download/3.10.0/com.vrchat.base-3.10.0.zip"
    "Avatars" = "https://github.com/vrchat/packages/releases/download/3.10.0/com.vrchat.avatars-3.10.0.zip"
}

# --- Setup Directories ---
if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
if (Test-Path $DependenciesDir) { Remove-Item $DependenciesDir -Recurse -Force }
New-Item -ItemType Directory -Path $DependenciesDir -Force | Out-Null

function Download-File {
    param ($Url, $Dest)
    Write-Host "Downloading $Url..."
    try {
        Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
    } catch {
        Write-Warning "Failed to download $Url. Error: $_"
    }
}

function Extract-Zip {
    param ($Zip, $Dest)
    Write-Host "Extracting $Zip..."
    try {
        Expand-Archive -Path $Zip -DestinationPath $Dest -Force
    } catch {
        Write-Warning "Failed to extract $Zip"
    }
}

# --- 1. VRChat SDK ---
Download-File $VRCUrls["Base"] "$TempDir\VRC_Base.zip"
Extract-Zip "$TempDir\VRC_Base.zip" "$TempDir\VRC_Base"

Download-File $VRCUrls["Avatars"] "$TempDir\VRC_Avatars.zip"
Extract-Zip "$TempDir\VRC_Avatars.zip" "$TempDir\VRC_Avatars"

# Map: Target FileName in Dependencies -> Source Search Pattern
$VRC_Dlls = @{
    "VRCSDKBase.dll" = "VRCSDKBase.dll"
    "VRCSDKBase-Editor.dll" = "VRCSDKBase-Editor.dll"
    "VRCSDK3A.dll" = "VRCSDK3A.dll"
    "VRCSDK3A-Editor.dll" = "VRCSDK3A-Editor.dll"
    "VRC.Dynamics.dll" = "VRC.Dynamics.dll"
    "VRC.SDK3.Dynamics.PhysBone.dll" = "VRC.SDK3.Dynamics.PhysBone.dll"
    "VRC.SDK3.Dynamics.Contact.dll" = "VRC.SDK3.Dynamics.Contact.dll"
    "VRCCore-Editor.dll" = "VRCCore-Editor.dll"
}

$SearchDirs = @("$TempDir\VRC_Base", "$TempDir\VRC_Avatars")

foreach ($targetName in $VRC_Dlls.Keys) {
    $pattern = $VRC_Dlls[$targetName]
    $found = $false
    foreach ($dir in $SearchDirs) {
        $result = Get-ChildItem -Path $dir -Recurse -Filter $pattern | Select-Object -First 1
        if ($result) {
            Copy-Item $result.FullName (Join-Path $DependenciesDir $targetName) -Force
            Write-Host "  -> Copied $targetName" -ForegroundColor Green
            $found = $true
            break
        }
    }
    if (-not $found) { Write-Warning "Could not find $targetName" }
}

# --- 2. Unity DLLs (Locate Installation) ---
Write-Host "Locating Unity installation..."

$UnityHubPaths = @(
    "C:\Program Files\Unity\Hub\Editor",
    "C:\Program Files\Unity\Editor",
    "D:\Unity\Hub\Editor",
    "E:\Unity\Hub\Editor"
)

$UnityInstallPath = $null

foreach ($path in $UnityHubPaths) {
    if (Test-Path $path) {
        # Find latest version (simple string sort, usually works for 2022.x)
        $Editors = Get-ChildItem -Path $path -Directory | Sort-Object Name -Descending
        foreach ($editor in $Editors) {
             $ManagedPath = Join-Path $editor.FullName "Editor\Data\Managed"
             if (Test-Path $ManagedPath) {
                Write-Host "Found Unity Install: $($editor.Name)" -ForegroundColor Cyan
                $UnityInstallPath = $ManagedPath
                break 
             }
        }
        if ($UnityInstallPath) { break }
    }
}

if ($UnityInstallPath) {
    # Root Managed DLLs
    $RootDlls = @("UnityEngine.dll", "UnityEditor.dll")
    foreach ($dll in $RootDlls) {
        $src = Join-Path $UnityInstallPath $dll
        if (Test-Path $src) {
            Copy-Item $src $DependenciesDir -Force
            Write-Host "  -> Copied $dll" -ForegroundColor Green
        } else {
            Write-Warning "Missing $dll in $UnityInstallPath"
        }
    }
    
    # UnityEngine Subfolder DLLs
    $SubFolderDlls = @(
        "UnityEngine.CoreModule.dll", 
        "UnityEditor.CoreModule.dll", 
        "UnityEngine.AnimationModule.dll",
        "UnityEngine.ImageConversionModule.dll",
        "UnityEngine.ClothModule.dll",
        "UnityEngine.AssetBundleModule.dll",
        "UnityEngine.AudioModule.dll",
        "UnityEngine.TextRenderingModule.dll",
        "UnityEngine.ParticleSystemModule.dll",
        "UnityEngine.PhysicsModule.dll",
        "UnityEngine.IMGUIModule.dll"
    )
    
    # Check both root and UnityEngine subfolder
    foreach ($dll in $SubFolderDlls) {
        $srcRoot = Join-Path $UnityInstallPath $dll
        $srcSub = Join-Path "$UnityInstallPath\UnityEngine" $dll
        
        if (Test-Path $srcRoot) {
            Copy-Item $srcRoot $DependenciesDir -Force
            Write-Host "  -> Copied $dll" -ForegroundColor Green
        } elseif (Test-Path $srcSub) {
            Copy-Item $srcSub $DependenciesDir -Force
            Write-Host "  -> Copied $dll" -ForegroundColor Green
        } else {
            Write-Warning "Missing $dll"
        }
    }
} else {
    Write-Error "Could not locate a valid Unity installation."
}

# Cleanup
Remove-Item $TempDir -Recurse -Force
Write-Host "Done."