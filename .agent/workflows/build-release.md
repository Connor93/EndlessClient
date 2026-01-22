---
description: Build a release version of EndlessClient for Windows, Linux, or macOS
---

# Build EndlessClient Release

// turbo-all

## Quick Start

### macOS (Apple Silicon) - Creates .app bundle
```bash
cd /Users/cfraser/Projects/EndlessClient
./build-release.sh --app
```

### Linux - Creates distributable tarball
```bash
cd /Users/cfraser/Projects/EndlessClient
./build-release.sh --linux --tar
```

### Windows
```powershell
cd c:\Projects\EndlessClient
.\build-release.ps1
```

## All Options

### Bash Script (macOS/Linux)
```bash
./build-release.sh [OPTIONS]

Options:
  --clean           Clean output directory before building
  --output DIR      Custom output directory
  --linux           Build for Linux x64
  --linux-arm       Build for Linux ARM64
  --osx             Build for macOS x64 (Intel)
  --osx-arm         Build for macOS ARM64 (Apple Silicon)
  --app             Create macOS .app bundle (macOS only)
  --tar             Create .tar.gz archive (recommended for Linux)
  --debug           Use Debug configuration
  --release         Use Release configuration
```

### PowerShell Script (Windows)
```powershell
.\build-release.ps1 [-Clean] [-OutputDir "path\to\output"]
```

## Output

### macOS (.app bundle)
Location: `bin/Debug/EndlessClient.app`
- Double-click to run
- Drag to /Applications to install

### Linux (tarball)
Location: `bin/Release/EndlessClient-linux-x64.tar.gz`
- Extract: `tar -xzf EndlessClient-linux-x64.tar.gz`
- Run: `./linux-x64/EndlessClient`

### Windows
Location: `bin\Release\SingleFile\win-x64\`
- Contains EndlessClient.exe and all assets
- Zip folder for distribution

## Platform Comparison

| Platform | Config | Format | Size | Notes |
|----------|--------|--------|------|-------|
| Windows | Release | Single-file exe | ~155 MB | Just zip and distribute |
| Linux | Release | Single-file + tarball | ~88 MB exe, ~174 MB tar.gz | Self-contained, no dependencies |
| macOS | Debug | .app bundle | ~200 MB | Debug avoids Unity DI issues |

## What's Included
- Self-contained executable (includes .NET runtime)
- All game assets: config/, data/, gfx/, maps/, pub/, sfx/, mfx/, jbox/
- Native libraries embedded or bundled
