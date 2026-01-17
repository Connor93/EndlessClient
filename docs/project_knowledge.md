# Project Knowledge: EndlessClient

This document serves as a repository for technical learnings, architectural patterns, and asset management details discovered during the development of the EndlessClient.

## 1. Asset Management & GFX Resources

### Resource ID Mapping
There is a consistent offset pattern between the **File ID** (found in the theme `.egf` file) and the **Code Resource ID** used in `GFXTypes`.

*   **Pattern**: `Code Resource ID = File ID - 100`
*   **Examples found**:
    *   Main HUD Buttons: File ID `125` -> Code ID `25`
    *   Inventory Strip Buttons: File ID `123` -> Code ID `23`

### UI Sprite Sheets
Standard UI buttons often use a **Two-Column Layout** for states:
*   **Column 1**: Normal State (Unhovered)
*   **Column 2**: Hover State (Hovered)
*   **Implementation**: Use `XNAButton` controls. Define `sourceRectangle` (Normal) and `sourceRectangleOver` (Hover) pointing to these respective columns.

### Static HUD Elements
Some UI elements are drawn as static backgrounds in `HudBackgroundFrame.cs` and do not have attached input handlers.
*   **Top Bar (Resource 23)**: Drawn at `(49, 7)`. Serves as the background for the Quest/SessionExp buttons. 
    *   *Note*: Disabled in `HudBackgroundFrame.cs` to remove "ghost" artifacts when buttons are hidden.
*   **Sidebar (Resource 22)**: Drawn at `(7, 53)`. Vertical strip likely intended for sidebar buttons.
    *   *Note*: Disabled in `HudBackgroundFrame.cs` to remove "ghost" artifacts.

## 2. Inventory System

### Expanding Inventory Size
To expand the inventory grid (e.g., from 14 to 17 columns):
1.  **Grid Logic**: Update `InventoryPanel.InventoryRowSlots` constant.
2.  **Item Hit-Tests**: Update `InventoryPanelItem.GridArea` width to cover the new columns.
    *   Formula: `(Slots * 26px) - Margin`.
    *   Example (17 slots): `(17 * 26) - 1 = 441` width.

### Custom Control Integration
When adding custom controls (like the Page/Action strip) to the inventory:
*   **Positioning**: Place them relative to the panel's coordinates. For a wide inventory, the "18th Column" area is approx `X = 453-458`.
*   **Button Sizing**: Ensure button dimensions match the asset strip width (e.g., 23px or 38px) to prevent clipping, even if the internal graphic is smaller.
*   **Vertical Alignment**: For strict stacking (like the 1, 2, Drop, Junk strip), use the precise Y-coordinates from the sprite sheet (`0, 26, 53, 79` etc.) and ensure they align with the inventory grid rows (26px height).

## 3. UI Component Patterns

### XNAButton
*   **Standard Control**: Use `XNAButton` (from `XNAControls` library) for clickable UI elements.
*   **Transparency**: Ensure textures are loaded with `transparent: true` via `NativeGraphicsManager`.
*   **Hover Effects**: Automatically handled by `XNAButton` if `sourceRectangleOver` is provided. The control swaps the source rect on mouse enter/leave.
*   **Sound Effects**: Attach click sounds via `OnMouseDown`:
    ```csharp
    btn.OnMouseDown += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
    ```

### Transparent / Invisible Buttons
*   **Use Case**: If a button needs to be clickable but invisible (e.g., clicking on a pre-painted background element), a custom class overriding `OnDrawControl` to do nothing was initially explored.
*   **Preferred Approach**: In most cases (like the inventory strip), the "Background" does NOT contain the button graphics, so standard `XNAButton` should be used to draw the actual sprites from the theme resource.

## 4. Theme & Alignment Nuances
*   **Global Layout Offset**: A recurring **-11px** horizontal shift was observed when aligning the Character Selection screen elements. This suggests a potential global offset in the theme's background assets compared to the original EO layout.
*   **Login Window Alignment**: The background image containing text labels (Account/Password) did not align uniformly with the input fields.
    *   **Strategy**: Keep the background fixed (Y=291).
    *   **Adjust Controls**: Shift text boxes independently (Username Y-7px, Password Y-12px) to match the label spacing.
