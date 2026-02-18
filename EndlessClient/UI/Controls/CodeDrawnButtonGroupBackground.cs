using System;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// Draws a semi-transparent rounded-rect background behind a group of HUD icon buttons.
    /// Renders entirely post-scale so it appears above windows/panels.
    /// </summary>
    public class CodeDrawnButtonGroupBackground : DrawableGameComponent, IPostScaleDrawable
    {
        public enum Side { Left, Right }

        private const int ICON_SIZE = 32;
        private const int ICON_GAP = 2;
        private const int PADDING = 4;
        private const int LEFT_BUTTON_COUNT = 5;
        private const int RIGHT_BUTTON_COUNT = 10;
        private const int ROWS_PER_COLUMN = 5;

        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeRepository _clientWindowSizeRepository;
        private readonly Side _side;

        private Rectangle _bounds;

        public CodeDrawnButtonGroupBackground(
            IUIStyleProvider styleProvider,
            IClientWindowSizeRepository clientWindowSizeRepository,
            Side side)
            : base(null)
        {
            _styleProvider = styleProvider;
            _clientWindowSizeRepository = clientWindowSizeRepository;
            _side = side;

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) => RecalculateBounds();

            RecalculateBounds();
        }

        private void RecalculateBounds()
        {
            if (_side == Side.Left)
            {
                // Matches HudControlsFactory.GetLeftStackPosition:
                // X = 0, Y = vertically centered stack of 5 buttons
                var totalHeight = LEFT_BUTTON_COUNT * ICON_SIZE + (LEFT_BUTTON_COUNT - 1) * ICON_GAP;
                var yStart = (_clientWindowSizeRepository.Height - totalHeight) / 2;

                _bounds = new Rectangle(
                    -PADDING,
                    yStart - PADDING,
                    ICON_SIZE + PADDING * 2,
                    totalHeight + PADDING * 2);
            }
            else
            {
                // Matches HudControlsFactory.GetRightStackPosition:
                // 2 columns, 5 rows. col 0 xPos = Width - 2*(ICON_SIZE+ICON_GAP), col 1 xPos = Width - 1*(ICON_SIZE+ICON_GAP)
                var totalHeight = ROWS_PER_COLUMN * ICON_SIZE + (ROWS_PER_COLUMN - 1) * ICON_GAP;
                var yStart = (_clientWindowSizeRepository.Height - totalHeight) / 2;
                var stride = ICON_SIZE + ICON_GAP;
                var xStart = _clientWindowSizeRepository.Width - 2 * stride; // left edge of col 0
                var totalWidth = 2 * ICON_SIZE + ICON_GAP; // two buttons with gap between them

                _bounds = new Rectangle(
                    xStart - PADDING,
                    yStart - PADDING,
                    totalWidth + PADDING * 2,
                    totalHeight + PADDING * 2);
            }
        }

        // IPostScaleDrawable — render above windows (max 90), below dialogs (100)
        public int PostScaleDrawOrder => 95;
        public bool SkipRenderTargetDraw => true;

        public override void Draw(GameTime gameTime)
        {
            // No game-resolution drawing — everything is post-scale
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var scaledBounds = new Rectangle(
                (int)(_bounds.X * scaleFactor) + renderOffset.X,
                (int)(_bounds.Y * scaleFactor) + renderOffset.Y,
                (int)(_bounds.Width * scaleFactor),
                (int)(_bounds.Height * scaleFactor));

            var cornerRadius = Math.Max(1, (int)(_styleProvider.CornerRadius * scaleFactor));
            var borderThickness = Math.Max(1, (int)(_styleProvider.BorderThickness * scaleFactor));

            // Use PanelBackground with extra transparency for subtlety
            var bgColor = _styleProvider.PanelBackground;
            var subtleBg = new Color(bgColor.R, bgColor.G, bgColor.B, (int)(bgColor.A * 0.7f));

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawingPrimitives.DrawRoundedRect(spriteBatch, scaledBounds, subtleBg, cornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(spriteBatch, scaledBounds, new Color(_styleProvider.PanelBorder, 0.4f), cornerRadius, borderThickness);
            spriteBatch.End();
        }
    }
}
