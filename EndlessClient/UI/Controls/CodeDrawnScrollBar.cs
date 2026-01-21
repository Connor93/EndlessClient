using System;
using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// A procedurally-drawn scrollbar that replaces texture-based scrollbars for code-drawn UI mode.
    /// </summary>
    public class CodeDrawnScrollBar : XNAControl, IScrollHandler
    {
        private readonly IUIStyleProvider _styleProvider;

        private Rectangle _scrollArea;
        private int _totalHeight;

        public int ScrollOffset { get; private set; }
        public int LinesToRender { get; set; }

        private Rectangle ThumbBounds
        {
            get
            {
                if (_totalHeight <= LinesToRender)
                    return new Rectangle(2, 16, DrawArea.Width - 4, DrawArea.Height - 32);

                var trackHeight = DrawArea.Height - 32; // excluding arrow buttons
                var thumbHeight = Math.Max(20, (int)(trackHeight * ((float)LinesToRender / _totalHeight)));
                var maxOffset = _totalHeight - LinesToRender;
                var thumbY = 16 + (int)((trackHeight - thumbHeight) * ((float)ScrollOffset / maxOffset));

                return new Rectangle(2, thumbY, DrawArea.Width - 4, thumbHeight);
            }
        }

        public CodeDrawnScrollBar(IUIStyleProvider styleProvider, Vector2 position, Vector2 size)
        {
            _styleProvider = styleProvider;
            DrawPosition = position;
            SetSize((int)size.X, (int)size.Y);
            ScrollOffset = 0;
            _scrollArea = new Rectangle(0, 16, (int)size.X, (int)size.Y - 32);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);
            base.Initialize();
        }

        public void UpdateDimensions(int numberOfLines)
        {
            _totalHeight = numberOfLines;
        }

        public void ScrollToTop()
        {
            ScrollOffset = 0;
        }

        public void ScrollToEnd()
        {
            if (_totalHeight > LinesToRender)
                ScrollOffset = _totalHeight - LinesToRender;
        }

        public void SetScrollOffset(int offset)
        {
            ScrollOffset = Math.Clamp(offset, 0, Math.Max(0, _totalHeight - LinesToRender));
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            var trackColor = _styleProvider.PanelBackground;
            var thumbColor = _styleProvider.ButtonNormal;
            var arrowColor = _styleProvider.ButtonText;
            var borderColor = _styleProvider.PanelBorder;

            _spriteBatch.Begin(transformMatrix: transform);

            // Track background
            var trackBounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, trackBounds, trackColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, trackBounds, borderColor, 1);

            // Up arrow button area
            var upBounds = new Rectangle(0, 0, DrawArea.Width, 16);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, upBounds, thumbColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, upBounds, borderColor, 1);

            // Down arrow button area
            var downBounds = new Rectangle(0, DrawArea.Height - 16, DrawArea.Width, 16);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, downBounds, thumbColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, downBounds, borderColor, 1);

            // Thumb
            if (_totalHeight > LinesToRender)
            {
                var thumbBounds = ThumbBounds;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbBounds, _styleProvider.ButtonHover);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbBounds, borderColor, 1);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            var localY = eventArgs.Position.Y - DrawAreaWithParentOffset.Y;

            // Up arrow clicked
            if (localY < 16)
            {
                if (ScrollOffset > 0)
                    ScrollOffset--;
                return true;
            }

            // Down arrow clicked
            if (localY > DrawArea.Height - 16)
            {
                if (ScrollOffset < _totalHeight - LinesToRender)
                    ScrollOffset++;
                return true;
            }

            // Track clicked (page up/down)
            var thumbBounds = ThumbBounds;
            if (localY < thumbBounds.Y)
            {
                ScrollOffset = Math.Max(0, ScrollOffset - LinesToRender);
            }
            else if (localY > thumbBounds.Y + thumbBounds.Height)
            {
                ScrollOffset = Math.Min(_totalHeight - LinesToRender, ScrollOffset + LinesToRender);
            }

            return true;
        }

        protected override bool HandleMouseWheelMoved(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (_totalHeight <= LinesToRender)
                return false;

            var dif = eventArgs.ScrollWheelDelta / -120;
            var newOffset = ScrollOffset + dif;
            ScrollOffset = Math.Clamp(newOffset, 0, _totalHeight - LinesToRender);

            return true;
        }
    }
}
