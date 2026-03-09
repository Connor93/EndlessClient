# Myra UI Migration — Technical Documentation

> **Last Updated**: February 23, 2026  
> **Status**: Active Migration — all dialogs complete, all 7 HUD windows complete, 7 of 11 HUD panels migrated

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Migration Progress](#migration-progress)
- [Technical Lessons Learned](#technical-lessons-learned)
- [Drag-and-Drop Bridge](#drag-and-drop-bridge)
- [Remaining Work](#remaining-work)
- [Known Gotchas](#known-gotchas)

---

## Overview

### Goal

Replace the client's three-layer UI system (XNA GFX-based → CodeDrawn → Myra) with a unified Myra UI framework using the [Myra](https://github.com/rds1983/Myra) library. This gives us:

- **Theme-driven styling** via Myra stylesheets (currently "DarkParchment") instead of pixel-pushing
- **Auto-layout** — no more manual `Rectangle` positioning for every element
- **Native window behavior** — built-in dragging, resizing, modal overlays, focus management
- **Scale-aware rendering** — Myra's `Desktop.Scale` + `BoundsFetcher` handles resolution independence
- **Faster iteration** — adding a new dialog means defining widgets, not drawing sprites

### The Three UI Layers (Historical)

The client evolved through three UI generations, all still present in the codebase:

| Layer | Implementation | Example | Active? |
|-------|---------------|---------|---------|
| **GFX** (`UIMode.Gfx`) | XNA `SpriteBatch` drawing GFX texture atlases. Original EO client look. | `PaperdollDialog`, `ShopDialog` | Legacy — used only when `UIMode=Gfx` |
| **CodeDrawn** (`UIMode.Code`) | Custom `CodeDrawnDialog` base class rendering with `SpriteBatch` primitives | `CodeDrawnPaperdollDialog`, `CodeDrawnShopDialog` | Intermediate — used when `UIMode=Code` |
| **Myra** (`UIMode != Gfx`) | Myra `Window` widgets managed by `MyraDialogAdapter` | `MyraPaperdollDialog`, `MyraShopDialog` | **Active default** — used when `UIMode != Gfx` |

### Configuration

`UIMode` is set in the client's configuration (`UIMode` enum in `EOLib.Config/UIMode.cs`):

```csharp
public enum UIMode
{
    Gfx,   // Traditional GFX texture-based UI
    Code,  // Procedurally-drawn code-based UI
    Myra   // Myra UI framework (current default for non-Gfx)
}
```

Factory classes check `_configProvider.UIMode != UIMode.Gfx` to decide whether to create a Myra dialog or fall back to the XNA/CodeDrawn version.

---

## Architecture

### Core Myra Infrastructure

Located in `EndlessClient/UI/Myra/`:

| File | Purpose |
|------|---------|
| `MyraUIManager.cs` | Singleton. Owns the Myra `Desktop`, handles `Initialize()`, `Render()`, scale/offset via `Desktop.Scale` and `BoundsFetcher`. Also provides `GetLogicalMousePosition()` helper and `PostRenderOverlay` callback. |
| `MyraDialogAdapter.cs` | **Base class** bridging Myra `Window` to `IXNADialog`/`IXNAControl`. Handles lifecycle (`Show`, `Close`, `Dispose`), `ShowDialog()` via `TaskCompletionSource`, and `MouseOver` hit-testing for drag-and-drop. |
| `MyraStylesheetProvider.cs` | Builds and applies the DarkParchment Myra stylesheet (colors, fonts, spacing) |
| `MyraFontProvider.cs` | Provides SpriteFont instances for Myra dialogs to use |

### Dialog Class Hierarchy

```
IXNADialog (interface)
├── XNADialog (XNA base — GFX dialogs)
│   ├── PaperdollDialog, ShopDialog, ChestDialog, ...
│   └── BaseEODialog → CodeDrawnDialog (CodeDrawn base)
│       ├── CodeDrawnPaperdollDialog, CodeDrawnShopDialog, ...
│       └── CodeDrawnScrollingListDialog, CodeDrawnGridLockerDialog, ...
│
└── MyraDialogAdapter (Myra base — bridges Window to IXNADialog)
    ├── MyraPaperdollDialog (direct subclass for custom layouts)
    ├── MyraScrollingListDialog (scrollable list layout)
    │   ├── MyraLockerDialog, MyraQuestDialog, ...
    │   └── MyraBarberDialog, MyraChestDialog, ...
    ├── MyraGridDialog (tabbed grid layout with tooltips)
    │   ├── MyraShopDialog, MyraGridLockerDialog, MyraTradeDialog
    │   └── (inherits tab system, tooltip rendering, tile click handling)
    ├── MyraItemTransferDialog (amount input for transfers)
    ├── MyraEOMessageBox (standard OK/Cancel message boxes)
    ├── MyraTextInputDialog, MyraTextMultiInputDialog (text input)
    ├── MyraItemInfoDialog, MyraNpcInfoDialog (info popups)
    └── MyraCreateCharacterDialog (character creation UI)

MyraHudPanelBase (DrawableGameComponent + IHudPanel — bridges Myra Window to HUD panels)
    ├── MyraNewsPanel (scrollable server news)
    ├── MyraHelpPanel (static help text by topic)
    ├── MyraPassiveSpellsPanel (8×2 spell slot grid)
    ├── MyraStatsPanel (4-column stat grid with training buttons)
    ├── MyraOnlineListPanel (scrollable table with filter cycling)
    ├── MyraPartyPanel (member list with HP/TP bars, solo/party mode)
    └── MyraChatPanel (tabbed chat with embedded input, auto-focus on Enter)
```

### Render Pipeline

In `EndlessGame.Draw()`:

```
1. base.Draw(gameTime)        → XNA game world rendered to RenderTarget2D
2. Scaled blit to backbuffer  → RenderTarget drawn with PointClamp sampling
3. _myraUIManager.Render()    → All Myra windows/dialogs rendered
4. DrawPostScaleControls()    → Dragged items, chat bubbles (on TOP of Myra)
```

> **Critical**: `DrawPostScaleControls()` was moved AFTER `_myraUIManager.Render()` during this migration so that dragged XNA items render ON TOP of Myra dialog windows. This is essential for drag-to-deposit functionality.

### Coordinate Spaces

There are **three** coordinate spaces in play:

| Space | Origin | Usage |
|-------|--------|-------|
| **Screen pixels** | (0,0) = top-left of OS window | Raw `Mouse.GetState()`, `Desktop.MousePosition` |
| **Game-logical** | (0,0) = top-left of game content | `GetLogicalMousePosition()`, widget `Left`/`Top` positioning |
| **Myra layout** | BoundsFetcher rectangle origin | `Window.ActualBounds` (⚠️ can be stale — see [Known Gotchas](#known-gotchas)) |

`GetLogicalMousePosition()` converts screen → game-logical:
```csharp
(rawMouse.X - renderOffset.X) / scaleFactor,
(rawMouse.Y - renderOffset.Y) / scaleFactor
```

---

## Migration Progress

### ✅ Dialogs with Myra Implementations (30 factories wired)

These dialogs have complete Myra replacements and are wired into their factory classes via `UIMode != Gfx` checks:

| Dialog | Myra Class | Factory | Status |
|--------|-----------|---------|--------|
| Paperdoll | `MyraPaperdollDialog` | `PaperdollDialogFactory` | ✅ Complete — tooltips, equip slots, drag-to-equip |
| Shop | `MyraShopDialog` | `ShopDialogFactory` | ✅ Complete — buy/sell/craft tabs, tooltips, drag-to-sell |
| Chest | `MyraChestDialog` | `ChestDialogFactory` | ✅ Complete — deposit/withdraw, drag-to-deposit |
| Locker (grid) | `MyraGridLockerDialog` | `LockerDialogFactory` | ✅ Complete — grid layout, drag-to-deposit |
| Locker (list) | `MyraLockerDialog` | `LockerDialogFactory` | ✅ Complete — scrolling list, take items |
| Trade | `MyraTradeDialog` | `TradeDialogFactory` | ✅ Complete — offer/remove items, drag-to-trade |
| Quest | `MyraQuestDialog` | `QuestDialogFactory` | ✅ Complete — NPC dialog flow |
| Barber | `MyraBarberDialog` | `BarberDialogFactory` | ✅ Complete — hairstyle/color selection |
| Create Character | `MyraCreateCharacterDialog` | `CreateCharacterDialogFactory` | ✅ Complete — preview, name input |
| Message Box | `MyraEOMessageBox` | `EOMessageBoxFactory` | ✅ Complete — OK/Cancel/Yes/No prompts |
| Item Transfer | `MyraItemTransferDialog` | `ItemTransferDialogFactory` | ✅ Complete — amount input for deposits |
| Text Input | `MyraTextInputDialog` | `TextInputDialogFactory` | ✅ Complete — single-line text entry |
| Multi Text Input | `MyraTextMultiInputDialog` | `TextInputMultiDialogFactory` | ✅ Complete — multi-field text entry |
| Item Info | `MyraItemInfoDialog` | `ItemInfoDialogFactory` | ✅ Complete — item stats popup |
| NPC Info | `MyraNpcInfoDialog` | `NpcInfoDialogFactory` | ✅ Complete — NPC stats popup |
| Bank Account | `MyraBankAccountDialog` | `BankAccountDialogFactory` | ✅ Complete — deposit/withdraw gold, locker upgrade |
| Skillmaster | `MyraSkillmasterDialog` | `SkillmasterDialogFactory` | ✅ Complete — learn/forget/reset skills, requirements display |
| Friend/Ignore List | `MyraFriendIgnoreListDialog` | `FriendIgnoreListDialogFactory` | ✅ Complete — add/remove, online highlighting, save to file |
| Guild | `MyraGuildDialog` | `GuildDialogFactory` | ✅ Complete — 15-state machine, back-stack, guild data polling |
| Board | `MyraBoardDialog` | `BoardDialogFactory` | ✅ Complete — 3-state post/reply with text editors, data polling |
| Help | (factory-only) | `HelpDialogFactory` | ✅ Complete — text items with link actions, no dedicated dialog class |
| Quest Status | `MyraQuestStatusDialog` | `QuestStatusDialogFactory` | ✅ Complete — progress/history toggle, quest data polling |
| Session Exp | `MyraSessionExpDialog` | `SessionExpDialogFactory` | ✅ Complete — 8-row stats grid layout |
| Innkeeper | `MyraInnkeeperDialog` | `InnkeeperDialogFactory` | ✅ Complete — 4-state citizen menu, chained text inputs |
| Law | `MyraLawDialog` | `LawDialogFactory` | ✅ Complete — 4-state marriage/divorce menu |
| Jukebox | `MyraJukeboxDialog` | `JukeboxDialogFactory` | ✅ Complete — song browsing, play with gold, dynamic title |
| Bard | `MyraBardDialog` | `BardDialogFactory` | ✅ Complete — 12×3 button grid, tick-based cooldown |
| Book | `MyraBookDialog` | `BookDialogFactory` | ✅ Complete — character info grid + quest list, paperdoll polling |
| Change Password | `MyraChangePasswordDialog` | `ChangePasswordDialogFactory` | ✅ Complete — 4 text inputs, validation, pre-login |
| Game Loading | `MyraGameLoadingDialog` | `GameLoadingDialogFactory` | ✅ Complete — SetState/CloseDialog API, progress states |
| Account Progress | `MyraProgressDialog` | `CreateAccountProgressDialogFactory` | ✅ Complete — auto-closing progress bar, cancel |
| Account Warning | `MyraScrollingMessageDialog` | `CreateAccountWarningDialogFactory` | ✅ Complete — scrollable text with OK button |

### ✅ All Dialogs Complete

All dialog migrations are complete. `SearchResultsDialog` has been migrated via `MyraSearchResultsDialog` + `ISearchResultsDialog`.

> **Note**: `ScrollingListDialog` (base class) is already covered by `MyraScrollingListDialog`. `BardDialog` (XNA) is covered by `MyraBardDialog`. These are not truly "remaining" — their Myra paths exist and are wired.

### HUD Panels — Migration Progress

HUD panels bridge Myra `Window` to `IHudPanel` via `MyraHudPanelBase`.

| Panel | Myra Class | Status | Notes |
|-------|-----------|--------|-------|
| NewsPanel | `MyraNewsPanel` | ✅ Complete | Scrollable server news, gold header, auto-refresh |
| HelpPanel | `MyraHelpPanel` | ✅ Complete | Static help text organized by topic |
| PassiveSpellsPanel | `MyraPassiveSpellsPanel` | ✅ Complete | 8×2 spell slot grid |
| StatsPanel | `MyraStatsPanel` | ✅ Complete | 4-column grid (Basic/Combat/Info/Resources), stat training buttons |
| OnlineListPanel | `MyraOnlineListPanel` | ✅ Complete | Scrollable table (Name/Title/Guild/Class), filter cycling (All/Friends/Admins/Party/Guild), count label |
| PartyPanel | `MyraPartyPanel` | ✅ Complete | Solo view: HP/TP bars with numbers, stat/skill points, weight. Party view: member HP bars with %, leader ★, remove/leave buttons |
| ChatPanel | `MyraChatPanel` | ✅ Complete | Tabbed chat (scr/glb/grp/sys/PM×2), embedded text input with Enter-to-send, auto-focus on Enter, DarkParchment themed |
| InventoryPanel | — | ❌ Not started | Contains `DraggablePanelItem` drag-and-drop system |
| ActiveSpellsPanel | — | ❌ Not started | Active spell bar |
| MacroPanel | — | ❌ Not started | Macro bar with drag-drop |
| SettingsPanel | — | ❌ Not started | Settings toggles |

### HUD Windows (Not Yet Migrated to Myra)

These are **standalone floating windows** in `HUD/Windows/`. They all extend `DraggableHudPanel` (or `XNAControl`) and implement `IZOrderedWindow`. They are CodeDrawn-only — no GFX or Myra variants exist:

| Window | Class | Myra Class | Status |
|--------|-------|------------|--------|
| Achievements | `CodeDrawnAchievementWindow` | `MyraAchievementWindow` | ✅ Complete |
| Bounty Tracker | `CodeDrawnBountyTrackerWindow` | `MyraBountyTrackerWindow` | ✅ Complete |
| EXP Tracker | `CodeDrawnExpTrackerWindow` | `MyraExpTrackerWindow` | ✅ Complete |
| Guild Panel | `CodeDrawnGuildPanel` | `MyraGuildPanel` | ✅ Complete |
| Guild Info | `CodeDrawnGuildInfoWindow` | `MyraGuildInfoWindow` | ✅ Complete |
| Quest Tracker | `CodeDrawnQuestTrackerWindow` | `MyraQuestTrackerWindow` | ✅ Complete |
| Quest Window | `CodeDrawnQuestWindow` | `MyraQuestWindow` | ✅ Complete |

> **Note**: All HUD windows now have Myra implementations wired into their factory classes via `UIMode != Gfx` checks. The `WindowZOrderManager` integration challenge was resolved by using Myra's built-in window ordering.

### Radar MiniMap (Special Case)

The radar minimap (`Rendering/Map/RadarMiniMapRenderer.cs`) is a **540-line `DrawableGameComponent`** — not a dialog. It does all rendering via raw `SpriteBatch` calls:

| Aspect | Detail |
|--------|--------|
| **Class** | `RadarMiniMapRenderer` extends `DrawableGameComponent` |
| **Size** | 540 lines |
| **Rendering** | Custom `SpriteBatch` with isometric diamond primitives, scissor clipping, `RenderTarget2D` terrain caching |
| **Features** | Isometric terrain map, color-coded tiles (walls, warps, doors, water, chests), entity dots (players, NPCs, bosses), directional player arrow, cardinal direction labels |
| **Dragging** | Custom drag handling via raw `Mouse.GetState()` with manual position/offset tracking |
| **Input** | Manual hit-testing against `IActiveDialogProvider` and `IHudControlProvider` panels |

**Migration approach**: This would be a **hybrid** — Myra can't do pixel-level isometric rendering. The recommended strategy:
1. **Panel chrome** → Myra `Window` (background, header, border, title, drag behavior — eliminates ~60 lines of manual drawing)
2. **Terrain/entity content** → Continue using `SpriteBatch` rendering into a `RenderTarget2D`, displayed as a Myra `Image` widget inside the Window
3. **Input handling** → Replace manual `Mouse.GetState()` drag logic with Myra's built-in Window dragging

> **Complexity**: High. While the chrome migration is straightforward, the terrain rendering, scissor clipping, and entity overlay are tightly coupled to `SpriteBatch` and cannot be replaced by Myra widgets.

---

## Technical Lessons Learned

### 1. `Window.ActualBounds` is Stale After Window Drag

**Problem**: When a user drags a Myra `Window` by its title bar, `Window.Left` and `Window.Top` are updated correctly, but `Window.ActualBounds` retains the **initial layout position** (e.g., `(10,10,...)`).

**Solution**: Use `Window.Left` / `Window.Top` for hit-testing, not `ActualBounds`:
```csharp
var bounds = new Rectangle(
    Window.Left,        // Tracks moved position
    Window.Top,         // Tracks moved position
    actual.Width,       // Size from ActualBounds is correct
    actual.Height);
```

### 2. `Desktop.MousePosition` is in Screen Pixels, Not Logical Coords

**Problem**: `Desktop.MousePosition` returns raw screen pixel coordinates, while Myra widget positions (`Left`, `Top`) are in game-logical coordinates. Using `Desktop.MousePosition` for hit-testing against widget bounds will always fail at non-1x scale.

**Solution**: Use `GetLogicalMousePosition()` (defined on `IMyraUIManager`) which correctly transforms `(rawMouse - renderOffset) / scaleFactor`.

### 3. Render Z-Order: Dragged Items Must Draw After Myra

**Problem**: `DrawPostScaleControls()` (which renders dragged `InventoryPanelItem` icons) was called BEFORE `_myraUIManager.Render()`, causing dragged items to appear BEHIND Myra dialogs.

**Solution**: Move `DrawPostScaleControls()` to AFTER `_myraUIManager.Render()` in `EndlessGame.Draw()`. Items now render on top of dialog windows.

### 4. Cross-Layer Drag-and-Drop Requires a Bridge

**Problem**: The inventory panel is XNA-based (`InventoryPanelItem` extends `DraggablePanelItem<EIFRecord>`), but drop targets are Myra dialogs. These are two separate UI frameworks that don't share input systems.

**Solution**: Bridge via the `HandleItemDoneDragging` method in `InventoryPanel.cs` — check `MouseOver` on `MyraDialogAdapter` instances stored in `ActiveDialogRepository`.

### 5. Tooltip Auto-Sizing: Remove `Wrap` and Fixed `Width`

**Problem**: Myra tooltip panels stretched to fill the available space when `Wrap = true` or a fixed `Width` was set.

**Solution**: Remove `Wrap` and explicit `Width`. Set `HorizontalAlignment` and `VerticalAlignment` to allow the panel to shrink-wrap to its content.

### 6. `IClientWindowSizeProvider` Dependency Cleanup

After implementing `GetLogicalMousePosition()` on `IMyraUIManager`, the `IClientWindowSizeProvider` dependency was removed from `MyraGridDialog`, `MyraShopDialog`, and `MyraGridLockerDialog` (and their factories), reducing coupling.

### 7. Modal Overlay Must Cover Letterbox Areas

**Problem**: Myra's built-in `EnableModalDarkening` only covers `Desktop.Bounds` (the game viewport), not the letterbox/pillarbox black bars around it.

**Solution**: Disable `MyraEnvironment.EnableModalDarkening` and draw a custom full-viewport dark overlay in `MyraUIManager.Render()` when any modal `Window` is active.

### 8. Drag-From-Anywhere via `DragHandle`

**Problem**: Myra `Window` only allows dragging from the title bar. Panels with hidden title bars (e.g., `MyraChatPanel`) or panels where users expect to drag from the content area become stuck.

**Solution**: Set `Window.DragHandle = Window` in the base class (`MyraHudPanelBase`) constructor. This makes the entire window surface draggable, not just the title bar. Applied globally to all Myra HUD panels.

### 9. Hiding the Window Title Bar

**Problem**: Some panels (e.g., chat panel) don't need a title bar, but Myra `Window` always renders one even when `Title` is empty.

**Solution**: After construction, collapse the title panel:
```csharp
if (Window.TitlePanel != null)
{
    Window.TitlePanel.Visible = false;
    Window.TitlePanel.Height = 0;
}
```
This preserves all other Window functionality (dragging, close interception, Desktop membership) while removing the visual title bar.

### 10. `IUIStyleProvider` for Theme Consistency

**Problem**: Hardcoded colors in individual panels create visual mismatches with the DarkParchment theme applied via the Myra stylesheet.

**Solution**: Inject `IUIStyleProvider` and use its color properties (e.g., `PanelBackground`, `InputBackground`, `TabActive`, `ChatDefault`) instead of ad-hoc `Color` constants. The `DarkParchmentStyleProvider` defines chat-specific colors (`ChatServer`, `ChatError`, `ChatPM`, etc.) alongside standard UI colors.

---

## Drag-and-Drop Bridge

### Architecture

The drag-and-drop bridge connects XNA `DraggablePanelItem` → Myra `MyraDialogAdapter`:

```
InventoryPanelItem (XNA)
    │ user releases mouse button
    ▼
DraggablePanelItem.StopDragging()
    │ fires DraggingFinishing event
    ▼
InventoryPanel.HandleItemDoneDragging()
    │ loops through ActiveDialogRepository.ActiveDialogs
    │ checks each dialog's MouseOver property
    ▼
MyraDialogAdapter.MouseOver (getter)
    │ uses GetLogicalMousePosition() for game-logical coords
    │ uses Window.Left/Top + ActualBounds.Width/Height for bounds
    ▼
Switch on dialog type → dispatch to appropriate controller method
```

### Supported Drop Targets

| Dialog Type | Action | Controller Method |
|-------------|--------|-------------------|
| `MyraPaperdollDialog` | Equip item | `_inventoryController.EquipItem()` |
| `MyraChestDialog` | Deposit in chest | `_inventoryController.DropItemInChest()` |
| `MyraLockerDialog` / `MyraGridLockerDialog` | Deposit in locker | `_inventoryController.DropItemInLocker()` |
| `MyraTradeDialog` | Offer in trade | `_inventoryController.TradeItem()` |
| `MyraShopDialog` | Auto-switch to sell tab + sell | `shopDialog.AcceptItemDrop()` |

### Shop Sell-on-Drop

`MyraShopDialog.AcceptItemDrop(int itemId)`:
1. Sets `ActiveTabIndex = 1` (sell tab)
2. Finds the matching `IShopItem` from `_sellItems`
3. Calls `TradeItem(sellItem, buying: false)` which shows the sell confirmation

---

## Remaining Work

### Phase 2: Remaining Dialogs ✅ COMPLETE

All dialog migrations are complete. 30 of 36 UI elements now have Myra implementations.

Remaining non-dialog items:
- `ScrollingListDialog` — Shared base class (already covered via Myra subclasses)
- `SearchResultsDialog` — CodeDrawn only, used for player/item search
- ~~`IniEditorDialog`~~ — Planned for removal

### Phase 3: HUD Windows Migration ✅ COMPLETE

All 7 CodeDrawn HUD Windows have been migrated to Myra:

**Batch A — Trackers + Search ✅ Complete**:
- `SearchResultsDialog` → `MyraSearchResultsDialog` + `ISearchResultsDialog`
- `BountyTrackerWindow` → `MyraBountyTrackerWindow` + `IBountyTrackerWindow`
- `QuestTrackerWindow` → `MyraQuestTrackerWindow` + `IQuestTrackerWindow`

**Batch B — Quest, Guild Info, EXP ✅ Complete**:
- `CodeDrawnQuestWindow` → `MyraQuestWindow`
- `CodeDrawnGuildInfoWindow` → `MyraGuildInfoWindow`
- `CodeDrawnExpTrackerWindow` → `MyraExpTrackerWindow`

**Batch C — High-Complexity ✅ Complete**:
- `CodeDrawnAchievementWindow` → `MyraAchievementWindow` + `IAchievementWindow`
- `CodeDrawnGuildPanel` → `MyraGuildPanel` + `IGuildPanel`

### Phase 4: HUD Panel Migration — In Progress

New infrastructure: `MyraHudPanelBase` bridges Myra `Window` to `IHudPanel` + `DrawableGameComponent`. Close button hides (via `Closing` + `e.Cancel = true`), not destroys. All factory methods return `IHudPanel`, with `IsPanelForRequestedState` updated to recognize Myra types. All panels are draggable from anywhere in the window via `DragHandle = Window` (global fix in `MyraHudPanelBase`).

**Batch 1 — Simple Display Panels ✅ Complete**:
- `MyraNewsPanel` — scrollable news with gold header, hidden at login
- `MyraHelpPanel` — static help text organized by topic
- `MyraPassiveSpellsPanel` — 8×2 spell slot grid placeholder

**Batch 2 — Data Display Panels ✅ Complete**:
- `MyraStatsPanel` — 4-column grid with stat training buttons
- `MyraOnlineListPanel` — scrollable table with All/Friends/Admins/Party/Guild filter cycling
- `MyraPartyPanel` — dual-mode: solo view (HP/TP/weight bars with numbers) or party view (member HP bars with %)

**Batch 3 — Interactive Panels** (not started):
- SettingsPanel, ActiveSpellsPanel

**Batch 4 — Drag-and-Drop Panels** (not started):
- MacroPanel, InventoryPanel — requires Myra-native drag system

**Batch 5 — ChatPanel ✅ Complete**:
- `MyraChatPanel` — tabbed chat with embedded text input, DarkParchment themed
  - **Tabs**: scr/glb/grp/sys + two PM tab slots, right-aligned at bottom, clickable Labels with hover effects
  - **Input**: Embedded `TextBox` with accent bar, Enter-to-send wired to `ChatController.SendChatAndClearTextBox()`
  - **Auto-focus**: Pressing Enter auto-focuses the chat input when no other widget has keyboard focus
  - **Title bar**: Hidden via `Window.TitlePanel.Visible = false` for a borderless look
  - **Theme**: All colors from `IUIStyleProvider` (DarkParchment) — panel bg, input, tabs, chat message colors
  - **Integration**: `ChatTextBoxActions` handles both `CodeDrawnChatPanel` and `MyraChatPanel` via safe `as` casting

### Phase 4½: UI Polish — Game-Style Theming

All Myra dialogs and windows currently use flat/plain Myra styling. This phase will overhaul the visual design to feel more like a game UI:

- **Background textures/borders** — Parchment, stone, or wood-style panel backgrounds instead of flat fills
- **Typography** — Stylized headers, pixel-fit fonts, drop shadows
- **Progress indicators** — Themed progress bars (XP bars, bounty trackers) with custom fills
- **Window chrome** — Custom title bars, close buttons, and border styling
- **Color palette** — Warm, inviting in-game colors vs. cold flat UI grays
- **Micro-animations** — Subtle hover effects, transitions, icon pulses
- **Consistency** — Unified look across all dialogs, windows, and trackers

> This can be done incrementally via Myra stylesheet updates (`MyraStylesheetProvider`) and per-widget styling without changing functionality.

### Phase 5: Cleanup

Once all dialogs, windows, and panels are Myra-based:
- Remove all `CodeDrawn*` dialog and window classes
- Remove `CodeDrawnDialog` base class and `DraggableHudPanel` where possible
- Remove `WindowZOrderManager` and `IZOrderedWindow`
- Simplify factory classes (remove `UIMode` branching)
- Consider removing `UIMode` enum entirely

---

## Known Gotchas

| Issue | Detail | Workaround |
|-------|--------|------------|
| `ActualBounds` stale after drag | `Window.ActualBounds` doesn't update after user drags the window title bar | Use `Window.Left`/`Window.Top` for position |
| `Desktop.MousePosition` in screen coords | NOT in game-logical coords | Use `GetLogicalMousePosition()` |
| `MouseOverPreviously` always false | `MyraDialogAdapter` stub returns `false` | Relies on `MouseOver` being sufficient for drop detection |
| Modal overlay coverage | Built-in Myra modal darkening doesn't cover letterbox regions | Custom full-viewport overlay in `MyraUIManager.Render()` |
| Tooltip sizing | `Wrap = true` or fixed `Width` causes stretch-to-fill | Remove both, use alignment for shrink-wrapping |
| Post-scale draw order | Must draw after Myra for correct z-ordering | `DrawPostScaleControls()` called after `_myraUIManager.Render()` |
| `TitleGrid` doesn't exist | Myra 1.5.10 uses `TitlePanel`, not `TitleGrid` | Use `Window.TitlePanel` to hide/collapse title bar |
| `IsFocused` doesn't exist on TextBox | Myra 1.5.10 uses `IsKeyboardFocused` | Use `TextBox.IsKeyboardFocused` and `SetKeyboardFocus()` |
| `HorizontalStackPanel` doesn't stretch children | TextBox in HorizontalStackPanel stays at minimum width | Use `Grid` with `ProportionType.Fill` column instead |

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `UI/Myra/MyraUIManager.cs` | Desktop management, render pipeline, `GetLogicalMousePosition()` |
| `UI/Myra/MyraDialogAdapter.cs` | Base bridge class, `MouseOver` implementation, lifecycle |
| `UI/Myra/MyraStylesheetProvider.cs` | DarkParchment theme definition |
| `Dialogs/MyraGridDialog.cs` | Tabbed grid layout with tooltip support |
| `Dialogs/MyraScrollingListDialog.cs` | Scrollable list layout |
| `Dialogs/ActiveDialogRepository.cs` | Tracks all open dialogs for drop targeting |
| `HUD/Panels/MyraHudPanelBase.cs` | Base class bridging Myra Window to IHudPanel for persistent HUD panels. Sets `DragHandle = Window` for drag-from-anywhere |
| `HUD/Panels/MyraChatPanel.cs` | Tabbed chat panel with embedded input, auto-focus, DarkParchment theme |
| `HUD/Chat/ChatTextBoxActions.cs` | Bridges chat input between CodeDrawn and Myra panels via safe `as` casting |
| `HUD/Panels/InventoryPanel.cs` | `HandleItemDoneDragging()` — drag-drop bridge |
| `HUD/Controls/DraggablePanelItem.cs` | XNA drag system base class |
| `HUD/HudStateActions.cs` | `IsPanelForRequestedState()` — maps panel type to InGameState |
| `GameExecution/EndlessGame.cs` | Render pipeline — draw order |
| `EOLib.Config/UIMode.cs` | UI mode enum |
