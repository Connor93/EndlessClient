using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Item;
using EOLib.Domain.Login;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnInventoryPanel : InventoryPanel
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _labelFont;

        private const int PanelWidth = 476;
        private const int PanelHeight = 118;
        private const int SlotWidth = 26;
        private const int SlotHeight = 26;
        // Items are positioned at (13 + 26*col, 9 + 26*row) per InventoryPanelItem.GetPosition
        private const int SlotsStartX = 13;
        private const int SlotsStartY = 9;

        public CodeDrawnInventoryPanel(INativeGraphicsManager nativeGraphicsManager,
                                       IInventoryController inventoryController,
                                       IStatusLabelSetter statusLabelSetter,
                                       IItemStringService itemStringService,
                                       IItemNameColorService itemNameColorService,
                                       IInventoryService inventoryService,
                                       IInventorySlotRepository inventorySlotRepository,
                                       IPlayerInfoProvider playerInfoProvider,
                                       ICharacterProvider characterProvider,
                                       ICharacterInventoryProvider characterInventoryProvider,
                                       IPubFileProvider pubFileProvider,
                                       IHudControlProvider hudControlProvider,
                                       IActiveDialogProvider activeDialogProvider,
                                       ISfxPlayer sfxPlayer,
                                       IConfigurationProvider configProvider,
                                       IUIStyleProvider styleProvider,
                                       IGraphicsDeviceProvider graphicsDeviceProvider,
                                       IContentProvider contentProvider,
                                       IClientWindowSizeProvider clientWindowSizeProvider)
            : base(nativeGraphicsManager, inventoryController, statusLabelSetter, itemStringService,
                   itemNameColorService, inventoryService, inventorySlotRepository, playerInfoProvider,
                   characterProvider, characterInventoryProvider, pubFileProvider, hudControlProvider,
                   activeDialogProvider, sfxPlayer, configProvider, clientWindowSizeProvider)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            // Remove the texture-based background
            BackgroundImage = null;
            DrawArea = new Rectangle(102, 330, PanelWidth, PanelHeight);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
            // Note: We keep the original buttons visible because hiding them breaks
            // MouseOver detection needed for drop/junk functionality.
            // Our styled buttons draw on top via the OnDrawControl override.
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw grid lines for slots (directly on panel background, no separate box)
            DrawInventoryGrid(pos);

            _spriteBatch.End();

            // Let the base class draw child controls (items, buttons)
            base.OnDrawControl(gameTime);

            // Now draw our styled buttons ON TOP of the texture buttons
            _spriteBatch.Begin();

            // Draw button area on the right (covers the original buttons completely)
            var buttonAreaRect = new Rectangle((int)pos.X + 448, (int)pos.Y + 2, 30, PanelHeight - 4);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, buttonAreaRect, new Color(40, 35, 30, 255));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, buttonAreaRect, _styleProvider.PanelBorder, 1);

            // Draw styled side buttons
            DrawSideButtons(pos);

            _spriteBatch.End();
        }

        private void DrawInventoryGrid(Vector2 pos)
        {
            var gridColor = new Color(80, 70, 60, 150);

            // Grid lines extend from near the panel edge to align with bottom/right
            var gridStartX = 4; // Close to panel border
            var gridStartY = 4;
            var gridEndX = SlotsStartX + InventoryRowSlots * SlotWidth;
            var gridEndY = SlotsStartY + InventoryRows * SlotHeight;

            // Draw vertical grid lines
            for (int col = 0; col <= InventoryRowSlots; col++)
            {
                var x = (int)pos.X + SlotsStartX + col * SlotWidth;
                DrawingPrimitives.DrawFilledRect(_spriteBatch,
                    new Rectangle(x, (int)pos.Y + gridStartY, 1, gridEndY - gridStartY),
                    gridColor);
            }

            // Draw horizontal grid lines
            for (int row = 0; row <= InventoryRows; row++)
            {
                var y = (int)pos.Y + SlotsStartY + row * SlotHeight;
                DrawingPrimitives.DrawFilledRect(_spriteBatch,
                    new Rectangle((int)pos.X + gridStartX, y, gridEndX - gridStartX, 1),
                    gridColor);
            }

            // Draw left edge line and top edge line to close the gap
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle((int)pos.X + gridStartX, (int)pos.Y + gridStartY, 1, gridEndY - gridStartY),
                gridColor);
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle((int)pos.X + gridStartX, (int)pos.Y + gridStartY, gridEndX - gridStartX, 1),
                gridColor);
        }

        private void DrawSideButtons(Vector2 pos)
        {
            // Original button positions within panel:
            // _page1: (453, 7), size 23x26
            // _page2: (453, 34), size 23x27
            // _drop: (453, 60), size 23x26
            // _junk: (453, 86), size 23x27
            var buttonX = (int)pos.X + 451;
            var buttonWidth = 25;
            var buttonHeight = 26;

            // Button 1: Page 1
            var btn1Rect = new Rectangle(buttonX, (int)pos.Y + 5, buttonWidth, buttonHeight);
            DrawStyledButton(btn1Rect, "1", _styleProvider.ButtonNormal);

            // Button 2: Page 2
            var btn2Rect = new Rectangle(buttonX, (int)pos.Y + 32, buttonWidth, 27);
            DrawStyledButton(btn2Rect, "2", _styleProvider.ButtonNormal);

            // Button 3: Drop (down arrow) - use "v" since arrow might not render
            var btn3Rect = new Rectangle(buttonX, (int)pos.Y + 58, buttonWidth, buttonHeight);
            DrawStyledButton(btn3Rect, "v", new Color(80, 140, 80));

            // Button 4: Junk (X)
            var btn4Rect = new Rectangle(buttonX, (int)pos.Y + 84, buttonWidth, 28);
            DrawStyledButton(btn4Rect, "X", new Color(140, 60, 60));
        }

        private void DrawStyledButton(Rectangle rect, string label, Color bgColor)
        {
            // Button background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rect, bgColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rect, _styleProvider.PanelBorder, 1);

            // Button label centered
            var textSize = _labelFont.MeasureString(label);
            var textPos = new Vector2(
                rect.X + (rect.Width - textSize.Width) / 2,
                rect.Y + (rect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(_labelFont, label, textPos, Color.White);
        }
    }
}
