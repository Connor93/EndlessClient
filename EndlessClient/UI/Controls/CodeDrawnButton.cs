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
        /// When true, the button's own DrawPostScale is suppressed.
        /// Use this when a parent dialog manually draws the button in its own DrawPostScale
        /// to prevent double-draw, while keeping the button parented for input handling.
        /// </summary>
        public bool SuppressPostScaleDraw { get; set; }

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
            // All drawing (fills, borders, text) is done in DrawPostScale for correct z-ordering.
            // Don't draw anything here or in children.
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (_spriteBatch == null) return;
            if (!Visible) return;
            if (SuppressPostScaleDraw) return;
            if (ImmediateParent != null && !ImmediateParent.Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawAllPostScale(scaledPos, scaleFactor);
        }

        /// <summary>
        /// Draw everything (fill, border, text) in the post-scale phase for correct z-ordering.
        /// </summary>
        private void DrawAllPostScale(Vector2 scaledPos, float scale)
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

            // Select font based on scale using adaptive helper
            var font = _contentProvider != null
                ? FontScaleHelper.GetScaledFont(_contentProvider, scale)
                : _font;

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            _spriteBatch.Begin();

            // Draw fill
            if (cornerRadius > 0)
                DrawingPrimitives.DrawRoundedRect(_spriteBatch, scaledBounds, backgroundColor, (int)(cornerRadius * scale));
            else
                DrawingPrimitives.DrawFilledRect(_spriteBatch, scaledBounds, backgroundColor);

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

                _spriteBatch.DrawString(font, _text, textPos, textColor);
            }

            _spriteBatch.End();
        }




    }
}
