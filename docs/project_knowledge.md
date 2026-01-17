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
