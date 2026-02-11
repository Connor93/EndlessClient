using System;
using System.Linq;
using EndlessClient.Rendering.Map;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.Rendering.MapEntityRenderers
{
    public class MapItemLayerRenderer : BaseMapEntityRenderer
    {
        private const int IndicatorWidth = 40;
        private const int IndicatorHeight = 20;
        private const double PulsePeriodMs = 1500.0;

        private readonly ICurrentMapStateProvider _currentMapStateProvider;
        private readonly IMapItemGraphicProvider _mapItemGraphicProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        private Texture2D _whitePixel;

        public override MapRenderLayer RenderLayer => MapRenderLayer.Item;

        protected override int RenderDistance => 16;

        public MapItemLayerRenderer(ICharacterProvider characterProvider,
                                    IGridDrawCoordinateCalculator gridDrawCoordinateCalculator,
                                    IClientWindowSizeProvider clientWindowSizeProvider,
                                    ICurrentMapStateProvider currentMapStateProvider,
                                    IMapItemGraphicProvider mapItemGraphicProvider,
                                    IGraphicsDeviceProvider graphicsDeviceProvider)
            : base(characterProvider, gridDrawCoordinateCalculator, clientWindowSizeProvider)
        {
            _currentMapStateProvider = currentMapStateProvider;
            _mapItemGraphicProvider = mapItemGraphicProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        protected override bool ElementExistsAt(int row, int col)
        {
            return _currentMapStateProvider.MapItems.ContainsKey(new MapCoordinate(col, row));
        }

        public override void RenderElementAt(SpriteBatch spriteBatch, int row, int col, int alpha, Vector2 additionalOffset = default)
        {
            EnsureWhitePixel();

            var items = _currentMapStateProvider.MapItems[new MapCoordinate(col, row)];

            foreach (var item in items.OrderBy(item => item.UniqueID))
            {
                var itemPos = GetDrawCoordinatesFromGridUnits(col, row);
                var itemTexture = _mapItemGraphicProvider.GetItemGraphic(item.ItemID, item.Amount);

                // Draw indicator diamond beneath the item
                DrawIndicator(spriteBatch, itemPos + additionalOffset, alpha);

                spriteBatch.Draw(itemTexture,
                                 new Vector2(itemPos.X - (int)Math.Round(itemTexture.Width / 2.0),
                                             itemPos.Y - (int)Math.Round(itemTexture.Height / 2.0)) + additionalOffset,
                                 Color.FromNonPremultiplied(255, 255, 255, alpha));
            }
        }

        private void DrawIndicator(SpriteBatch spriteBatch, Vector2 center, int alpha)
        {
            if (_whitePixel == null)
                return;

            // Time-based pulse: oscillate alpha using wall-clock time (FPS-independent)
            var pulsePhase = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond % PulsePeriodMs) / PulsePeriodMs;
            var pulseAlpha = 0.55f + 0.45f * (float)Math.Sin(pulsePhase * Math.PI * 2);
            var alphaScale = (alpha / 255f) * pulseAlpha;

            // Pass 1: dark shadow outline (offset +1px down-right)
            DrawDiamondOutline(spriteBatch, center, 1, Color.Black * (alphaScale * 0.6f));

            // Pass 2: bright white outline
            DrawDiamondOutline(spriteBatch, center, 0, Color.White * alphaScale);

            // Fill
            DrawDiamondFill(spriteBatch, center, Color.White * (alphaScale * 0.12f));
        }

        private void DrawDiamondOutline(SpriteBatch spriteBatch, Vector2 center, int shadowOffset, Color color)
        {
            for (var y = 0; y < IndicatorHeight; y++)
            {
                int halfWidth;
                if (y < IndicatorHeight / 2)
                    halfWidth = (int)(y * ((float)IndicatorWidth / IndicatorHeight));
                else
                    halfWidth = (int)((IndicatorHeight - 1 - y) * ((float)IndicatorWidth / IndicatorHeight));

                var drawY = (int)center.Y - IndicatorHeight / 2 + y + shadowOffset;
                var xCenter = (int)center.X + shadowOffset;

                if (halfWidth <= 0)
                {
                    spriteBatch.Draw(_whitePixel,
                        new Rectangle(xCenter, drawY, 1, 1),
                        color);
                    continue;
                }

                // Left edge
                spriteBatch.Draw(_whitePixel,
                    new Rectangle(xCenter - halfWidth, drawY, 1, 1),
                    color);

                // Right edge
                spriteBatch.Draw(_whitePixel,
                    new Rectangle(xCenter + halfWidth, drawY, 1, 1),
                    color);
            }
        }

        private void DrawDiamondFill(SpriteBatch spriteBatch, Vector2 center, Color color)
        {
            for (var y = 0; y < IndicatorHeight; y++)
            {
                int halfWidth;
                if (y < IndicatorHeight / 2)
                    halfWidth = (int)(y * ((float)IndicatorWidth / IndicatorHeight));
                else
                    halfWidth = (int)((IndicatorHeight - 1 - y) * ((float)IndicatorWidth / IndicatorHeight));

                if (halfWidth <= 1)
                    continue;

                var drawY = (int)center.Y - IndicatorHeight / 2 + y;
                var xCenter = (int)center.X;

                spriteBatch.Draw(_whitePixel,
                    new Rectangle(xCenter - halfWidth + 1, drawY, halfWidth * 2 - 1, 1),
                    color);
            }
        }

        private void EnsureWhitePixel()
        {
            if (_whitePixel != null)
                return;

            _whitePixel = new Texture2D(_graphicsDeviceProvider.GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
        }
    }
}
