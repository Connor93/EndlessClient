using System;
using EndlessClient.Rendering;
using EndlessClient.Services;
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
    /// A procedurally-drawn button with hover and pressed states.
    /// Replaces texture-based buttons for code-drawn UI mode.
    /// Implements IPostScaleDrawable for crisp text rendering when scaled.
    /// </summary>
    public class CodeDrawnButton : XNAControl, IPostScaleDrawable
    {
        private enum ButtonState { Normal, Hover, Pressed }

        private readonly IUIStyleProvider _styleProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _scaledFont;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private ButtonState _state = ButtonState.Normal;
        private string _text = string.Empty;

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        public event EventHandler OnClick;

        /// <summary>
        /// Legacy constructor without scaled mode support (for backwards compatibility).
        /// Buttons created with this constructor will not have crisp text in scaled mode.
        /// </summary>
        public CodeDrawnButton(IUIStyleProvider styleProvider, BitmapFont font)
            : this(styleProvider, font, font, null)
        {
        }

        /// <summary>
        /// Full constructor with scaled mode support for crisp text rendering.
        /// </summary>
        public CodeDrawnButton(
            IUIStyleProvider styleProvider,
            BitmapFont font,
            BitmapFont scaledFont,
            IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _styleProvider = styleProvider;
            _font = font;
            _scaledFont = scaledFont ?? font;
            _clientWindowSizeProvider = clientWindowSizeProvider;
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

        // IPostScaleDrawable implementation
        public int PostScaleDrawOrder => 100;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider?.IsScaledMode ?? false;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                // In scaled mode, skip fills here - draw everything in DrawPostScale
                // so each control draws fills + borders + text together for correct z-ordering
            }
            else
            {
                // In normal mode, draw everything
                DrawComplete();
            }

            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            // Check both our visibility AND parent visibility to avoid drawing orphaned buttons
            if (!Visible) return;
            if (ImmediateParent != null && !ImmediateParent.Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            // Draw fills first, then borders and text - all together for correct z-ordering
            DrawFillsScaled(scaledPos, scaleFactor);
            DrawBordersAndText(scaledPos, scaleFactor);
        }

        /// <summary>
        /// Draw fills at scaled coordinates for post-scale rendering.
        /// </summary>
        private void DrawFillsScaled(Vector2 scaledPos, float scale)
        {
            var backgroundColor = _state switch
            {
                ButtonState.Pressed => _styleProvider.ButtonPressed,
                ButtonState.Hover => _styleProvider.ButtonHover,
                _ => _styleProvider.ButtonNormal
            };
            var cornerRadius = _styleProvider.CornerRadius;

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            _spriteBatch.Begin();

            // Draw background
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRect(_spriteBatch, scaledBounds, backgroundColor, (int)(cornerRadius * scale));
            else
                DrawingPrimitives.DrawFilledRect(_spriteBatch, scaledBounds, backgroundColor);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draw only fills for render target phase in scaled mode.
        /// </summary>
        private void DrawFills()
        {
            var backgroundColor = _state switch
            {
                ButtonState.Pressed => _styleProvider.ButtonPressed,
                ButtonState.Hover => _styleProvider.ButtonHover,
                _ => _styleProvider.ButtonNormal
            };
            var cornerRadius = _styleProvider.CornerRadius;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Draw background only
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, backgroundColor, cornerRadius);
            else
                DrawingPrimitives.DrawFilledRect(_spriteBatch, bounds, backgroundColor);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draw borders and text in post-scale phase for crisp rendering.
        /// </summary>
        private void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            var borderColor = _styleProvider.ButtonBorder;
            var textColor = _styleProvider.ButtonText;
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;

            // Select font based on scale
            var font = scale >= 1.75f ? _scaledFont : (scale >= 1.25f ? _scaledFont : _font);

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            _spriteBatch.Begin();

            // Draw border
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, scaledBounds, borderColor, cornerRadius, Math.Max(1, (int)(borderThickness * scale)));
            else
                DrawingPrimitives.DrawRectBorder(_spriteBatch, scaledBounds, borderColor, Math.Max(1, (int)(borderThickness * scale)));

            // Draw text centered
            if (!string.IsNullOrEmpty(_text) && font != null)
            {
                var textSize = font.MeasureString(_text);
                var textPos = new Vector2(
                    scaledPos.X + (scaledWidth - textSize.Width) / 2,
                    scaledPos.Y + (scaledHeight - textSize.Height) / 2);
                _spriteBatch.DrawString(font, _text, textPos, textColor);
            }

            _spriteBatch.End();
        }

        /// <summary>
        /// Draw everything in one pass for non-scaled mode.
        /// </summary>
        private void DrawComplete()
        {
            var backgroundColor = _state switch
            {
                ButtonState.Pressed => _styleProvider.ButtonPressed,
                ButtonState.Hover => _styleProvider.ButtonHover,
                _ => _styleProvider.ButtonNormal
            };
            var borderColor = _styleProvider.ButtonBorder;
            var textColor = _styleProvider.ButtonText;
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Draw background
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, backgroundColor, cornerRadius);
            else
                DrawingPrimitives.DrawFilledRect(_spriteBatch, bounds, backgroundColor);

            // Draw border
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, borderColor, cornerRadius, borderThickness);
            else
                DrawingPrimitives.DrawRectBorder(_spriteBatch, bounds, borderColor, borderThickness);

            // Draw text centered
            if (!string.IsNullOrEmpty(_text) && _font != null)
            {
                var textSize = _font.MeasureString(_text);
                var textPos = new Vector2(
                    (bounds.Width - textSize.Width) / 2,
                    (bounds.Height - textSize.Height) / 2);
                _spriteBatch.DrawString(_font, _text, textPos, textColor);
            }

            _spriteBatch.End();
        }
    }
}
