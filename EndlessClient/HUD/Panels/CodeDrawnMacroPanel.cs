using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
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
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Code-drawn Macro/Hotkey panel that extends the base MacroPanel.
    /// Shows 16 slots for F1-F8 and Shift+F1-F8 hotkeys.
    /// </summary>
    public class CodeDrawnMacroPanel : MacroPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;
        private readonly BitmapFont _scaledFont;
        private readonly BitmapFont _scaledHeaderFont;

        private const int PanelWidth = 440;
        private const int PanelHeight = 145; // Extended to include button area
        private const int LeftPanelStartX = 16;
        private const int RightPanelStartX = 228;
        private const int GridStartY = 26;
        private const int SlotWidth = 52;
        private const int SlotHeight = 45;
        private const int SlotsPerRow = 4;

        private Rectangle _okButtonRect;
        private bool _okButtonHovered;
        private bool _wasMouseDown;

        public CodeDrawnMacroPanel(INativeGraphicsManager nativeGraphicsManager,
                                   IStatusLabelSetter statusLabelSetter,
                                   IPlayerInfoProvider playerInfoProvider,
                                   ICharacterProvider characterProvider,
                                   IEIFFileProvider eifFileProvider,
                                   IESFFileProvider esfFileProvider,
                                   HUD.Macros.IMacroSlotDataRepository macroSlotDataRepository,
                                   ISfxPlayer sfxPlayer,
                                   IConfigurationProvider configProvider,
                                   IClientWindowSizeProvider clientWindowSizeProvider,
                                   IUserInputProvider userInputProvider,
                                   IEODialogButtonService dialogButtonService,
                                   IUIStyleProvider styleProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider,
                                   IContentProvider contentProvider)
            : base(nativeGraphicsManager, statusLabelSetter, playerInfoProvider, characterProvider,
                   eifFileProvider, esfFileProvider, macroSlotDataRepository, sfxPlayer, configProvider,
                   clientWindowSizeProvider, userInputProvider, dialogButtonService)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _sfxPlayer = sfxPlayer;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _headerFont = contentProvider.Fonts[Constants.FontSize09];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];
            _scaledHeaderFont = contentProvider.Fonts[Constants.FontSize10];

            // Clear the GFX background image - we'll draw our own
            BackgroundImage = null;

            // Update draw area to include button
            DrawArea = new Rectangle(102, 330, PanelWidth, PanelHeight);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();

            // Hide the base class GFX button - we use our own code-drawn button
            if (_closeButton is XNAControls.XNAControl btn)
                btn.Visible = false;
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);
            var transformedPos = TransformMousePosition(mousePos);
            var isMouseDown = mouseState.LeftButton == ButtonState.Pressed;

            // Recalculate button rect (to ensure it matches current position)
            var pos = DrawPositionWithParentOffset;
            var buttonWidth = 80;
            var buttonHeight = 24;
            _okButtonRect = new Rectangle(
                (int)pos.X + (PanelWidth - buttonWidth) / 2,
                (int)pos.Y + PanelHeight - buttonHeight - 8,
                buttonWidth, buttonHeight);

            // Update button hover state
            _okButtonHovered = _okButtonRect.Contains(transformedPos);

            // Fire Activated when mouse is pressed inside panel to trigger z-order update
            var panelRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            if (isMouseDown && !_wasMouseDown && panelRect.Contains(transformedPos))
            {
                OnActivated();
            }

            // Handle button click
            if (_wasMouseDown && !isMouseDown && _okButtonHovered)
            {
                _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
                Visible = false;
            }

            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
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
                // This ensures proper z-ordering between overlapping panels
                base.OnDrawControl(gameTime);
                return;
            }

            DrawPanelComplete(DrawPositionWithParentOffset, _font, _headerFont);
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawPositionWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            // Draw fills first
            DrawPanelFillsScaled(scaledPos, scaleFactor);

            // Draw child icons (between fills and borders for correct layering)
            foreach (var child in ChildControls)
            {
                if (child is HUD.Macros.MacroPanelItem item && item.Visible)
                {
                    item.DrawIconPostScale(_spriteBatch, scaleFactor, renderOffset);
                }
            }

            // Draw borders and text last
            DrawPanelBordersAndText(scaledPos, scaleFactor);
        }

        private void DrawPanelFillsScaled(Vector2 pos, float scale)
        {
            _spriteBatch.Begin();

            var scaledWidth = (int)(PanelWidth * scale);
            var scaledHeight = (int)(PanelHeight * scale);

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Left panel area fill
            var leftX = (int)(pos.X + (LeftPanelStartX - 4) * scale);
            var leftY = (int)(pos.Y + 4 * scale);
            var leftRect = new Rectangle(leftX, leftY, (int)((SlotsPerRow * SlotWidth + 8) * scale), (int)(96 * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftRect, new Color(40, 35, 30, 180));

            // Right panel area fill
            var rightX = (int)(pos.X + (RightPanelStartX - 4) * scale);
            var rightRect = new Rectangle(rightX, leftY, (int)((SlotsPerRow * SlotWidth + 8) * scale), (int)(96 * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rightRect, new Color(40, 35, 30, 180));

            // NOTE: Slot grid fills intentionally omitted - icons are drawn to render target
            // and would be covered by fills drawn in post-scale phase

            // OK button fill
            var buttonWidth = (int)(80 * scale);
            var buttonHeight = (int)(24 * scale);
            var buttonRect = new Rectangle(
                (int)pos.X + (scaledWidth - buttonWidth) / 2,
                (int)pos.Y + scaledHeight - buttonHeight - (int)(8 * scale),
                buttonWidth, buttonHeight);
            var buttonColor = _okButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, buttonRect, buttonColor);

            _spriteBatch.End();
        }

        private void DrawSlotGridFillsScaled(Vector2 pos, int startX, float scale)
        {
            var scaledSlotWidth = (int)(SlotWidth * scale);
            var scaledSlotHeight = (int)(SlotHeight * scale);
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SlotsPerRow; col++)
                {
                    var x = (int)(pos.X + startX * scale) + col * scaledSlotWidth;
                    var y = (int)(pos.Y + GridStartY * scale) + row * scaledSlotHeight;
                    var slotRect = new Rectangle(x, y, scaledSlotWidth - (int)(2 * scale), scaledSlotHeight - (int)(2 * scale));
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, slotRect, new Color(30, 25, 20, 200));
                }
            }
        }

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Left panel area fill
            var leftPanelRect = new Rectangle((int)pos.X + LeftPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(40, 35, 30, 180));

            // Right panel area fill
            var rightPanelRect = new Rectangle((int)pos.X + RightPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rightPanelRect, new Color(40, 35, 30, 180));

            // Slot grid fills
            DrawSlotGridFills(pos, LeftPanelStartX);
            DrawSlotGridFills(pos, RightPanelStartX);

            // OK button fill
            var buttonWidth = 80;
            var buttonHeight = 24;
            _okButtonRect = new Rectangle(
                (int)pos.X + (PanelWidth - buttonWidth) / 2,
                (int)pos.Y + PanelHeight - buttonHeight - 8,
                buttonWidth, buttonHeight);
            var buttonColor = _okButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _okButtonRect, buttonColor);

            _spriteBatch.End();
        }

        private void DrawPanelBordersAndText(Vector2 scaledPos, float scale)
        {
            _spriteBatch.Begin();

            // Select font based on scale
            BitmapFont font, headerFont;
            if (scale >= 1.75f) { font = _scaledFont; headerFont = _scaledHeaderFont; }
            else if (scale >= 1.25f) { font = _headerFont; headerFont = _headerFont; }
            else { font = _font; headerFont = _headerFont; }

            var panelWidth = (int)(PanelWidth * scale);
            var panelHeight = (int)(PanelHeight * scale);

            // Panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, System.Math.Max(1, (int)(2 * scale)));

            // Left/Right panel borders
            var leftPanelRect = new Rectangle(
                (int)(scaledPos.X + (LeftPanelStartX - 4) * scale),
                (int)(scaledPos.Y + 4 * scale),
                (int)((SlotsPerRow * SlotWidth + 8) * scale),
                (int)(96 * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(70, 60, 50), 1);

            var rightPanelRect = new Rectangle(
                (int)(scaledPos.X + (RightPanelStartX - 4) * scale),
                (int)(scaledPos.Y + 4 * scale),
                (int)((SlotsPerRow * SlotWidth + 8) * scale),
                (int)(96 * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rightPanelRect, new Color(70, 60, 50), 1);

            // Panel labels
            _spriteBatch.DrawString(headerFont, "F1-F8", new Vector2(scaledPos.X + (LeftPanelStartX + 70) * scale, scaledPos.Y + 6 * scale), Color.White);
            _spriteBatch.DrawString(headerFont, "^F1-^F8", new Vector2(scaledPos.X + (RightPanelStartX + 60) * scale, scaledPos.Y + 6 * scale), Color.White);

            // Slot grid borders and labels
            DrawSlotGridBordersAndText(scaledPos, scale, LeftPanelStartX, new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" }, font);
            DrawSlotGridBordersAndText(scaledPos, scale, RightPanelStartX, new[] { "^F1", "^F2", "^F3", "^F4", "^F5", "^F6", "^F7", "^F8" }, font);

            // OK button border and text
            var buttonWidth = (int)(80 * scale);
            var buttonHeight = (int)(24 * scale);
            var buttonRect = new Rectangle(
                (int)(scaledPos.X + (PanelWidth - 80) / 2 * scale),
                (int)(scaledPos.Y + (PanelHeight - 24 - 8) * scale),
                buttonWidth, buttonHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, buttonRect, Color.Black, 1);
            var buttonText = "OK";
            var textSize = headerFont.MeasureString(buttonText);
            var textPos = new Vector2(
                buttonRect.X + (buttonRect.Width - textSize.Width) / 2,
                buttonRect.Y + (buttonRect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(headerFont, buttonText, textPos, Color.White);

            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos, BitmapFont font, BitmapFont headerFont)
        {
            _spriteBatch.Begin();

            // Draw main panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw left/right panel areas
            var leftPanelRect = new Rectangle((int)pos.X + LeftPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(40, 35, 30, 180));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(70, 60, 50), 1);

            var rightPanelRect = new Rectangle((int)pos.X + RightPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rightPanelRect, new Color(40, 35, 30, 180));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rightPanelRect, new Color(70, 60, 50), 1);

            // Draw panel labels
            _spriteBatch.DrawString(headerFont, "F1-F8", new Vector2(pos.X + LeftPanelStartX + 70, pos.Y + 6), Color.White);
            _spriteBatch.DrawString(headerFont, "^F1-^F8", new Vector2(pos.X + RightPanelStartX + 60, pos.Y + 6), Color.White);

            // Draw slot grids
            DrawSlotGrid(pos, LeftPanelStartX, new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" });
            DrawSlotGrid(pos, RightPanelStartX, new[] { "^F1", "^F2", "^F3", "^F4", "^F5", "^F6", "^F7", "^F8" });

            // Draw OK button
            var buttonWidth = 80;
            var buttonHeight = 24;
            _okButtonRect = new Rectangle(
                (int)pos.X + (PanelWidth - buttonWidth) / 2,
                (int)pos.Y + PanelHeight - buttonHeight - 8,
                buttonWidth, buttonHeight);

            var buttonColor = _okButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _okButtonRect, buttonColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _okButtonRect, Color.Black, 1);

            var buttonText = "OK";
            var textSize = headerFont.MeasureString(buttonText);
            var textPos = new Vector2(
                _okButtonRect.X + (_okButtonRect.Width - textSize.Width) / 2,
                _okButtonRect.Y + (_okButtonRect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(headerFont, buttonText, textPos, Color.White);

            _spriteBatch.End();
        }

        private void DrawSlotGridFills(Vector2 panelPos, int startX)
        {
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SlotsPerRow; col++)
                {
                    var slotX = (int)panelPos.X + startX + (col * SlotWidth);
                    var slotY = (int)panelPos.Y + GridStartY + (row * SlotHeight);
                    var slotRect = new Rectangle(slotX, slotY, SlotWidth - 4, SlotHeight - 4);
                    var slotColor = (row + col) % 2 == 0 ? new Color(55, 50, 45, 180) : new Color(50, 45, 40, 180);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, slotRect, slotColor);
                }
            }
        }

        private void DrawSlotGridBordersAndText(Vector2 panelPos, float scale, int startX, string[] labels, BitmapFont font)
        {
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SlotsPerRow; col++)
                {
                    var slotX = (int)(panelPos.X + (startX + col * SlotWidth) * scale);
                    var slotY = (int)(panelPos.Y + (GridStartY + row * SlotHeight) * scale);
                    var slotWidth = (int)((SlotWidth - 4) * scale);
                    var slotHeight = (int)((SlotHeight - 4) * scale);
                    var slotRect = new Rectangle(slotX, slotY, slotWidth, slotHeight);
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, slotRect, new Color(70, 60, 50), 1);

                    var labelIndex = row * SlotsPerRow + col;
                    if (labelIndex < labels.Length)
                    {
                        _spriteBatch.DrawString(font, labels[labelIndex],
                            new Vector2(slotX + 2 * scale, slotY + (SlotHeight - 16) * scale), new Color(150, 140, 130));
                    }
                }
            }
        }


        private void DrawSlotGrid(Vector2 panelPos, int startX, string[] labels)
        {
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SlotsPerRow; col++)
                {
                    var slotX = (int)panelPos.X + startX + (col * SlotWidth);
                    var slotY = (int)panelPos.Y + GridStartY + (row * SlotHeight);
                    var slotRect = new Rectangle(slotX, slotY, SlotWidth - 4, SlotHeight - 4);

                    // Draw slot background
                    var slotColor = (row + col) % 2 == 0 ? new Color(55, 50, 45, 180) : new Color(50, 45, 40, 180);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, slotRect, slotColor);
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, slotRect, new Color(70, 60, 50), 1);

                    // Draw function key label in corner
                    var labelIndex = row * SlotsPerRow + col;
                    if (labelIndex < labels.Length)
                    {
                        _spriteBatch.DrawString(_font, labels[labelIndex],
                            new Vector2(slotX + 2, slotY + SlotHeight - 16), new Color(150, 140, 130));
                    }
                }
            }
        }
    }
}

