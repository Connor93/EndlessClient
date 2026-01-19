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

## 6. Player Commands

### Command System Architecture
Player commands (prefixed with `#`) are handled client-side via the `IPlayerCommand` interface:

```csharp
public interface IPlayerCommand
{
    string CommandText { get; }  // e.g., "item"
    bool Execute(string parameter);
}
```

*   **Registration**: Commands use `[AutoMappedType]` attribute for automatic DI registration.
*   **Location**: `EOLib/Domain/Chat/Commands/`
*   **Handler**: `LocalCommandHandler` dispatches commands to matching `IPlayerCommand` implementations.

### Item Lookup Command (`#item`)
*   **Files Created**:
    *   `EOLib/Domain/Chat/Commands/ItemCommand.cs` - Command handler
    *   `EOLib/Domain/Interact/IItemInfoDialogActions.cs` - Dialog interface
    *   `EndlessClient/Dialogs/ItemInfoDialog.cs` - Extends `ScrollingListDialog`
    *   `EndlessClient/Dialogs/Factories/ItemInfoDialogFactory.cs` - Creates dialogs with item graphics
    *   `EndlessClient/Dialogs/Actions/ItemInfoDialogActions.cs` - Display logic

*   **Key Patterns**:
    *   Commands in `EOLib` cannot access UI directly; use interface (`IItemInfoDialogActions`) implemented in `EndlessClient`.
    *   Item graphics loaded via `GFXTypes.Items` with formula: `2 * item.Graphic - 1`.
    *   Dialogs extend `ScrollingListDialog` and use `ListDialogItem` for text (not custom SpriteBatch drawing).
    *   Search results dialog uses `SetPrimaryClickAction()` for clickable items.

*   **ActiveDialogRepository**: Add new dialog types to `IActiveDialogProvider`, `IActiveDialogRepository`, and `ActiveDialogRepository` class (property + ActiveDialogs list + Dispose).

## 7. Custom Client-Server Packets

### SDK Packet Factory Limitations
The `Moffat.EndlessOnline.SDK` contains a `PacketFactory` that creates packet objects from raw bytes. It only recognizes packets defined in the SDK's `Protocol.Net.Server` namespace.

*   **Problem**: If you need to send/receive custom packets not in the SDK, the factory returns `Option.None` and the packet is silently dropped.
*   **Problem 2**: If the SDK HAS a packet type for your Family+Action but with a different format than your server sends, the SDK creates its packet type (wrong data) and the handler fails `IsHandlerFor()` check because types don't match.

### Solution: Use Unused Packet Actions
To avoid conflicts with SDK-defined packets:

1. **Choose an unused action number** - Check the SDK's packet definitions; action 19 (DIALOG) is unused for ITEM family.
2. **Server**: Use `static_cast<PacketAction>(19)` in PacketBuilder.
3. **Client**: Add fallback in `PacketEncoderService.Decode()`:
   ```csharp
   var result = _packetFactory.Create(decodedBytes);
   if (result.HasValue)
       return result;
   return TryCreateCustomPacket(decodedBytes);  // Fallback for custom packets
   ```

### Etheos Server Packet Format
The etheos server's `PacketBuilder::Get()` produces this format:

```
[length_low][length_high][action][family][payload...]
```

*   **Length**: 2 bytes (EO-encoded), stripped by client during receive
*   **Action**: 1 byte (sent first, read as byte 0 after length stripped)
*   **Family**: 1 byte (byte 1 after length stripped)  
*   **Payload**: Starts at byte 2 (NO sequence bytes on server→client!)

**Critical**: Server→client packets do NOT include sequence bytes. Only client→server packets have them. When decoding custom server packets, use `payloadStart = 2`, not 3+.

### EO Number Encoding (etheos)
The etheos server uses `PacketProcessor::ENumber()` for encoding numbers:

*   **Char** (1 byte): `value + 1` (to avoid null bytes)
*   **Short** (2 bytes): Low byte = `(value % 254) + 1`, High byte = `(value / 254) + 1`
*   **FE (254)**: Break/delimiter character, represents value 253 or field separator
*   **00 (0)**: Encoded as 128 in some contexts

