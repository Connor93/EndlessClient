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

2. Start the client:
```bash
cd /Users/cfraser/Projects/EndlessClient/bin/Debug/client/net10.0/osx-arm64
dotnet EndlessClient.dll
```

## Notes
- The MIDI warning "Unable to initialize the midi sound system" is expected - background music won't play but the game runs fine
- Config file is at `ClientAssets/config/settings.ini` (copied to build output)
- User overrides can be placed at `~/.endlessclient/config/settings.ini`