*   **Micro-Adjustments**: UI assets may not strictly align to 0,0 grid coordinates. Expect to fine-tune X/Y positions by 1-5 pixels to center elements within panel borders or rows.
*   **Sprite Sheet Packing**: Assets like the Inventory Button Strip (Resource 23) are often tightly packed.
    *   Example: 4 buttons packed vertically with variable heights (26px/27px) to average out to non-integer sizes (26.5px).
    *   Always verify coordinate strides against the actual image or visual feedback.

## 5. Build & Development Environment (macOS Apple Silicon)

### SDK Compatibility
*   **Setup**: For users with newer .NET SDKs (e.g., .NET 10) who cannot revert to the specifically pinned .NET 8 version.
*   **Global.json**: Must be updated to allow major version roll-forward: `"rollForward": "latestMajor"`.
*   **EOL Workloads**: The `net8.0-macos` workload is considered End-of-Life by .NET 10.
    *   *Workaround**: Build with `dotnet build -p:CheckEolWorkloads=false`.

### Architecture Mismatches (Roslyn Tools)
*   **Issue**: `codegeneration.roslyn.tool` defaults to a `runtimeconfig` targeting .NET Core 2.1. This attempts to load the .NET 3.1 runtime, which may only be available as x86_64. On Apple Silicon (arm64), this causes a `BadImageFormatException` or `mach-o file, but is an incompatible architecture` error.
*   **Fix**: Edit `packages/codegeneration.roslyn.tool/.../CodeGeneration.Roslyn.Tool.runtimeconfig.json` to target `8.0.0` or a compatible runtime present on the machine.

### MonoGame Content Builder (MGCB)
*   **Native Dependencies**: MGCB relies on `freeimage`. On Apple Silicon/macOS, this library is not always automatically found.
*   **Fix**:
    1.  Install via Homebrew: `brew install freeimage`
    2.  Manually copy the dylib to the tool folder if it fails to load:
        ```bash
        cp /opt/homebrew/lib/libfreeimage.dylib packages/dotnet-mgcb/.../tools/net8.0/any/
        ```

### Platform Targeting (DesktopGL vs Native macOS)
*   **Xcode Requirement**: Targeting `netX.X-macos` requires a full Xcode App Bundle logic, which demands a complete Xcode installation.
*   **Command Line Tools Only**: If only Xcode Command Line Tools are installed, the build will fail with "A valid Xcode installation was not found".
*   **Solution for Dev**: Switch `EndlessClient.csproj` to target generic `net10.0` (or `net8.0`) and use `<MonoGamePlatform>DesktopGL</MonoGamePlatform>`. This builds a standard executable using SDL2/OpenGL that runs fine on macOS without the app bundle overhead.

### Code Style
*   **Enforcement**: The project enforces strict code formatting. Build failures like `IDE0055: Fix formatting` are common.
*   **Fix**: Run `dotnet format EndlessClient.sln`.

### Asset Copying
*   **Issue**: On some platforms (or depending on build order), the standard NuGet asset copy (from `EndlessClient.Binaries`) might overwrite custom assets in `ClientAssets` if the timestamp or copy order isn't strictly controlled.
*   **Fix**: Ensure `CopyCustomClientAssets` target in `EndlessClient.csproj` runs `AfterTargets="Build"` (rather than undefined/earlier targets) to guarantee it overwrites default assets with custom ones.

### Dependency Injection (Unity Container) Issues

#### Code Generator Failure on ARM64
*   **Issue**: `CodeGeneration.Roslyn.Tool` (v0.7.63) produces **empty generated files** on macOS ARM64 (only `using` statements, no registration code). This causes `AutomaticTypeMapper` to fail silently.
*   **Symptom**: `System.StackOverflowException` during `ResolveAll<IGameInitializer>()` with deep recursive calls in `Unity.Processors.MemberProcessor`.
*   **Root Cause**: Without generated registration code, types are not registered as singletons, and certain constructor parameters (like `List<IGameComponent>`) are not registered at all.

#### ManualRegistrar Workaround
*   **Solution**: Created `EndlessClient/ManualRegistrar.cs` that:
    1. Reflectively scans assemblies for `[MappedType]` and `[AutoMappedType]` attributes
    2. Registers types with correct lifetime managers via reflection on Unity's `IUnityContainer`
    3. Explicitly registers `List<IGameComponent>` as an empty instance
*   **Integration**: Called from `GameRunnerBase.SetupDependencies()` after accessing the container via `_registry.GetType().GetProperty("UnityContainer")`.

