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
    public class CodeDrawnActiveSpellsPanel : ActiveSpellsPanel
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;

        private const int PanelWidth = 484;
        private const int PanelHeight = 118;
        private const int LeftPanelWidth = 95;
        private const int GridStartX = 100;
        private const int SlotSize = 45;
        private const int SlotPadding = 0;

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
            _font = contentProvider.Fonts[Constants.FontSize08];
            _headerFont = contentProvider.Fonts[Constants.FontSize09];

            // Clear the GFX background image - we'll draw our own
            BackgroundImage = null;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
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
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < SpellRowLength; col++)
                {
                    var slotX = (int)pos.X + GridStartX + (col * SlotSize);
                    var slotY = (int)pos.Y + 20 + (row * (SlotSize + 6));
                    var slotRect = new Rectangle(slotX, slotY, SlotSize - 2, SlotSize - 2);

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

            // Let base class draw the spell icons, labels, and buttons
            base.OnDrawControl(gameTime);
        }
    }
}
