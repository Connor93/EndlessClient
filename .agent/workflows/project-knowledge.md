---
description: How to maintain project knowledge for the EndlessClient codebase
---

# Project Knowledge Maintenance Workflow

This workflow ensures that project-specific knowledge is preserved and shared across agent sessions.

## Before Starting Any Task

1. **Read the project knowledge file** at `docs/project_knowledge.md`
   - Review all existing entries to understand known quirks, patterns, and requirements
   - Pay special attention to sections relevant to your current task (UI, Inventory, Build, DI, etc.)

2. **Understand the environment** - Key sections to review:
   - Section 5: Build & Development Environment (macOS Apple Silicon specific issues)
   - Section 3: UI Component Patterns (XNAButton usage)
   - Section 4: Theme & Alignment Nuances

## During Your Task

3. **Document discoveries immediately** - When you learn something new about:
   - Asset management (GFX resource IDs, sprite sheets)
   - UI component patterns or alignment quirks
   - Build system issues (especially macOS ARM64)
   - Dependency injection patterns
   - Path resolution differences across platforms
   
   Add it to `docs/project_knowledge.md` right away. Do NOT wait until task completion.

## After Completing Your Task

4. **Review and update knowledge**
   - Did you discover any new quirks or requirements?
   - Did you establish any new patterns that others should follow?
   - Were there any "gotchas" that took time to debug?

5. **Add new sections** if the discovery doesn't fit existing categories

## Building on macOS

```bash
# Build command
dotnet build --no-restore EndlessClient/EndlessClient.csproj -p:CheckEolWorkloads=false

# Run from output directory
cd bin/Debug/client/net10.0/osx-arm64
dotnet EndlessClient.dll
```

## Related Projects

This client works with the **etheos** server (sibling directory `../etheos/`).
- Server knowledge is maintained in: `../etheos/docs/project_knowledge.md`
- When making changes that affect client-server interaction, update both knowledge files
- Client assets (maps, pub files) are synced to the server during server builds
