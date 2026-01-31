---
description: Build and start the EndlessClient game
---

# Build and Start EndlessClient

// turbo-all

1. Build the client:
```bash
cd /Users/cfraser/Projects/EndlessClient
dotnet build --no-restore EndlessClient/EndlessClient.csproj -p:CheckEolWorkloads=false
```

2. Setup required directories (creates empty dirs that the game expects):
```bash
cd /Users/cfraser/Projects/EndlessClient/bin/Debug/client/net10.0/osx-arm64
mkdir -p mfx sfx jbox maps
```

3. Symlink ClientAssets to build directory (avoids copying large files):
```bash
cd /Users/cfraser/Projects/EndlessClient/bin/Debug/client/net10.0/osx-arm64
ln -sfn /Users/cfraser/Projects/EndlessClient/ClientAssets/gfx gfx
ln -sfn /Users/cfraser/Projects/EndlessClient/ClientAssets/data data  
ln -sfn /Users/cfraser/Projects/EndlessClient/ClientAssets/pub pub
ln -sfn /Users/cfraser/Projects/EndlessClient/ClientAssets/config config
```

4. Start the client:
```bash
cd /Users/cfraser/Projects/EndlessClient/bin/Debug/client/net10.0/osx-arm64
dotnet EndlessClient.dll &
```

## Notes
- The MIDI warning "Unable to initialize the midi sound system" is expected - background music won't play but the game runs fine
- Config file is at `ClientAssets/config/settings.ini` (symlinked to build output)
- User overrides can be placed at `~/.endlessclient/config/settings.ini`
- `mfx`, `sfx`, `jbox`, and `maps` can be empty directories initially
