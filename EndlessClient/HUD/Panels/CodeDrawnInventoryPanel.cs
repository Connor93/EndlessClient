using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs;
using EndlessClient.HUD.Inventory;
using EndlessClient.HUD.Windows;
using EndlessClient.Input;
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
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnInventoryPanel : InventoryPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _labelFont;

        private const int PanelWidth = 196;
        private const int PanelHeight = 296;
        private const int SlotWidth = 26;
        private const int SlotHeight = 26;
        // Items are positioned at (13 + 26*col, 28 + 26*row) per InventoryPanelItem.GetPosition
        private const int SlotsStartX = 13;
        private const int SlotsStartY = 28;

        // IZOrderedWindow implementation
        private int _zOrder;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => true;

        public void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
        }

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
                                       IClientWindowSizeProvider clientWindowSizeProvider,
                                       IUserInputProvider userInputProvider)
            : base(nativeGraphicsManager, inventoryController, statusLabelSetter, itemStringService,
                   itemNameColorService, inventoryService, inventorySlotRepository, playerInfoProvider,
                   characterProvider, characterInventoryProvider, pubFileProvider, hudControlProvider,
                   activeDialogProvider, sfxPlayer, configProvider, clientWindowSizeProvider, userInputProvider)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _contentProvider = contentProvider;
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            // Remove the texture-based background
            BackgroundImage = null;
            DrawArea = new Rectangle(102, 212, PanelWidth, PanelHeight);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        private bool _sortButtonWasPressed;

        protected override void OnUnconditionalUpdateControl(GameTime gameTime)
        {
            base.OnUnconditionalUpdateControl(gameTime);

            var mouseState = MonoGame.Extended.Input.MouseExtended.GetState();
            var transformedPos = TransformMousePosition(mouseState.Position);
            var mousePos = new Point((int)transformedPos.X, (int)transformedPos.Y);

            // Check if the mouse is over the sort button area
            var pos = DrawPositionWithParentOffset;
            var sortRect = new Rectangle((int)pos.X + 4, (int)pos.Y + 4, PanelWidth - 8, 20);

            if (sortRect.Contains(mousePos) && mouseState.IsButtonDown(MonoGame.Extended.Input.MouseButton.Left))
            {
                _sortButtonWasPressed = true;
            }
            else if (_sortButtonWasPressed && mouseState.IsButtonUp(MonoGame.Extended.Input.MouseButton.Left))
            {
                _sortButtonWasPressed = false;
                if (sortRect.Contains(mousePos))
                {
                    SortInventory();
                }
            }
            else if (!sortRect.Contains(mousePos))
            {
                _sortButtonWasPressed = false;
            }
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            // Always draw background in render-target phase because base class creates
            // InventoryPanelItem child controls that can only draw in render-target.
            // DrawPostScale cannot be used for the background as it would cover the children.
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw sort button at top of panel
            DrawSortButton(pos);

            // Draw grid lines for slots
            DrawInventoryGrid(pos);

            _spriteBatch.End();

            // Let the base class draw child controls (items)
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            // No-op: this panel has InventoryPanelItem child controls that draw in render-target,
            // so the background must also be drawn there (in OnDrawControl) to avoid covering them.
        }

        private void DrawSortButton(Vector2 pos)
        {
            var sortRect = new Rectangle((int)pos.X + 4, (int)pos.Y + 4, PanelWidth - 8, 20);
            DrawStyledButton(sortRect, "Sort", _styleProvider.ButtonNormal);
        }

        private void DrawInventoryGrid(Vector2 pos)
        {
            var gridColor = new Color(80, 70, 60, 150);

            var gridStartX = 4;
            var gridStartY = SlotsStartY;
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
        private InventoryItemContextMenu _activeContextMenu;

        protected override void HandleItemRightClick(object sender, EOLib.IO.Pub.EIFRecord itemData)
        {
            // Dismiss any existing context menu
            if (_activeContextMenu != null)
            {
                Game.Components.Remove(_activeContextMenu);
                _activeContextMenu.Dispose();
                _activeContextMenu = null;
            }

            var mousePos = _userInputProvider.CurrentMouseState.Position;

            var hasPortableLocker = _characterInventoryProvider.ItemInventory
                .Any(i => i.ItemID == InventoryConstants.PortableLockerItemID);
            var hasGlamorGem = _characterInventoryProvider.ItemInventory
                .Any(i => i.ItemID == InventoryConstants.GlamorGemItemID);

            var contextMenu = new InventoryItemContextMenu(
                _styleProvider, _graphicsDeviceProvider, _contentProvider,
                _userInputProvider, _sfxPlayer, itemData,
                new Vector2(mousePos.X, mousePos.Y),
                hasPortableLocker, hasGlamorGem);

            contextMenu.UseEquipClicked += UseOrEquipItem;
            contextMenu.DropClicked += DropItem;
            contextMenu.JunkClicked += JunkItem;
            contextMenu.StoreClicked += StoreItem;
            contextMenu.GlamorClicked += GlamorItem;

            contextMenu.Initialize();

            _activeContextMenu = contextMenu;
        }
    }
}
