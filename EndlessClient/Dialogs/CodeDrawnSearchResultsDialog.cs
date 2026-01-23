using System;
using System.Collections.Generic;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn search results dialog with post-scale rendering support.
    /// Used by #item and #npc commands when multiple matches are found.
    /// </summary>
    public class CodeDrawnSearchResultsDialog : XNADialog, IPostScaleDrawable
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;
        private readonly BitmapFont _scaledFont;
        private readonly BitmapFont _scaledHeaderFont;
        private readonly List<SearchResultItem> _items;

        private const int DialogWidth = 290;
        private const int DialogHeight = 280;
        private const int ListAreaTop = 45;
        private const int ListAreaHeight = 190;
        private const int ItemHeight = 20;
        private const int VisibleItems = 9;

        private string _title = string.Empty;
        private int _scrollOffset;
        private int _hoveredIndex = -1;
        private Rectangle _cancelButtonRect;
        private Rectangle _scrollUpRect;
        private Rectangle _scrollDownRect;
        private bool _cancelButtonHovered;
        private bool _wasMouseDown;
        private int _previousScrollWheelValue;

        public string Title
        {
            get => _title;
            set => _title = value;
        }

        public CodeDrawnSearchResultsDialog(
            IUIStyleProvider styleProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            BitmapFont font,
            BitmapFont headerFont,
            BitmapFont scaledFont,
            BitmapFont scaledHeaderFont)
        {
            _styleProvider = styleProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _font = font;
            _headerFont = headerFont;
            _scaledFont = scaledFont;
            _scaledHeaderFont = scaledHeaderFont;
            _items = new List<SearchResultItem>();

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);
        }

        public void AddItem(string text, Action onClick)
        {
            _items.Add(new SearchResultItem(text, onClick));
        }

        public void ClearItems()
        {
            _items.Clear();
            _scrollOffset = 0;
        }

        /// <summary>
        /// Closes the dialog with Cancel result. This is a public wrapper around the protected Close method.
        /// </summary>
        public void Close()
        {
            Close(XNADialogResult.Cancel);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
            CenterInGameView();
        }

        public override void CenterInGameView()
        {
            int centerWidth, centerHeight;
            if (XNADialog.GameViewportProvider != null)
            {
                centerWidth = XNADialog.GameViewportProvider.GameWidth;
                centerHeight = XNADialog.GameViewportProvider.GameHeight;
            }
            else if (Game != null)
            {
                centerWidth = Game.GraphicsDevice.Viewport.Width;
                centerHeight = Game.GraphicsDevice.Viewport.Height;
            }
            else
            {
                centerWidth = 640;
                centerHeight = 480;
            }

            var centerX = (centerWidth - DialogWidth) / 2;
            var centerY = (centerHeight - DialogHeight) / 2;
            DrawPosition = new Vector2(centerX, centerY);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            var rawMousePos = new Point(mouseState.X, mouseState.Y);
            var mousePos = TransformMousePosition(rawMousePos);
            var isMouseDown = mouseState.LeftButton == ButtonState.Pressed;

            // Calculate positions based on current draw position
            var pos = DrawPositionWithParentOffset;

            // Cancel button rect
            var buttonWidth = 80;
            var buttonHeight = 24;
            _cancelButtonRect = new Rectangle(
                (int)pos.X + (DialogWidth - buttonWidth) / 2,
                (int)pos.Y + DialogHeight - buttonHeight - 12,
                buttonWidth, buttonHeight);
            _cancelButtonHovered = _cancelButtonRect.Contains(mousePos);

            // Scroll button rects
            var scrollX = (int)pos.X + DialogWidth - 24;
            _scrollUpRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop, 16, 16);
            _scrollDownRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop + ListAreaHeight - 16, 16, 16);

            // List area for hover detection
            var listRect = new Rectangle((int)pos.X + 8, (int)pos.Y + ListAreaTop, DialogWidth - 40, ListAreaHeight);
            if (listRect.Contains(mousePos))
            {
                var relativeY = mousePos.Y - ((int)pos.Y + ListAreaTop);
                var hoverIdx = relativeY / ItemHeight + _scrollOffset;
                _hoveredIndex = hoverIdx >= 0 && hoverIdx < _items.Count ? hoverIdx : -1;
            }
            else
            {
                _hoveredIndex = -1;
            }

            // Handle mousewheel scrolling when mouse is over dialog
            var dialogRect = new Rectangle((int)pos.X, (int)pos.Y, DialogWidth, DialogHeight);
            if (dialogRect.Contains(mousePos))
            {
                var scrollDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
                var maxOffset = Math.Max(0, _items.Count - VisibleItems);
                if (scrollDelta > 0 && _scrollOffset > 0)
                    _scrollOffset--;
                else if (scrollDelta < 0 && _scrollOffset < maxOffset)
                    _scrollOffset++;
            }
            _previousScrollWheelValue = mouseState.ScrollWheelValue;

            // Handle clicks
            if (_wasMouseDown && !isMouseDown)
            {
                if (_cancelButtonHovered)
                {
                    Close(XNADialogResult.Cancel);
                }
                else if (_scrollUpRect.Contains(mousePos) && _scrollOffset > 0)
                {
                    _scrollOffset--;
                }
                else if (_scrollDownRect.Contains(mousePos) && _scrollOffset < Math.Max(0, _items.Count - VisibleItems))
                {
                    _scrollOffset++;
                }
                else if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    var item = _items[_hoveredIndex];
                    Close(XNADialogResult.OK);
                    item.OnClick?.Invoke();
                }
            }

            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
        }

        // IPostScaleDrawable implementation
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                DrawFills(DrawPositionWithParentOffset);
                base.OnDrawControl(gameTime);
                return;
            }

            DrawComplete(DrawPositionWithParentOffset, 1.0f, _font, _headerFont);
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawPositionWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawBordersAndText(scaledPos, scaleFactor);
        }

        /// <summary>
        /// Draws only fills (for render target in scaled mode)
        /// </summary>
        private void DrawFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DialogWidth, DialogHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Title bar fill
            var titleRect = new Rectangle((int)pos.X + 1, (int)pos.Y + 1, DialogWidth - 2, 30);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, titleRect, _styleProvider.TitleBarBackground);

            // List area background
            var listRect = new Rectangle((int)pos.X + 8, (int)pos.Y + ListAreaTop, DialogWidth - 40, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, listRect, new Color(0, 0, 0, 60));

            // Row fills
            for (int i = 0; i < VisibleItems && _scrollOffset + i < _items.Count; i++)
            {
                var rowY = (int)pos.Y + ListAreaTop + (i * ItemHeight);
                var rowRect = new Rectangle((int)pos.X + 8, rowY, DialogWidth - 40, ItemHeight);

                var itemIndex = _scrollOffset + i;
                Color rowColor;
                if (itemIndex == _hoveredIndex)
                    rowColor = new Color(255, 255, 255, 30);
                else
                    rowColor = i % 2 == 0 ? new Color(70, 60, 50, 80) : new Color(60, 50, 40, 80);

                DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowColor);
            }

            // Scrollbar fills
            var scrollX = (int)pos.X + DialogWidth - 24;
            var scrollTrackRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop, 16, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, scrollTrackRect, new Color(40, 35, 30, 200));

            // Scroll buttons
            var upColor = _scrollOffset > 0 ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollUpRect, upColor);

            var maxOffset = Math.Max(0, _items.Count - VisibleItems);
            var downColor = _scrollOffset < maxOffset ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollDownRect, downColor);

            // Scroll thumb
            if (_items.Count > VisibleItems)
            {
                var thumbTrackHeight = ListAreaHeight - 36;
                var thumbHeight = Math.Max(10, thumbTrackHeight * VisibleItems / _items.Count);
                var thumbY = (int)pos.Y + ListAreaTop + 17 + (int)((thumbTrackHeight - thumbHeight) * _scrollOffset / (float)maxOffset);
                var thumbRect = new Rectangle(scrollX + 2, thumbY, 12, thumbHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, _styleProvider.ButtonNormal);
            }

            // Cancel button fill
            var buttonColor = _cancelButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _cancelButtonRect, buttonColor);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws borders and text post-scale for crisp rendering
        /// </summary>
        private void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            _spriteBatch.Begin();

            // Select font based on scale
            BitmapFont font, headerFont;
            if (scale >= 1.75f)
            {
                font = _scaledFont;
                headerFont = _scaledHeaderFont;
            }
            else if (scale >= 1.25f)
            {
                font = _headerFont;
                headerFont = _headerFont;
            }
            else
            {
                font = _font;
                headerFont = _headerFont;
            }

            var panelWidth = (int)(DialogWidth * scale);
            var panelHeight = (int)(DialogHeight * scale);

            // Panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, _styleProvider.CornerRadius, Math.Max(1, (int)(2 * scale)));

            // Title text
            if (!string.IsNullOrEmpty(_title))
            {
                _spriteBatch.DrawString(headerFont, _title, new Vector2(scaledPos.X + 16 * scale, scaledPos.Y + 8 * scale), _styleProvider.TitleBarText);
            }

            // List area border
            var listRect = new Rectangle(
                (int)(scaledPos.X + 8 * scale),
                (int)(scaledPos.Y + ListAreaTop * scale),
                (int)((DialogWidth - 40) * scale),
                (int)(ListAreaHeight * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, listRect, _styleProvider.PanelBorder, 1);

            // Item text
            for (int i = 0; i < VisibleItems && _scrollOffset + i < _items.Count; i++)
            {
                var itemIndex = _scrollOffset + i;
                var item = _items[itemIndex];
                var rowY = (int)(scaledPos.Y + (ListAreaTop + i * ItemHeight) * scale);

                var textColor = itemIndex == _hoveredIndex
                    ? new Color(150, 230, 255)
                    : _styleProvider.TextHighlight;

                _spriteBatch.DrawString(font, item.Text, new Vector2(scaledPos.X + 12 * scale, rowY + 2 * scale), textColor);
            }

            // Scrollbar borders and arrows
            var scrollX = (int)(scaledPos.X + (DialogWidth - 24) * scale);
            var scrollTrackRect = new Rectangle(scrollX, (int)(scaledPos.Y + ListAreaTop * scale), (int)(16 * scale), (int)(ListAreaHeight * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, scrollTrackRect, new Color(80, 70, 60), 1);

            var upRect = new Rectangle(scrollX, (int)(scaledPos.Y + ListAreaTop * scale), (int)(16 * scale), (int)(16 * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, upRect, Color.Black, 1);
            _spriteBatch.DrawString(font, "▲", new Vector2(upRect.X + 3 * scale, upRect.Y + 1 * scale), Color.White);

            var downRect = new Rectangle(scrollX, (int)(scaledPos.Y + (ListAreaTop + ListAreaHeight - 16) * scale), (int)(16 * scale), (int)(16 * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, downRect, Color.Black, 1);
            _spriteBatch.DrawString(font, "▼", new Vector2(downRect.X + 3 * scale, downRect.Y + 1 * scale), Color.White);

            // Scroll thumb border
            if (_items.Count > VisibleItems)
            {
                var thumbTrackHeight = (int)((ListAreaHeight - 36) * scale);
                var thumbHeight = Math.Max((int)(10 * scale), thumbTrackHeight * VisibleItems / _items.Count);
                var maxOffset = _items.Count - VisibleItems;
                var thumbY = (int)(scaledPos.Y + (ListAreaTop + 17) * scale) + (int)((thumbTrackHeight - thumbHeight) * _scrollOffset / (float)maxOffset);
                var thumbRect = new Rectangle(scrollX + (int)(2 * scale), thumbY, (int)(12 * scale), thumbHeight);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, new Color(120, 110, 100), 1);
            }

            // Cancel button border and text
            var buttonWidth = (int)(80 * scale);
            var buttonHeight = (int)(24 * scale);
            var buttonRect = new Rectangle(
                (int)(scaledPos.X + (DialogWidth - 80) / 2 * scale),
                (int)(scaledPos.Y + (DialogHeight - 24 - 12) * scale),
                buttonWidth, buttonHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, buttonRect, Color.Black, 1);

            var buttonText = "Cancel";
            var textSize = headerFont.MeasureString(buttonText);
            var textPos = new Vector2(
                buttonRect.X + (buttonRect.Width - textSize.Width) / 2,
                buttonRect.Y + (buttonRect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(headerFont, buttonText, textPos, Color.White);

            _spriteBatch.End();
        }

        /// <summary>
        /// Complete drawing for non-scaled mode
        /// </summary>
        private void DrawComplete(Vector2 pos, float scale, BitmapFont font, BitmapFont headerFont)
        {
            _spriteBatch.Begin();

            // Panel background with rounded corners
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DialogWidth, DialogHeight);
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bgRect, _styleProvider.PanelBackground, _styleProvider.CornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, _styleProvider.CornerRadius, 2);

            // Title bar
            var titleRect = new Rectangle((int)pos.X + 1, (int)pos.Y + 1, DialogWidth - 2, 30);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, titleRect, _styleProvider.TitleBarBackground);

            // Title text
            if (!string.IsNullOrEmpty(_title))
            {
                _spriteBatch.DrawString(headerFont, _title, new Vector2(pos.X + 16, pos.Y + 8), _styleProvider.TitleBarText);
            }

            // List area background
            var listRect = new Rectangle((int)pos.X + 8, (int)pos.Y + ListAreaTop, DialogWidth - 40, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, listRect, new Color(0, 0, 0, 60));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, listRect, _styleProvider.PanelBorder, 1);

            // Items
            for (int i = 0; i < VisibleItems && _scrollOffset + i < _items.Count; i++)
            {
                var itemIndex = _scrollOffset + i;
                var item = _items[itemIndex];
                var rowY = (int)pos.Y + ListAreaTop + (i * ItemHeight);
                var rowRect = new Rectangle((int)pos.X + 8, rowY, DialogWidth - 40, ItemHeight);

                // Row background
                Color rowColor;
                if (itemIndex == _hoveredIndex)
                    rowColor = new Color(255, 255, 255, 30);
                else
                    rowColor = i % 2 == 0 ? new Color(70, 60, 50, 80) : new Color(60, 50, 40, 80);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowColor);

                // Item text
                var textColor = itemIndex == _hoveredIndex
                    ? new Color(150, 230, 255)
                    : _styleProvider.TextHighlight;
                _spriteBatch.DrawString(font, item.Text, new Vector2(pos.X + 12, rowY + 2), textColor);
            }

            // Scrollbar
            var scrollX = (int)pos.X + DialogWidth - 24;
            var scrollTrackRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop, 16, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, scrollTrackRect, new Color(40, 35, 30, 200));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, scrollTrackRect, new Color(80, 70, 60), 1);

            // Up button
            _scrollUpRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop, 16, 16);
            var upColor = _scrollOffset > 0 ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollUpRect, upColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _scrollUpRect, Color.Black, 1);
            _spriteBatch.DrawString(font, "▲", new Vector2(_scrollUpRect.X + 3, _scrollUpRect.Y + 1), Color.White);

            // Down button
            _scrollDownRect = new Rectangle(scrollX, (int)pos.Y + ListAreaTop + ListAreaHeight - 16, 16, 16);
            var maxOffset = Math.Max(0, _items.Count - VisibleItems);
            var downColor = _scrollOffset < maxOffset ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollDownRect, downColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _scrollDownRect, Color.Black, 1);
            _spriteBatch.DrawString(font, "▼", new Vector2(_scrollDownRect.X + 3, _scrollDownRect.Y + 1), Color.White);

            // Scroll thumb
            if (_items.Count > VisibleItems)
            {
                var thumbTrackHeight = ListAreaHeight - 36;
                var thumbHeight = Math.Max(10, thumbTrackHeight * VisibleItems / _items.Count);
                var thumbY = (int)pos.Y + ListAreaTop + 17 + (int)((thumbTrackHeight - thumbHeight) * _scrollOffset / (float)maxOffset);
                var thumbRect = new Rectangle(scrollX + 2, thumbY, 12, thumbHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, _styleProvider.ButtonNormal);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, new Color(120, 110, 100), 1);
            }

            // Cancel button
            var buttonWidth = 80;
            var buttonHeight = 24;
            _cancelButtonRect = new Rectangle(
                (int)pos.X + (DialogWidth - buttonWidth) / 2,
                (int)pos.Y + DialogHeight - buttonHeight - 12,
                buttonWidth, buttonHeight);

            var buttonColor = _cancelButtonHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _cancelButtonRect, buttonColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _cancelButtonRect, Color.Black, 1);

            var buttonText = "Cancel";
            var textSize = headerFont.MeasureString(buttonText);
            var textPos = new Vector2(
                _cancelButtonRect.X + (_cancelButtonRect.Width - textSize.Width) / 2,
                _cancelButtonRect.Y + (_cancelButtonRect.Height - textSize.Height) / 2);
            _spriteBatch.DrawString(headerFont, buttonText, textPos, Color.White);

            _spriteBatch.End();
        }

        private Point TransformMousePosition(Point position)
        {
            if (!_clientWindowSizeProvider.IsScaledMode)
                return position;

            var offset = _clientWindowSizeProvider.RenderOffset;
            var scale = _clientWindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Clamp(gameX, 0, _clientWindowSizeProvider.GameWidth - 1),
                Math.Clamp(gameY, 0, _clientWindowSizeProvider.GameHeight - 1));
        }

        // Absorb clicks to prevent click-through
        protected override bool HandleClick(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs) => true;
        protected override bool HandleMouseDown(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs) => true;

        private class SearchResultItem
        {
            public string Text { get; }
            public Action OnClick { get; }

            public SearchResultItem(string text, Action onClick)
            {
                Text = text;
                OnClick = onClick;
            }
        }
    }
}
