using System;
using EndlessClient.Content;
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
        private readonly IContentProvider _contentProvider;
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
            : this(styleProvider, font, (IContentProvider)null, clientWindowSizeProvider)
        {
        }

        /// <summary>
        /// Full constructor with adaptive font scaling via FontScaleHelper.
        /// </summary>
        public CodeDrawnButton(
            IUIStyleProvider styleProvider,
            BitmapFont font,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _styleProvider = styleProvider;
            _font = font;
            _contentProvider = contentProvider;
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
        public bool SkipRenderTargetDraw => true;

        protected override void OnDrawControl(GameTime gameTime)
        {
            // Draw fills into the render target (they scale fine as solid shapes).
            // Borders and text are drawn later in DrawPostScale at native resolution for crispness.
            DrawFills();

            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (_spriteBatch == null) return;
            if (!Visible) return;
            if (ImmediateParent != null && !ImmediateParent.Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawBordersAndText(scaledPos, scaleFactor);
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

            // Select font based on scale using adaptive helper
            var font = _contentProvider != null
                ? FontScaleHelper.GetScaledFont(_contentProvider, scale)
                : _font;

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            _spriteBatch.Begin();

            // Draw border
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, scaledBounds, borderColor, (int)(cornerRadius * scale), Math.Max(1, (int)(borderThickness * scale)));
            else
                DrawingPrimitives.DrawRectBorder(_spriteBatch, scaledBounds, borderColor, Math.Max(1, (int)(borderThickness * scale)));

            // Draw text with shadow for contrast
            if (!string.IsNullOrEmpty(_text) && font != null)
            {
                var textSize = font.MeasureString(_text);
                var textPos = new Vector2(
                    (int)(scaledPos.X + (scaledWidth - textSize.Width) / 2),
                    (int)(scaledPos.Y + (scaledHeight - textSize.Height) / 2));

                // Shadow (1px offset, dark)
                var shadowColor = new Color(0, 0, 0, 180);
                _spriteBatch.DrawString(font, _text, textPos + new Vector2(1, 1), shadowColor);

                // Main text
                _spriteBatch.DrawString(font, _text, textPos, textColor);
            }

            _spriteBatch.End();
        }


    }
}
