using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Login;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Code-drawn Macro/Hotkey panel that extends the base MacroPanel.
    /// Shows 16 slots for F1-F8 and Shift+F1-F8 hotkeys.
    /// </summary>
    public class CodeDrawnMacroPanel : MacroPanel
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;

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
            _sfxPlayer = sfxPlayer;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _headerFont = contentProvider.Fonts[Constants.FontSize09];

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

            // Handle button click
            if (_wasMouseDown && !isMouseDown && _okButtonHovered)
            {
                _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
                Visible = false;
            }

            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw main panel background (extended height)
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw left panel area (F1-F8)
            var leftPanelRect = new Rectangle((int)pos.X + LeftPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(40, 35, 30, 180));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(70, 60, 50), 1);

            // Draw right panel area (Shift+F1-F8)
            var rightPanelRect = new Rectangle((int)pos.X + RightPanelStartX - 4, (int)pos.Y + 4, SlotsPerRow * SlotWidth + 8, 96);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rightPanelRect, new Color(40, 35, 30, 180));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rightPanelRect, new Color(70, 60, 50), 1);

            // Draw panel labels
            _spriteBatch.DrawString(_headerFont, "F1-F8", new Vector2(pos.X + LeftPanelStartX + 70, pos.Y + 6), Color.White);
            _spriteBatch.DrawString(_headerFont, "^F1-^F8", new Vector2(pos.X + RightPanelStartX + 60, pos.Y + 6), Color.White);

            // Draw slot grid for left panel (F1-F8)
            DrawSlotGrid(pos, LeftPanelStartX, new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" });

            // Draw slot grid for right panel (Shift+F1-F8)
            DrawSlotGrid(pos, RightPanelStartX, new[] { "^F1", "^F2", "^F3", "^F4", "^F5", "^F6", "^F7", "^F8" });

            // Draw code-drawn OK button at bottom center
            var buttonWidth = 80;
            var buttonHeight = 24;
            _okButtonRect = new Rectangle(
                (int)pos.X + (PanelWidth - buttonWidth) / 2,
                (int)pos.Y + PanelHeight - buttonHeight - 8,
                buttonWidth, buttonHeight);

            var buttonColor = _okButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _okButtonRect, buttonColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _okButtonRect, Color.Black, 1);

            // Draw button text centered
            var buttonText = "OK";
            var textSize = _headerFont.MeasureString(buttonText);
            var textPos = new Vector2(
                _okButtonRect.X + (_okButtonRect.Width - textSize.Width) / 2,
                _okButtonRect.Y + (_okButtonRect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(_headerFont, buttonText, textPos, Color.White);

            _spriteBatch.End();

            // Let base class draw the macro items (but not the old button since we cleared it)
            base.OnDrawControl(gameTime);
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

