# HoardersForge Mod Build & Package Script

# Stop on errors
$ErrorActionPreference = "Stop"

# Paths
$ModDir = $PSScriptRoot
$ReleaseDir = Join-Path $ModDir "release"

# Read version from modinfo.json
$ModInfoPath = Join-Path $ModDir "resources\modinfo.json"
$ModInfo = Get-Content -Raw -Path $ModInfoPath | ConvertFrom-Json
$ModVersion = $ModInfo.version

$ModZipName = "hoardersforge_v$($ModVersion).zip"
$ModZipPath = Join-Path $ReleaseDir $ModZipName

$GameModsDir = Join-Path $env:APPDATA "VintagestoryData\Mods"
$CentralReleasesDir = Join-Path $ModDir "..\releases"

Write-Output "=== Starting packaging for HoardersForge ==="

# 1. Run unit tests
Write-Output "Running unit tests..."
dotnet test "$ModDir\tests\HoardersForge.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit tests failed! Aborting build."
    exit 1
}

# 2. Compile C# DLL
Write-Output "Compiling C# project..."
dotnet build "$ModDir\HoardersForge.csproj" -c Release

# 2. Create directories
if (-not (Test-Path $ReleaseDir)) {
    New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
    Write-Output "Created release directory: $ReleaseDir"
}

if (-not (Test-Path $CentralReleasesDir)) {
    New-Item -ItemType Directory -Path $CentralReleasesDir | Out-Null
    Write-Output "Created central releases directory: $CentralReleasesDir"
}

# 3. Package mod into zip using 7z
Write-Output "Packaging files into ZIP..."

Push-Location $ModDir
try {
    # Delete old zip if exists
    if (Test-Path $ModZipPath) {
        Remove-Item $ModZipPath -Force
    }

    # Create a clean temporary directory for building the zip
    $TempBuildDir = Join-Path $ReleaseDir "temp_build"
    if (Test-Path $TempBuildDir) {
        Remove-Item $TempBuildDir -Recurse -Force | Out-Null
    }
    New-Item -ItemType Directory -Path $TempBuildDir | Out-Null

    # Copy files/folders to the temporary build dir
    Copy-Item -Path "resources\modinfo.json" -Destination $TempBuildDir -Force
    if (Test-Path "modicon.png") {
        Copy-Item -Path "modicon.png" -Destination $TempBuildDir -Force
    }
    if (Test-Path "resources\assets") {
        Copy-Item -Path "resources\assets" -Destination $TempBuildDir -Recurse -Force
    }
    
    # Copy compiled DLL
    $DllPath = Join-Path $ModDir "bin\Release\HoardersForge.dll"
    Copy-Item -Path $DllPath -Destination $TempBuildDir -Force

    # Run 7z to create the zip file (forces forward slashes '/' compatible with Linux!)
    Push-Location $TempBuildDir
    try {
        7z a -tzip -mx9 $ModZipPath * | Out-Null
    }
    finally {
        Pop-Location
    }

    # Clean up temp build folder
    Remove-Item $TempBuildDir -Recurse -Force | Out-Null
    
    Write-Output "Successfully packaged mod into: $ModZipPath"
}
finally {
    Pop-Location
}

# 4. Deploy to Vintage Story Mods folder
Write-Output "Deploying to Vintage Story Mods folder..."
if (-not (Test-Path $GameModsDir)) {
    New-Item -ItemType Directory -Path $GameModsDir | Out-Null
    Write-Output "Created Game Mods directory: $GameModsDir"
}

$TargetGameZip = Join-Path $GameModsDir $ModZipName
Copy-Item $ModZipPath $TargetGameZip -Force
Write-Output "Deployed to game mods folder: $TargetGameZip"

# 5. Copy to central releases folder
$TargetCentralZip = Join-Path $CentralReleasesDir $ModZipName
Copy-Item $ModZipPath $TargetCentralZip -Force
Write-Output "Copied to central releases: $TargetCentralZip"

Write-Output "=== Build & Deploy Completed Successfully! ==="
