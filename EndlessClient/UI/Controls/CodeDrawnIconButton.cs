using System;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// A 32×32 icon-based HUD button showing a pixel art texture.
    /// Supports hover/press visual states, tooltip text, and IPostScaleDrawable for crisp rendering.
    /// </summary>
    public class CodeDrawnIconButton : XNAControl, IPostScaleDrawable
    {
        private enum ButtonState { Normal, Hover, Pressed }

        private const int ICON_SIZE = 32;
        private const int TOOLTIP_PAD_H = 6;
        private const int TOOLTIP_PAD_V = 3;

        private readonly IUIStyleProvider _styleProvider;
        private readonly Texture2D _iconTexture;
        private readonly BitmapFont _tooltipFont;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private ButtonState _state = ButtonState.Normal;
        private string _tooltipText = string.Empty;

        /// <summary>Tooltip text shown on hover.</summary>
        public string TooltipText
        {
            get => _tooltipText;
            set => _tooltipText = value ?? string.Empty;
        }

        /// <summary>When true, tooltip appears to the left of the button instead of right.</summary>
        public bool TooltipOnLeft { get; set; }

        public event EventHandler OnClick;

        public CodeDrawnIconButton(
            IUIStyleProvider styleProvider,
            Texture2D iconTexture,
            BitmapFont tooltipFont,
            IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _styleProvider = styleProvider;
            _iconTexture = iconTexture;
            _tooltipFont = tooltipFont;
            _clientWindowSizeProvider = clientWindowSizeProvider;

            SetSize(ICON_SIZE, ICON_SIZE);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            OnMouseEnter += (_, _) => _state = ButtonState.Hover;
            OnMouseLeave += (_, _) => _state = ButtonState.Normal;

            base.Initialize();
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButton.Left)
            {
                OnClick?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return base.HandleClick(control, eventArgs);
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButton.Left)
            {
                _state = ButtonState.Pressed;
                return true;
            }
            return base.HandleMouseDown(control, eventArgs);
        }

        protected override bool HandleMouseUp(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButton.Left)
            {
                _state = MouseOver ? ButtonState.Hover : ButtonState.Normal;
                return true;
            }
            return base.HandleMouseUp(control, eventArgs);
        }

        // IPostScaleDrawable
        public int PostScaleDrawOrder => _state == ButtonState.Hover ? 10000 : 50;
        public bool SkipRenderTargetDraw => true;

        protected override void OnDrawControl(GameTime gameTime)
        {
            // Draw fill at game resolution (pre-scale)
            DrawFill();
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawIconAndBorder(spriteBatch, scaledPos, scaleFactor);

            if (_state == ButtonState.Hover && !string.IsNullOrEmpty(_tooltipText))
                DrawTooltip(spriteBatch, scaledPos, scaleFactor);
        }

        private void DrawFill()
        {
            var backgroundColor = _state switch
            {
                ButtonState.Pressed => _styleProvider.ButtonPressed,
                ButtonState.Hover => _styleProvider.ButtonHover,
                _ => _styleProvider.ButtonNormal
            };

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bounds, backgroundColor);
            _spriteBatch.End();
        }

        private void DrawIconAndBorder(SpriteBatch spriteBatch, Vector2 scaledPos, float scale)
        {
            var scaledWidth = (int)(ICON_SIZE * scale);
            var scaledHeight = (int)(ICON_SIZE * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            // Tint the icon based on state
            var tint = _state switch
            {
                ButtonState.Pressed => new Color(180, 180, 180),
                ButtonState.Hover => Color.White,
                _ => new Color(220, 220, 220)
            };

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw icon texture scaled
            if (_iconTexture != null)
            {
                spriteBatch.Draw(_iconTexture, scaledBounds, tint);
            }

            // Draw border
            var borderColor = _state == ButtonState.Normal
                ? new Color(_styleProvider.ButtonBorder, 0.5f)
                : _styleProvider.ButtonBorder;
            var borderThickness = Math.Max(1, (int)(_styleProvider.BorderThickness * scale));
            DrawingPrimitives.DrawRectBorder(spriteBatch, scaledBounds, borderColor, borderThickness);

            spriteBatch.End();
        }

        private void DrawTooltip(SpriteBatch spriteBatch, Vector2 scaledPos, float scale)
        {
            if (_tooltipFont == null) return;

            var textSize = _tooltipFont.MeasureString(_tooltipText);
            var tooltipWidth = (int)textSize.Width + TOOLTIP_PAD_H * 2;
            var tooltipHeight = (int)textSize.Height + TOOLTIP_PAD_V * 2;

            var scaledBtnWidth = (int)(ICON_SIZE * scale);

            // Position tooltip on the appropriate side
            var tooltipX = TooltipOnLeft
                ? (int)scaledPos.X - tooltipWidth - 4
                : (int)scaledPos.X + scaledBtnWidth + 4;
            var tooltipY = (int)scaledPos.Y + ((int)(ICON_SIZE * scale) - tooltipHeight) / 2;

            var tooltipBounds = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            spriteBatch.Begin();
            DrawingPrimitives.DrawFilledRect(spriteBatch, tooltipBounds, _styleProvider.TooltipBackground);
            DrawingPrimitives.DrawRectBorder(spriteBatch, tooltipBounds, _styleProvider.ButtonBorder, 1);
            spriteBatch.DrawString(_tooltipFont, _tooltipText,
                new Vector2(tooltipX + TOOLTIP_PAD_H, tooltipY + TOOLTIP_PAD_V),
                _styleProvider.ButtonText);
            spriteBatch.End();
        }
    }
}
