---
description: Build a release version of EndlessClient (single-file exe with assets)
---

# Build EndlessClient Release

// turbo-all

1. Run the release build script:
```powershell
cd c:\Projects\EndlessClient
.\build-release.ps1
```

## Options

- **Clean build**: `.\build-release.ps1 -Clean`
- **Custom output**: `.\build-release.ps1 -OutputDir "path\to\output"`

## Output

The script creates a distributable folder at `bin\Release\SingleFile\` containing:
- `EndlessClient.exe` - Single-file executable (~155 MB, includes .NET runtime)
- `config/` - User preferences and settings
- `ContentPipeline/` - UI assets
- `data/`, `gfx/`, `maps/`, `pub/`, `sfx/`, `mfx/`, `jbox/` - Game assets

## Notes
- Self-contained: No .NET runtime needed on target machine
- Just zip the output folder to distribute