The SDK's `EoReader.GetShort()` uses the same decoding, so values from etheos are compatible.

### Custom Packet Implementation Pattern

**Server (etheos) - Item.cpp**:
```cpp
PacketBuilder reply(PACKET_ITEM, static_cast<PacketAction>(19), 128);
reply.AddShort(item_id);
reply.AddChar(num_sources);
// ... data
character->Send(reply);
```

**Client - Register handler with PacketHandlerFinder**:
```csharp
[AutoMappedType]
public class ItemSourceHandler : InGameOnlyPacketHandler<ItemSourceResponsePacket>
{
    public override PacketFamily Family => PacketFamily.Item;
    public override PacketAction Action => (PacketAction)19;  // Custom action
}
```

**Client - Custom packet type**:
```csharp
public class ItemSourceResponsePacket : IPacket
{
    public PacketFamily Family => PacketFamily.Item;
    public PacketAction Action => (PacketAction)19;
    
    public void Deserialize(EoReader reader) { /* parse payload */ }
}
```

**Client - Fallback decoder in PacketEncoderService**:
```csharp
private Option<IPacket> TryCreateCustomPacket(byte[] data)
{
    if (data[1] == (byte)PacketFamily.Item && data[0] == 19)
    {
        int payloadStart = 2;  // NO sequence bytes on server packets!
        var payloadData = new byte[data.Length - payloadStart];
        Array.Copy(data, payloadStart, payloadData, 0, data.Length - payloadStart);
        
        var packet = new ItemSourceResponsePacket();
        packet.Deserialize(new EoReader(payloadData));
        return Option.Some<IPacket>(packet);
    }
    return Option.None<IPacket>();
}
```

## 8. Dialog Types and Sizing

### ScrollingListDialog DialogType Enum
The `DialogType` enum controls dialog size and features:

| Type | Size | Scroll | Use Case |
|------|------|--------|----------|
| `Shop`, `FriendIgnore`, `Locker`, etc. | Large | Yes | Shops, inventories |
| `Help` | Large (alt bg) | Yes | Help screens |
| `Chest` | Large | No | Chest contents |
| `QuestProgressHistory`, `Board` | Medium | Yes | Quest progress, boards |
| `NpcQuestDialog` | Small | Yes | NPC dialogs |
| `BankAccountDialog` | Small | No | Bank info |

*   **Recommendation**: Use `QuestProgressHistory` for medium-sized info dialogs (like item info).
*   **Item Graphics**: Draw over the dialog using `OnDrawControl()` with `_spriteBatch.Begin()`/`End()`.

## 9. NPC Graphics Formula

When loading NPC sprites from `GFXTypes.NPC`, the formula is:
```
resourceId = (npc.Graphic - 1) * 40 + frameOffset
```

**Frame offsets** (based on direction Down/Right vs Up/Left):
| Frame | Down/Right | Up/Left |
|-------|------------|---------|
| Standing | 1 | 3 |
| Standing2 | 2 | 4 |
| Walk1 | 5 | 9 |
| Walk2 | 6 | 10 |
| Walk3 | 7 | 11 |
| Walk4 | 8 | 12 |
| Attack1 | 13 | 15 |
| Attack2 | 14 | 16 |

*   **For dialogs**: Use `(npc.Graphic - 1) * 40 + 1` for the standing south frame.
*   **Reference**: See `NPCSpriteSheet.GetNPCTexture()` for the complete implementation.

### NPC Lookup Command (`#npc`)
*   **Files Created**:
    *   `EOLib/Domain/Chat/Commands/NpcCommand.cs` - Command handler
    *   `EOLib/Domain/Interact/INpcInfoDialogActions.cs` - Dialog interface
    *   `EOLib/Domain/Interact/NpcSourceRepository.cs` - Data model for drops/shops/crafts/spawns
    *   `EOLib/Net/Packets/NpcSourceRequestPacket.cs` - Request packet
    *   `EOLib/PacketHandlers/Npcs/NpcSourceHandler.cs` - Packet handler (action 20)
    *   `EndlessClient/Dialogs/NpcInfoDialog.cs` - Extends `ScrollingListDialog`
    *   `EndlessClient/Dialogs/Factories/NpcInfoDialogFactory.cs` - Creates dialogs
    *   `EndlessClient/Dialogs/Actions/NpcInfoDialogActions.cs` - Display logic

