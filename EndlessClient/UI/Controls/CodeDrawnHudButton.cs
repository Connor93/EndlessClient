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
    /// A small procedurally-drawn HUD button for the top bar.
    /// Displays a single character like [Q] or [E].
    /// Implements IPostScaleDrawable for crisp text rendering when scaled.
    /// </summary>
    public class CodeDrawnHudButton : XNAControl, IPostScaleDrawable
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

        public CodeDrawnHudButton(
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
        public int PostScaleDrawOrder => 50; // Draw above panels but below dialogs
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider?.IsScaledMode ?? false;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                DrawFills();
            }
            else
            {
                DrawComplete();
            }

            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawBordersAndText(scaledPos, scaleFactor);
        }

        private void DrawFills()
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

        private void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            var borderColor = _styleProvider.ButtonBorder;
            var textColor = _styleProvider.ButtonText;
            var borderThickness = _styleProvider.BorderThickness;

            var font = scale >= 1.75f ? _scaledFont : (scale >= 1.25f ? _scaledFont : _font);

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);
            var scaledBounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            _spriteBatch.Begin();

            // Draw border
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
            var borderThickness = _styleProvider.BorderThickness;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bounds, backgroundColor);

            // Border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bounds, borderColor, borderThickness);

            // Text centered
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