#### Unity LifetimeManager Gotchas
*   **Critical**: Unity `LifetimeManager` instances are **stateful and cannot be reused** across multiple `RegisterType` calls. Each registration requires a new `Activator.CreateInstance()`.
*   **Error Message**: "The lifetime manager is already registered. WithLifetime managers cannot be reused."

#### Multiple Attribute Handling
*   **Issue**: Some types have multiple `[MappedType]` attributes. Using `GetCustomAttribute<T>()` throws `AmbiguousMatchException`.
*   **Fix**: Use `GetCustomAttributes<T>()` and iterate over all attributes.

### macOS Runtime Path Resolution

#### App Bundle vs Development Mode
*   **Issue**: `PathResolver.GetPath()` returns `Contents/Resources/{path}` on macOS, which is correct for packaged `.app` bundles but fails when running via `dotnet run`.
*   **Fix**: Modified `PathResolver.cs` to fallback to direct paths when `Contents/Resources` doesn't exist:
    ```csharp
    var bundlePath = Path.Combine(ResourcesRoot, inputPath);
    if (Directory.Exists(Path.GetDirectoryName(bundlePath)) || File.Exists(bundlePath))
        return bundlePath;
    return inputPath;  // Fallback for development mode
    ```

#### Content Root Directory
*   **Issue**: MonoGame's `ContentManager` on macOS prepends `Contents/Resources` to all content paths.
*   **Fix**: In `EndlessClientInitializer.cs`, detect development mode and use absolute path:
    ```csharp
    if (!Directory.Exists(Path.Combine("Contents", "Resources", "ContentPipeline")))
        contentDir = Path.GetFullPath("ContentPipeline");
    ```

### Cross-Platform File Paths

#### Path Separator Compatibility
*   **Issue**: Content path constants in `ContentProvider.cs` used Windows-style backslashes (`ChatBubble\TL`).
*   **Symptom**: `FileNotFoundException` on macOS/Linux with paths like `ChatBubble\TL.xnb`.
*   **Fix**: Always use forward slashes (`/`) in content paths - MonoGame and .NET handle conversion:
    ```csharp
    public const string ChatTL = @"ChatBubble/TL";  // Cross-platform
    ```

### Running the Application on macOS

```bash
# Build
dotnet build --no-restore EndlessClient/EndlessClient.csproj -p:CheckEolWorkloads=false

# Run from output directory
cd bin/Debug/client/net10.0/osx-arm64
dotnet EndlessClient.dll
```

*   **MIDI Warning**: "Unable to initialize the midi sound system" is expected if MIDI output is not configured - background music won't play but the game runs fine.

### Persistent Game Components (IGameComponent)

#### DispatcherGameComponent Hanging Issue
*   **Issue**: Clicking "Play Game" connects to server but UI doesn't transition to login screen.
*   **Root Cause**: `DispatcherGameComponent.InvokeAsync()` hangs because the component's `Update()` method is never called. The component wasn't being added to the game's component collection.
*   **Why It Happens**: The `ManualRegistrar` registered `List<IGameComponent>` as an empty list, but `EndlessClientInitializer.Initialize()` expected this list to contain `DispatcherGameComponent` and `PacketHandlerGameComponent`.

#### Fix Applied
*   **Solution**: Modified `EndlessClientInitializer` to directly inject `DispatcherGameComponent` and `PacketHandlerGameComponent` as constructor parameters, then add them to `_game.Components` in `Initialize()`:
    ```csharp
    _game.Components.Add(_dispatcherGameComponent);
    _game.Components.Add(_packetHandlerGameComponent);
    ```
*   **Files Modified**: `EndlessClient/Initialization/EndlessClientInitializer.cs`

### Config File Resolution on macOS

#### Settings File Location
*   **Behavior**: On macOS, `PathResolver.GetModifiablePath()` returns `~/.endlessclient/config/settings.ini` if it exists, otherwise falls back to the local `config/settings.ini` in the current directory.
*   **Development Workflow**: Edit `ClientAssets/config/settings.ini`, which gets copied to the build output directory. The app uses this file when no home directory config exists.
*   **User Override**: Users can create `~/.endlessclient/config/settings.ini` to override the default settings.

#### Common Issue
*   **Symptom**: Client uses old settings even after editing `ClientAssets/config/settings.ini`.
*   **Cause**: Old `~/.endlessclient/config/settings.ini` file exists from a previous install.
*   **Fix**: Delete the home directory config: `rm ~/.endlessclient/config/settings.ini`
