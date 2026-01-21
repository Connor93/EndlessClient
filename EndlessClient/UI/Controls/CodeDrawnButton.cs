using System;
using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// A procedurally-drawn button with hover and pressed states.
    /// Replaces texture-based buttons for code-drawn UI mode.
    /// </summary>
    public class CodeDrawnButton : XNAControl
    {
        private enum ButtonState { Normal, Hover, Pressed }

        private readonly IUIStyleProvider _styleProvider;
        private readonly BitmapFont _font;
        private ButtonState _state = ButtonState.Normal;
        private string _text = string.Empty;

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        public event EventHandler OnClick;

        public CodeDrawnButton(IUIStyleProvider styleProvider, BitmapFont font)
        {
            _styleProvider = styleProvider;
            _font = font;
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

        protected override void OnDrawControl(GameTime gameTime)
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

            // Use transformation matrix to offset drawing to button's screen position
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

            base.OnDrawControl(gameTime);
        }
    }
}
