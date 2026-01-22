using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Code-drawn Passive Spells panel with same visual style as Active Spells.
    /// Currently just displays styled background - passive skills not yet implemented.
    /// </summary>
    public class CodeDrawnPassiveSpellsPanel : PassiveSpellsPanel
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

        public CodeDrawnPassiveSpellsPanel(INativeGraphicsManager nativeGraphicsManager,
                                           IUIStyleProvider styleProvider,
                                           IGraphicsDeviceProvider graphicsDeviceProvider,
                                           IContentProvider contentProvider,
                                           IClientWindowSizeProvider clientWindowSizeProvider)
            : base(nativeGraphicsManager, clientWindowSizeProvider)
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

            // Draw left info panel
            var leftPanelRect = new Rectangle((int)pos.X, (int)pos.Y, LeftPanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leftPanelRect, new Color(50, 45, 40, 220));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leftPanelRect, new Color(80, 70, 60), 1);

            // Draw "Passive Skills" header
            _spriteBatch.DrawString(_headerFont, "Passive", new Vector2(pos.X + 20, pos.Y + 10), Color.White);
            _spriteBatch.DrawString(_headerFont, "Skills", new Vector2(pos.X + 25, pos.Y + 26), Color.White);

            // Draw placeholder text
            _spriteBatch.DrawString(_font, "(Not yet", new Vector2(pos.X + 18, pos.Y + 55), _styleProvider.TextSecondary);
            _spriteBatch.DrawString(_font, "implemented)", new Vector2(pos.X + 8, pos.Y + 70), _styleProvider.TextSecondary);

            // Draw grid area background
            var gridRect = new Rectangle((int)pos.X + GridStartX - 2, (int)pos.Y + 2, PanelWidth - GridStartX, PanelHeight - 4);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, gridRect, new Color(40, 35, 30, 180));

            // Draw empty slot grid (8 columns x 2 visible rows)
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 8; col++)
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

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }
    }
}
