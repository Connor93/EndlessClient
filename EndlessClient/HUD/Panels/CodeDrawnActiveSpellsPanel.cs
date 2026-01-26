using System;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Spells;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.HUD.Windows;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Login;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Code-drawn Active Spells panel that extends the base ActiveSpellsPanel.
    /// Overrides drawing to use styled visuals while preserving all functionality.
    /// </summary>
    public class CodeDrawnActiveSpellsPanel : ActiveSpellsPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;
        private readonly BitmapFont _scaledFont;
        private readonly BitmapFont _scaledHeaderFont;

        private const int PanelWidth = 484;
        private const int PanelHeight = 118;
        private const int LeftPanelWidth = 95;
        // Match SpellPanelItem.GetDisplayPosition: X=101+col*45, Y=9+row*52
        private const int GridStartX = 101;
        private const int GridStartY = 9;
        private const int SlotWidth = 42;  // ICON_AREA_WIDTH from SpellPanelItem
        private const int SlotHeight = 36; // ICON_AREA_HEIGHT from SpellPanelItem
        private const int SlotXSpacing = 45;  // xdelta in SpellPanelItem
        private const int SlotYSpacing = 52;  // ydelta in SpellPanelItem

        public CodeDrawnActiveSpellsPanel(INativeGraphicsManager nativeGraphicsManager,
                                          ITrainingController trainingController,
                                          IEOMessageBoxFactory messageBoxFactory,
                                          IStatusLabelSetter statusLabelSetter,
                                          IPlayerInfoProvider playerInfoProvider,
                                          ICharacterProvider characterProvider,
                                          ICharacterInventoryProvider characterInventoryProvider,
                                          IESFFileProvider esfFileProvider,
                                          ISpellSlotDataRepository spellSlotDataRepository,
                                          IHudControlProvider hudControlProvider,
                                          ISfxPlayer sfxPlayer,
                                          IConfigurationProvider configProvider,
                                          IClientWindowSizeProvider clientWindowSizeProvider,
                                          IUserInputProvider userInputProvider,
                                          IUIStyleProvider styleProvider,
                                          IGraphicsDeviceProvider graphicsDeviceProvider,
                                          IContentProvider contentProvider)
            : base(nativeGraphicsManager, trainingController, messageBoxFactory, statusLabelSetter,
                   playerInfoProvider, characterProvider, characterInventoryProvider, esfFileProvider,
                   spellSlotDataRepository, hudControlProvider, sfxPlayer, configProvider,
                   clientWindowSizeProvider, userInputProvider)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _headerFont = contentProvider.Fonts[Constants.FontSize09];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];
            _scaledHeaderFont = contentProvider.Fonts[Constants.FontSize10];

            // Clear the GFX background image - we'll draw our own
            BackgroundImage = null;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        // IZOrderedWindow implementation
        private int _zOrder = 0;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

        public void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                // In scaled mode: skip fills here - they will be drawn in DrawPostScale
                // so each panel draws fills + text together for correct z-ordering
            }
            else
            {
                // In normal mode: draw everything in one pass
                DrawPanelComplete();
            }

            // Let base class draw the spell icons, labels, and buttons
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible)
                return;

            var gamePos = DrawPositionWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            // Draw fills first, then text/borders - each panel complete before next
            DrawPanelFillsScaled(scaledPos, scaleFactor);
            DrawPanelBordersAndText(scaleFactor, renderOffset);
        }

        private void DrawPanelFillsScaled(Vector2 pos, float scale)
        {
            _spriteBatch.Begin();

            var scaledWidth = (int)(PanelWidth * scale);
            var scaledHeight = (int)(PanelHeight * scale);
            var scaledLeftWidth = (int)(LeftPanelWidth * scale);

            // Draw main panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Draw left info panel fill
            var leftPanelRect = new Rectangle((int)pos.X, (int)pos.Y, scaledLeftWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(50, 45, 40, 220));

            _spriteBatch.End();
        }

        private void DrawPanelFills()
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw main panel background fill only
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Draw left info panel fill
            var leftPanelRect = new Rectangle((int)pos.X, (int)pos.Y, LeftPanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(50, 45, 40, 220));

            // Draw grid area background fill
            var gridRect = new Rectangle((int)pos.X + GridStartX - 2, (int)pos.Y + 2, PanelWidth - GridStartX, PanelHeight - 4);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, gridRect, new Color(40, 35, 30, 180));

            // Draw slot fills (8 columns x 2 visible rows)
            // Match SpellPanelItem.GetDisplayPosition: X=101+col*45, Y=9+row*52
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SpellRowLength; col++)
                {
                    var slotX = (int)pos.X + GridStartX + (col * SlotXSpacing);
                    var slotY = (int)pos.Y + GridStartY + (row * SlotYSpacing);
                    var slotRect = new Rectangle(slotX, slotY, SlotWidth, SlotHeight);

                    var slotColor = (row + col) % 2 == 0 ? new Color(55, 50, 45, 180) : new Color(50, 45, 40, 180);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, slotRect, slotColor);
                }
            }

            _spriteBatch.End();
        }

        private void DrawPanelBordersAndText(float scale, Point renderOffset)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;
            var scaledPos = new Vector2(pos.X * scale + renderOffset.X, pos.Y * scale + renderOffset.Y);

            // Dynamic font selection based on scale
            BitmapFont font, headerFont;
            if (scale >= 1.75f)
            {
                font = _scaledFont;
                headerFont = _scaledHeaderFont;
            }
            else if (scale >= 1.25f)
            {
                font = _headerFont;
                headerFont = _headerFont;
            }
            else
            {
                font = _font;
                headerFont = _font;
            }

            // Draw main panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, (int)(PanelWidth * scale), (int)(PanelHeight * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw left info panel border
            var leftPanelRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, (int)(LeftPanelWidth * scale), (int)(PanelHeight * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(80, 70, 60), 1);

            // Draw slot borders (8 columns x 2 visible rows)
            // Match SpellPanelItem.GetDisplayPosition: X=101+col*45, Y=9+row*52
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SpellRowLength; col++)
                {
                    var slotX = (int)(scaledPos.X + (GridStartX + col * SlotXSpacing) * scale);
                    var slotY = (int)(scaledPos.Y + (GridStartY + row * SlotYSpacing) * scale);
                    var slotRect = new Rectangle(slotX, slotY, (int)(SlotWidth * scale), (int)(SlotHeight * scale));

                    DrawingPrimitives.DrawRectBorder(_spriteBatch, slotRect, new Color(70, 60, 50), 1);
                }
            }

            // Draw "LVL" and "Pts" labels in left panel
            var lvlPos = new Vector2(scaledPos.X + 8 * scale, scaledPos.Y + 78 * scale);
            var ptsPos = new Vector2(scaledPos.X + 8 * scale, scaledPos.Y + 96 * scale);
            _spriteBatch.DrawString(font, "LVL", lvlPos, _styleProvider.TextSecondary);
            _spriteBatch.DrawString(font, "Pts", ptsPos, _styleProvider.TextSecondary);

            _spriteBatch.End();
        }

        private void DrawPanelComplete()
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw main panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw left info panel (selected spell)
            var leftPanelRect = new Rectangle((int)pos.X, (int)pos.Y, LeftPanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(50, 45, 40, 220));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(80, 70, 60), 1);

            // Draw grid area background
            var gridRect = new Rectangle((int)pos.X + GridStartX - 2, (int)pos.Y + 2, PanelWidth - GridStartX, PanelHeight - 4);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, gridRect, new Color(40, 35, 30, 180));

            // Draw slot grid lines (8 columns x 2 visible rows)
            // Match SpellPanelItem.GetDisplayPosition: X=101+col*45, Y=9+row*52
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SpellRowLength; col++)
                {
                    var slotX = (int)pos.X + GridStartX + (col * SlotXSpacing);
                    var slotY = (int)pos.Y + GridStartY + (row * SlotYSpacing);
                    var slotRect = new Rectangle(slotX, slotY, SlotWidth, SlotHeight);

                    // Draw slot background
                    var slotColor = (row + col) % 2 == 0 ? new Color(55, 50, 45, 180) : new Color(50, 45, 40, 180);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, slotRect, slotColor);
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, slotRect, new Color(70, 60, 50), 1);
                }
            }

            // Draw "LVL" and "Pts" labels in left panel
            _spriteBatch.DrawString(_font, "LVL", new Vector2(pos.X + 8, pos.Y + 78), _styleProvider.TextSecondary);
            _spriteBatch.DrawString(_font, "Pts", new Vector2(pos.X + 8, pos.Y + 96), _styleProvider.TextSecondary);

            _spriteBatch.End();
        }
    }
}