*   **Custom Packet Pattern**: Uses `PacketFamily.Npc` with action 20 (not in SDK)
    *   Request packet: `NpcSourceRequestPacket` implements `IPacket`
    *   Response handled via `PacketEncoderService.TryDecodePacket()` fallback
    *   Must add decoding case to `PacketEncoderService.cs` for custom server responses

*   **Dialog Tracking**: Added `NpcInfoDialog` property to `ActiveDialogRepository`

## 10. Custom Packet Handling Pattern

For packets using actions not defined in the SDK:

1. **Client Request**: Create a custom `IPacket` class with `Serialize()`:
   ```csharp
   public class MyRequestPacket : IPacket {
       public PacketFamily Family => PacketFamily.XXX;
       public PacketAction Action => (PacketAction)NN; // Unused action number
   }
   ```

2. **Client Response**: Add case to `PacketEncoderService.TryDecodePacket()`:
   ```csharp
   if (family == PacketFamily.XXX && action == NN) {
       var packet = new MyResponsePacket();
       packet.Deserialize(reader);
       return Option.Some<IPacket>(packet);
   }
   ```

3. **Handler**: Create handler extending `InGameOnlyPacketHandler<T>`:
   ```csharp
   public override PacketAction Action => (PacketAction)NN;
   ```

**Current Custom Packets**:
| Family | Action | Purpose |
|--------|--------|---------|
| Item | 19 | Item source lookup |
| Npc | 20 | NPC source lookup |

## 11. Macro Panel Patterns

### Panel Creation Architecture
New HUD panels follow this wiring pattern:

1. **Enum**: Add identifier to `HudControlIdentifier.cs`
2. **Factory Interface**: Add method to `IHudPanelFactory.cs`
3. **Factory Implementation**: Implement in `HudPanelFactory.cs`
4. **Controls Factory**: Wire up in `HudControlsFactory.CreateHud()` dictionary and `CreateStatePanel()` switch
5. **Button Controller**: Add `Click<Panel>()` to `IHudButtonController.cs` and `HudButtonController.cs`
6. **Control Provider**: Add to `HudPanels` list in `IHudControlProvider.cs`
7. **State Handler**: Update `DoHudStateChangeClick()` in `HudControlsFactory.cs`

### DraggablePanelItem Pattern
For draggable items within panels:

```csharp
public class MyPanelItem : DraggablePanelItem<TData>
{
    // GridArea defines bounds for drag validity (absolute coords)
    protected override Rectangle GridArea => new Rectangle(
        _parentContainer.DrawPositionWithParentOffset.ToPoint() + new Point(x, y),
        new Point(width, height));

    // EventArea determines mouse hit testing
    public override Rectangle EventArea => IsDragging ? DrawArea : DrawAreaWithParentOffset;
}
```

### Macro Slot Storage
Macro slots use `Option<MacroSlot>[]` with file persistence:

```ini
# config/macros.ini format:
[host:account]
CharacterName.0=S:1    # Slot 0: Spell ID 1
CharacterName.8=I:378  # Slot 8 (Shift+F1): Item ID 378
```

*   **S prefix**: Spell
*   **I prefix**: Item

### F-Key Handling Priority
`FunctionKeyController.SelectSpell()` checks in this order:
1. Macro slot (if assigned) - uses item or casts spell
2. Active Spells panel slot (fallback)

### Macro Spell Hotkey Activation
For `SpellTarget.Normal` spells (offensive spells requiring a target click):
1. `FunctionKeyController.HandleSpellCast()` sets:
   - `SpellSlotDataRepository.PreparedMacroSpellId = Option.Some(spellId)`
   - `SpellSlotDataRepository.SpellIsPrepared = true`
   - `SpellSlotDataRepository.SelectedSpellSlot = Option.None<int>()` (to avoid conflicting with slot system)
