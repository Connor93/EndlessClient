# EndlessClient Release Build Script
# Creates a single-file executable with all game assets ready to distribute

param(
    [switch]$Clean,
    [string]$OutputDir = "bin\Release\SingleFile"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "=== EndlessClient Release Build ===" -ForegroundColor Cyan

# Clean if requested
if ($Clean -and (Test-Path $OutputDir)) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Build single-file executable
# Note: IncludeNativeLibrariesForSelfExtract=false keeps SDL2.dll and other native libs external
# MonoGame requires these as external files - they cannot be bundled
Write-Host "Building single-file executable..." -ForegroundColor Green
dotnet publish EndlessClient/EndlessClient.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Clean up debug files
Write-Host "Cleaning up debug files..." -ForegroundColor Yellow
Remove-Item "$OutputDir\*.pdb" -Force -ErrorAction SilentlyContinue
Remove-Item "$OutputDir\*.config" -Force -ErrorAction SilentlyContinue

# The single-file publish doesn't include ContentPipeline files properly
# Copy from the normal build output which has the .xnb files
Write-Host "Copying ContentPipeline from build output..." -ForegroundColor Green
$contentSource = Join-Path $ProjectRoot "bin\Release\client\net8.0-windows\win-x64\ContentPipeline"
$contentDest = Join-Path $OutputDir "ContentPipeline"
if (Test-Path $contentSource) {
    Remove-Item -Path $contentDest -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item -Path $contentSource -Destination $contentDest -Recurse -Force
}

# Copy game assets from ClientAssets
Write-Host "Copying game assets..." -ForegroundColor Green
$assetFolders = @("config", "data", "gfx", "jbox", "maps", "mfx", "pub", "sfx")

foreach ($folder in $assetFolders) {
    $source = Join-Path $ProjectRoot "ClientAssets\$folder"
    $dest = Join-Path $OutputDir $folder
    
    if (Test-Path $source) {
        Write-Host "  Copying $folder..." -ForegroundColor Gray
        Copy-Item -Path $source -Destination $dest -Recurse -Force
    }
    else {
        Write-Host "  Warning: $folder not found in ClientAssets" -ForegroundColor Yellow
    }
}

# Copy missing sfx files from binaries package (har*.wav harp notes, etc.)
Write-Host "Copying additional sfx from binaries package..." -ForegroundColor Green
$binariesSfx = Join-Path $ProjectRoot "packages\endlessclient.binaries\1.4.0.2\build\net462\client\sfx"
$destSfx = Join-Path $OutputDir "sfx"
if (Test-Path $binariesSfx) {
    # Copy har*.wav (harp notes) and gui*.wav (guitar notes) if they exist
    Get-ChildItem -Path $binariesSfx -Filter "har*.wav" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $destSfx -Force
    }
    Get-ChildItem -Path $binariesSfx -Filter "gui*.wav" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $destSfx -Force
    }
    Write-Host "  Copied instrument samples from binaries package." -ForegroundColor Gray
}

# Show results
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "Output: $((Resolve-Path $OutputDir).Path)" -ForegroundColor White
Write-Host ""
Get-ChildItem $OutputDir | Format-Table Name, @{
    Name       = "Size"; 
    Expression = {
        if ($_.PSIsContainer) { "[DIR]" } 
        else { "{0:N2} MB" -f ($_.Length / 1MB) }
    }
} -AutoSize

Write-Host ""
Write-Host "Ready to distribute! Zip the folder and share." -ForegroundColor Green
