using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XNAControls;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// A procedurally-drawn panel with rounded corners and borders.
    /// This replaces texture-based panel backgrounds for code-drawn UI mode.
    /// </summary>
    public class CodeDrawnPanel : XNAControl
    {
        private readonly IUIStyleProvider _styleProvider;
        private bool _drawBorder = true;
        private bool _useRoundedCorners = true;
        private Color? _backgroundColorOverride;
        private Color? _borderColorOverride;

        public bool DrawBorder
        {
            get => _drawBorder;
            set => _drawBorder = value;
        }

        public bool UseRoundedCorners
        {
            get => _useRoundedCorners;
            set => _useRoundedCorners = value;
        }

        public Color? BackgroundColorOverride
        {
            get => _backgroundColorOverride;
            set => _backgroundColorOverride = value;
        }

        public Color? BorderColorOverride
        {
            get => _borderColorOverride;
            set => _borderColorOverride = value;
        }

        public CodeDrawnPanel(IUIStyleProvider styleProvider)
        {
            _styleProvider = styleProvider;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);
            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var backgroundColor = _backgroundColorOverride ?? _styleProvider.PanelBackground;
            var borderColor = _borderColorOverride ?? _styleProvider.PanelBorder;
            var cornerRadius = _useRoundedCorners ? _styleProvider.CornerRadius : 0;
            var borderThickness = _styleProvider.BorderThickness;

            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin();

            // Draw background
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, backgroundColor, cornerRadius);
            else
                DrawingPrimitives.DrawFilledRect(_spriteBatch, bounds, backgroundColor);

            // Draw border
            if (_drawBorder)
            {
                if (cornerRadius > 0)
                    DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, borderColor, cornerRadius, borderThickness);
                else
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, bounds, borderColor, borderThickness);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }
    }
}