2. Plays `SoundEffectID.SpellActivate` and shows "X spell was selected" message
3. `MapInteractionController.LeftClick(ISpellTargetable)` checks `PreparedMacroSpellId` first, falling back to `SelectedSpellInfo` from spell slots
4. After casting, clears `PreparedMacroSpellId`, `SpellIsPrepared`, and `SelectedSpellSlot`

**Key Gotcha**: Without `PreparedMacroSpellId`, macro spells would only show the selection UI but never cast, because `SelectedSpellInfo` is derived from `SelectedSpellSlot` (the normal spell panel slot system).

### Cross-Panel Drag Support
For drag-and-drop between panels (e.g., Inventory→MacroPanel):

1. In source panel's `HandleItemDoneDragging`, detect target panel:
   ```csharp
   var macroPanel = _hudControlProvider.GetComponent<MacroPanel>(HudControlIdentifier.MacroPanel);
   if (macroPanel.MouseOver)
   {
       var mousePos = MouseExtended.GetState().Position.ToVector2();
       var targetSlot = macroPanel.GetSlotFromPosition(mousePos);
       if (targetSlot >= 0)
       {
           macroPanel.AcceptItemDrop(item.ItemID, targetSlot);
           e.RestoreOriginalSlot = true;  // Keep item in original panel
           return;
       }
   }
   ```

2. Target panel provides `Accept*Drop()` methods:
   - `AcceptItemDrop(int itemId, int targetSlot)`
   - `AcceptSpellDrop(int spellId, int targetSlot)`

3. Target panel updates its repository and refreshes display

### MacroPanel Grid Layout Constants
The MacroPanel uses resource 72 background with a 4x2 grid on each of two panels. **Final values after tuning**:

```csharp
private const int LeftPanelX = 16;      // Left edge of left panel grid
private const int RightPanelX = 230;    // Left edge of right panel grid
private const int GridStartY = 26;      // Top edge of both grids
private const int SlotWidth = 52;       // Width per slot
private const int SlotHeight = 45;      // Height per slot
```

*   **Cell Padding**: `GetSlotPosition()` adds 5px padding in both X and Y to center items within cells.
*   **Two-Panel Issue**: The left and right panels require different X offsets; `RightPanelX` is NOT simply `LeftPanelX + (4 * SlotWidth)`.

### OK Button Positioning
The MacroPanel uses an OK button (via `IEODialogButtonService`) instead of an X close button:

```csharp
_closeButton = new XNAButton(dialogButtonService.SmallButtonSheet,
    new Vector2(BackgroundImage.Width / 2 - 40, BackgroundImage.Height - 42),
    dialogButtonService.GetSmallDialogButtonOutSource(SmallButton.Ok),
    dialogButtonService.GetSmallDialogButtonOverSource(SmallButton.Ok));
```

*   **Event Handler**: Use `OnMouseDown` (not `OnClick`) to prevent the panel from instantly closing when reopened:
    ```csharp
    _closeButton.OnMouseDown += (_, _) => { Visible = false; };
    ```

### Drag-from-Inventory Fix
When handling drag-and-drop from InventoryPanel to MacroPanel, the MacroPanel check **must be positioned BEFORE** other drop target checks:

```csharp
private void HandleItemDoneDragging(...)
{
    ResetSlotMap(...);
    
    // Check MacroPanel FIRST (before map drop, other button checks)
    var macroPanel = _hudControlProvider.GetComponent<MacroPanel>(...);
    if (macroPanel.Visible && macroPanel.MouseOver)
    {
        // Handle drop...
        return;
    }
    
    // Then other checks (map drop, junk button, dialogs, etc.)
}
```

*   **Visibility Check**: Include `macroPanel.Visible` for safety, though MacroPanel must be visible for user to target it.

## 12. XNAControls Collection Modification Bug

### The Problem
Clicking on NPC dialogs (like the wiseman/skillmaster) can cause a crash:
```
InvalidOperationException: Collection was modified; enumeration operation may not execute.
   at XNAControls.Input.InputTargetFinder.GetMouseButtonEventTargetControl
```

### Root Cause
`XNAControls` (NuGet package v2.3.1) iterates over the control collection during mouse click handling. If **any** control is added or removed during this iteration, .NET throws `InvalidOperationException`.

Common triggers:
- Dialog `Close()` during click handler
- `ClearItemList()` / `RemoveFromList()` removing controls
- `ShowDialog()` adding new controls
- Context menu disposal

### Solution: Deferred Execution
Wrap control modifications with `DispatcherGameComponent.Invoke()` to defer them to the next frame:

```csharp
// Instead of:
ClearItemList();

// Do:
DispatcherGameComponent.Invoke(() => 
{
    foreach (var item in itemsToDispose)
    {
        item.SetControlUnparented();
        item.Dispose();
    }
});
```

**Files Modified**:
- `ClickDispatcher.cs` - Deferred `ShowNPCDialog` and context menu disposal
- `SkillmasterDialog.cs` - Deferred `SetState` in click handlers
- `ScrollingListDialog.cs` - Deferred `ClearItemList`, `RemoveFromList`, link click actions

### Safety Net: Exception Handler
Added catch in `EndlessGame.Update()` for any remaining edge cases:

```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("Collection was modified"))
{
    System.Diagnostics.Debug.WriteLine($"[XNAControls] Collection modification: {ex.Message}");
    // Operation retries next frame
}
```

**Key Pattern**: Any code that modifies controls during a click event handler should use `DispatcherGameComponent.Invoke()`.

## 13. Input Handler Architecture

### Handler Chain Pattern
Keyboard input is processed through a chain of `IInputHandler` implementations created by `UserInputHandler`:

```
UserInputHandler
  ↳ ArrowKeyHandler     (movement via arrows + WASD)
  ↳ ControlKeyHandler   (attack via Ctrl + Space)
  ↳ FunctionKeyHandler  (F1-F12 macros/spells)
  ↳ NumPadHandler       (emotes)
  ↳ PanelShortcutHandler (panel toggles, resizable mode only)
```

### Adding Dependencies to Input Handlers
To inject new dependencies into an input handler:

1. **Handler**: Add field, constructor parameter, store dependency
2. **UserInputHandler**: Add constructor parameter, pass to handler instantiation
3. **UserInputHandlerFactory**: Add field, constructor parameter, pass to `CreateUserInputHandler()`

*Example*: Adding `IConfigurationProvider` and `IHudControlProvider` to check game state during input handling.

### Conditional Keyboard Override Pattern (WASD Movement)
When keyboard keys need dual-purpose (game action vs typing), use this pattern:

**In Input Handler** (e.g., `ArrowKeyHandler`, `ControlKeyHandler`):
```csharp
// Only use key for game action if:
// 1. Feature is enabled in config
// 2. Shift is NOT held (allows typing capitals)
// 3. Chat box is empty (player hasn't started typing)
if (_configurationProvider.WASDMovement && !IsShiftHeld() && !IsChatActive())
{
    if (IsKeyHeld(Keys.W) && _arrowKeyController.MoveUp())
        return Option.Some(Keys.W);
}

private bool IsChatActive()
{
    return _hudControlProvider.IsInGame &&
           _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox).Text.Length > 0;
}
```

**In ChatTextBox.IsSpecialInput()** (filter keys from text input):
```csharp
// Same conditions: filter WASD/Space from typing when used for movement/attack
if (_configurationProvider.WASDMovement && modifiers != KeyboardModifiers.Shift && Text.Length == 0)
{
    if (k == Keys.W || k == Keys.A || k == Keys.S || k == Keys.D || k == Keys.Space)
        return true;  // Don't type this key
}
```

**Files Involved**:
- `ArrowKeyHandler.cs` - WASD movement
- `ControlKeyHandler.cs` - Spacebar attack
- `ChatTextBox.cs` - Filter keys from typing
- `UserInputHandler.cs` / `UserInputHandlerFactory.cs` - Dependency wiring
- `settings.ini` - `WASDMovement=true/false` toggle

