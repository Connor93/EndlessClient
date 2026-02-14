using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Chat;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Config;
using EOLib.Domain.Chat;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnChatPanel : CodeDrawnHudPanelBase, IChatPanel
    {
        private readonly IChatActions _chatActions;
        private readonly IChatRenderableGenerator _chatRenderableGenerator;
        private readonly IChatProvider _chatProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _chatFont;
        private readonly BitmapFont _labelFont;
        private readonly IContentProvider _contentProvider;

        // Base font pixel size for dynamic scaling (14px = FontSize11)
        private const int BaseFontPixelSize = 14;

        // Inline scroll state (replaces ScrollBar child which can't render in post-scale mode)
        private int _scrollOffset;
        private int _totalLines;
        private bool _isDraggingThumb;
        private int _dragStartY;
        private int _dragStartOffset;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly Dictionary<ChatTab, CodeDrawnChatTabInfo> _tabs;

        private const int PanelWidth = 489;
        private const int PanelHeight = 186; // 10 lines (130) + extra buffer (8) + gaps + input bar (22) + tabs (14) + padding
        private const int VisibleLines = 10; // Reduced from 10 to fit larger FontSize10 text
        private const int InputBarHeight = 22;

        // Collapse/expand state
        private const int CollapsedButtonSize = 28;
        private const int MinimizeButtonSize = 12;
        private const int BadgeSize = 16;
        private bool _isCollapsed;
        private int _unreadCount;
        private Dictionary<ChatTab, int> _lastSeenCounts = new Dictionary<ChatTab, int>();
        private int _lastScrollWheelValue;

        // Scrollbar layout constants (in panel-local unscaled coords)
        private const int ScrollBarWidth = 16;
        private const int ScrollArrowHeight = 16;
        private const int ScrollBarLeft = PanelWidth - ScrollBarWidth - 4; // 469
        private const int ScrollBarTop = 2;
        private int ScrollBarHeight => VisibleLines * 13 + 8; // message area height = 138
        private int ScrollTrackHeight => ScrollBarHeight - ScrollArrowHeight * 2;
        private int MaxScrollOffset => Math.Max(0, _totalLines - VisibleLines);

        // Integrated text input
        private ChatInputTextBox _inputTextBox;

        public ChatTab CurrentTab => _tabs.Single(x => x.Value.Active).Key;

        // Properties to expose integrated text input for chat controller
        public string InputText
        {
            get => _inputTextBox?.Text ?? "";
            set { if (_inputTextBox != null) _inputTextBox.Text = value; }
        }

        public bool InputSelected
        {
            get => _inputTextBox?.Selected ?? false;
            set { if (_inputTextBox != null) _inputTextBox.Selected = value; }
        }

        // Events for chat input
        public event EventHandler OnEnterPressed;
        public event EventHandler OnInputClicked;
        public event EventHandler OnInputTextChanged;

        public CodeDrawnChatPanel(INativeGraphicsManager nativeGraphicsManager,
                                  IChatActions chatActions,
                                  IChatRenderableGenerator chatRenderableGenerator,
                                  IChatProvider chatProvider,
                                  IHudControlProvider hudControlProvider,
                                  IUIStyleProvider styleProvider,
                                  IGraphicsDeviceProvider graphicsDeviceProvider,
                                  IContentProvider contentProvider,
                                  IClientWindowSizeProvider clientWindowSizeProvider,
                                  IConfigurationProvider configurationProvider)
            : base(clientWindowSizeProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _chatActions = chatActions;
            _chatRenderableGenerator = chatRenderableGenerator;
            _chatProvider = chatProvider;
            _hudControlProvider = hudControlProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _chatFont = contentProvider.Fonts[Constants.FontSize11];
            _contentProvider = contentProvider;
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            DrawArea = new Rectangle(102, 280, PanelWidth, PanelHeight); // Y position adjusted for taller panel

            // Create integrated text input box with WASD filtering
            // Position: below message area (130px + 8 buffer) + gap (4) = 146, relative to panel
            var inputY = 4 + VisibleLines * 13 + 8 + 4 + 3; // message area top + height + buffer + gap + padding inside input bar
            _inputTextBox = new ChatInputTextBox(configurationProvider, Rectangle.Empty, Constants.FontSize11, caretTexture: contentProvider.Textures[ContentProvider.Cursor], clientWindowSizeProvider: clientWindowSizeProvider)
            {
                MaxChars = 140,
                MaxWidth = PanelWidth - 40,
                DrawArea = new Rectangle(18, inputY, PanelWidth - 40, InputBarHeight - 4), // 18px left for "> " prompt
                Selected = true
            };
            _inputTextBox.SetParentControl(this);
            _inputTextBox.OnEnterPressed += (_, _) => OnEnterPressed?.Invoke(this, EventArgs.Empty);
            _inputTextBox.OnClicked += (_, _) => OnInputClicked?.Invoke(this, EventArgs.Empty);
            _inputTextBox.OnTextChanged += (_, _) => OnInputTextChanged?.Invoke(this, EventArgs.Empty);

            _tabs = new Dictionary<ChatTab, CodeDrawnChatTabInfo>
            {
                { ChatTab.Local, new CodeDrawnChatTabInfo("scr", true) },
                { ChatTab.Global, new CodeDrawnChatTabInfo("glb", false) },
                { ChatTab.Group, new CodeDrawnChatTabInfo("grp", false) },
                { ChatTab.System, new CodeDrawnChatTabInfo("sys", false) },
                { ChatTab.Private1, new CodeDrawnChatTabInfo("", false) { IsLarge = true, Visible = false } },
                { ChatTab.Private2, new CodeDrawnChatTabInfo("", false) { IsLarge = true, Visible = false } },
            };
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            _inputTextBox.Initialize();

            // Initialize max width after font is loaded
            _inputTextBox.MaxWidth = PanelWidth - 30;

            base.Initialize();
        }

        protected override void OnUnconditionalUpdateControl(GameTime gameTime)
        {
            foreach (var pair in _tabs.Where(x => x.Value.Visible))
            {
                var tab = pair.Key;
                var info = pair.Value;

                if (!info.CachedChat.SetEquals(_chatProvider.AllChat[tab]))
                {
                    info.CachedChat = _chatProvider.AllChat[tab].ToHashSet();
                    info.Renderables = _chatRenderableGenerator.GenerateChatRenderables(info.CachedChat).ToList();

                    if (info.Active && !_isCollapsed)
                    {
                        _totalLines = info.Renderables.Count;
                        ScrollToEnd();
                    }
                    else
                    {
                        info.CachedScrollOffset = Math.Max(0, info.Renderables.Count - VisibleLines);
                        info.HasUnread = true;
                    }
                }
            }

            // Track unread messages while collapsed (excluding System tab)
            if (_isCollapsed)
            {
                _unreadCount = 0;
                foreach (var tab in _tabs.Keys.Where(t => t != ChatTab.System))
                {
                    var currentCount = _chatProvider.AllChat[tab].Count;
                    if (_lastSeenCounts.TryGetValue(tab, out var lastCount) && currentCount > lastCount)
                        _unreadCount += currentCount - lastCount;
                }
            }

            // Handle scroll wheel
            if (!_isCollapsed)
            {
                var mouseState = Mouse.GetState();
                var wheelDelta = mouseState.ScrollWheelValue - _lastScrollWheelValue;
                _lastScrollWheelValue = mouseState.ScrollWheelValue;
                if (wheelDelta != 0)
                {
                    // Check if mouse is over the panel
                    var transformedMouse = TransformMousePosition(new Point(mouseState.X, mouseState.Y));
                    var panelRect = new Rectangle(DrawAreaWithParentOffset.X, DrawAreaWithParentOffset.Y, PanelWidth, PanelHeight);
                    if (panelRect.Contains(transformedMouse))
                    {
                        if (wheelDelta > 0)
                            ScrollUp(3);
                        else
                            ScrollDown(3);
                    }
                }
            }

            // Handle thumb dragging
            if (_isDraggingThumb)
            {
                var mouseState = Mouse.GetState();
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    _isDraggingThumb = false;
                }
                else if (MaxScrollOffset > 0)
                {
                    var currentMousePos = TransformMousePosition(new Point(mouseState.X, mouseState.Y));
                    var localY = currentMousePos.Y - DrawAreaWithParentOffset.Y;

                    var thumbHeight = GetThumbHeight();
                    var usableTrack = ScrollTrackHeight - thumbHeight;
                    if (usableTrack > 0)
                    {
                        var dragDelta = localY - _dragStartY;
                        var offsetDelta = (int)((dragDelta / (float)usableTrack) * MaxScrollOffset);
                        _scrollOffset = Math.Clamp(_dragStartOffset + offsetDelta, 0, MaxScrollOffset);
                    }
                }
            }

            base.OnUnconditionalUpdateControl(gameTime);
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            // Transform mouse position for scaled mode
            var mousePos = TransformMousePosition(eventArgs.Position);
            var panelPos = DrawAreaWithParentOffset;

            // If collapsed, check if clicking the collapsed button to expand
            if (_isCollapsed)
            {
                var btnRect = new Rectangle(panelPos.X, panelPos.Y, CollapsedButtonSize, CollapsedButtonSize);
                if (btnRect.Contains(mousePos))
                {
                    _isCollapsed = false;
                    _unreadCount = 0;
                    _lastSeenCounts.Clear();
                    _inputTextBox.Selected = true;

                    // Expand panel rightward from collapsed button's current position, clamped to screen
                    var expandedX = DrawArea.X;
                    var expandedY = DrawArea.Y;
                    expandedX = Math.Clamp(expandedX, 0, WindowSizeProvider.GameWidth - PanelWidth);
                    expandedY = Math.Clamp(expandedY, 0, WindowSizeProvider.GameHeight - PanelHeight);
                    DrawArea = new Rectangle(expandedX, expandedY, PanelWidth, PanelHeight);

                    // Force re-sync: regenerate renderables from current chat data
                    // to ensure messages received while collapsed are displayed
                    var activeTab = CurrentTab;
                    var activeInfo = _tabs[activeTab];
                    activeInfo.CachedChat = _chatProvider.AllChat[activeTab].ToHashSet();
                    activeInfo.Renderables = _chatRenderableGenerator.GenerateChatRenderables(activeInfo.CachedChat).ToList();
                    _totalLines = activeInfo.Renderables.Count;
                    ScrollToEnd();

                    // Clear cached chat for other tabs so they re-sync on next switch
                    foreach (var otherTab in _tabs.Where(t => t.Key != activeTab && t.Value.Visible))
                        otherTab.Value.CachedChat.Clear();
                }
                return true;
            }

            // Check minimize button (top-left corner of panel)
            var minBtnRect = new Rectangle(
                panelPos.X + 4,
                panelPos.Y + 2,
                MinimizeButtonSize,
                MinimizeButtonSize);
            if (minBtnRect.Contains(mousePos))
            {
                _isCollapsed = true;
                _unreadCount = 0;
                _lastSeenCounts.Clear();
                _inputTextBox.Selected = false;
                foreach (var tab in _tabs.Keys)
                    _lastSeenCounts[tab] = _chatProvider.AllChat[tab].Count;

                // Shrink DrawArea to just the collapsed button (left side / top-left corner)
                DrawArea = new Rectangle(
                    DrawArea.X,
                    DrawArea.Y,
                    CollapsedButtonSize,
                    CollapsedButtonSize);
                return true;
            }

            // Check if clicked on a tab
            foreach (var pair in _tabs.Where(x => x.Value.Visible))
            {
                var tabRect = GetTabRect(pair.Key);
                var absRect = new Rectangle(panelPos.X + tabRect.X, panelPos.Y + tabRect.Y, tabRect.Width, tabRect.Height);

                if (absRect.Contains(mousePos))
                {
                    // Check if close button was clicked for PM tabs
                    if ((pair.Key == ChatTab.Private1 || pair.Key == ChatTab.Private2) && pair.Value.Active)
                    {
                        var closeRect = new Rectangle(absRect.X + 3, absRect.Y + 3, 12, 12);
                        if (closeRect.Contains(mousePos))
                        {
                            ClosePMTab(pair.Key);
                            return true;
                        }
                    }

                    SelectTab(pair.Key);
                    return true;
                }
            }

            return base.HandleClick(control, eventArgs);
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (_isCollapsed) return true;

            if (eventArgs.Button == MouseButton.Right)
            {
                HandleRightClick(eventArgs);
                return true;
            }

            // Check scrollbar interaction (must be in HandleMouseDown to prevent base drag from moving window)
            var mousePos = TransformMousePosition(eventArgs.Position);
            var panelPos = DrawAreaWithParentOffset;
            var localX = mousePos.X - panelPos.X;
            var localY = mousePos.Y - panelPos.Y;

            if (localX >= ScrollBarLeft && localX < ScrollBarLeft + ScrollBarWidth
                && localY >= ScrollBarTop && localY < ScrollBarTop + ScrollBarHeight)
            {
                var sbLocalY = localY - ScrollBarTop;

                // Up arrow
                if (sbLocalY < ScrollArrowHeight)
                {
                    ScrollUp();
                    return true;
                }

                // Down arrow
                if (sbLocalY >= ScrollBarHeight - ScrollArrowHeight)
                {
                    ScrollDown();
                    return true;
                }

                // Track area — page scroll or start thumb drag
                if (_totalLines > VisibleLines)
                {
                    var trackLocalY = sbLocalY - ScrollArrowHeight;
                    var thumbY = GetThumbY();
                    var thumbHeight = GetThumbHeight();

                    if (trackLocalY >= thumbY && trackLocalY < thumbY + thumbHeight)
                    {
                        // Start thumb drag
                        _isDraggingThumb = true;
                        _dragStartY = localY;
                        _dragStartOffset = _scrollOffset;
                    }
                    else if (trackLocalY < thumbY)
                    {
                        _scrollOffset = Math.Max(0, _scrollOffset - VisibleLines);
                    }
                    else
                    {
                        _scrollOffset = Math.Min(MaxScrollOffset, _scrollOffset + VisibleLines);
                    }
                }

                return true;
            }

            return base.HandleMouseDown(control, eventArgs);
        }

        protected override bool HandleDrag(IXNAControl control, MouseEventArgs eventArgs)
        {
            // When dragging the scrollbar thumb, suppress the base DraggableHudPanel behavior
            // which would move the entire window
            if (_isDraggingThumb)
                return true;

            return base.HandleDrag(control, eventArgs);
        }

        private void HandleRightClick(MouseEventArgs eventArgs)
        {
            var clickedYRelativeToTopOfPanel = eventArgs.Position.Y - DrawAreaWithParentOffset.Y;
            var clickedChatRow = (int)Math.Round(clickedYRelativeToTopOfPanel / 13.0) - 1;
            var currentTabInfo = _tabs[CurrentTab];

            if (clickedChatRow >= 0 && _scrollOffset + clickedChatRow < currentTabInfo.CachedChat.Count)
            {
                var who = _chatProvider.AllChat[CurrentTab][_scrollOffset + clickedChatRow].Who;
                if (!string.IsNullOrEmpty(who))
                {
                    // Use integrated text input
                    _inputTextBox.Text = $"!{who} ";
                }
            }
        }


        protected override void OnDrawControl(GameTime gameTime)
        {
            base.OnDrawControl(gameTime);
        }

        public override void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var scaledPos = CalculateScaledPosition(scaleFactor, renderOffset);

            if (_isCollapsed)
            {
                DrawCollapsedButton(scaledPos, scaleFactor);
                return;
            }

            // Draw fills first, then text/borders - each panel complete before next
            DrawPanelFills(scaledPos, scaleFactor);

            // Draw borders/frame post-scale for crispness
            DrawPanelBordersOnly(scaledPos, scaleFactor);

            // Draw chat messages post-scale for crisp text
            DrawChatMessagesScaled(scaledPos, scaleFactor);

            // Draw input textbox text post-scale for crisp text
            DrawInputTextScaled(scaledPos, scaleFactor);

            // Draw scrollbar
            DrawScrollbar(scaledPos, scaleFactor);

            // Draw minimize button in top-left corner
            DrawMinimizeButton(scaledPos, scaleFactor);
        }

        // Required by base class but not used since we override DrawPostScale completely with custom drawing
        protected override void DrawFillsScaled(Vector2 pos, float scale) { }
        protected override void DrawBordersAndTextScaled(Vector2 pos, float scale) { }

        /// <summary>
        /// Draws only the filled backgrounds (no borders) - for render target phase in scaled mode
        /// </summary>
        private void DrawPanelFills(Vector2 pos, float scale)
        {
            var panelWidth = (int)(PanelWidth * scale);
            var panelHeight = (int)(PanelHeight * scale);
            var lineHeight = (int)(13 * scale);
            var visibleLinesHeight = VisibleLines * lineHeight + (int)(8 * scale); // +8 for text descenders
            var inputHeight = (int)(InputBarHeight * scale);
            var padding = (int)(4 * scale);

            _spriteBatch.Begin();

            // Draw panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Draw message area fill
            var messageAreaRect = new Rectangle(
                (int)pos.X + padding,
                (int)pos.Y + padding,
                panelWidth - (int)(30 * scale),
                visibleLinesHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, messageAreaRect, _styleProvider.PanelBackgroundAlt);

            // Draw input bar fill
            var inputBarY = (int)pos.Y + padding + visibleLinesHeight + padding;
            var inputBarRect = new Rectangle(
                (int)pos.X + padding,
                inputBarY,
                panelWidth - (int)(12 * scale),
                inputHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, inputBarRect, _styleProvider.InputBackground);

            // Draw ">" prompt
            _spriteBatch.DrawString(_labelFont, ">", new Vector2(inputBarRect.X + padding, inputBarRect.Y + (int)(3 * scale)), _styleProvider.InputText);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws only the borders/frame (no fills) - for post-scale phase
        /// </summary>
        private void DrawPanelBordersOnly(Vector2 scaledPos, float scale)
        {
            // Calculate dimensions the same way as DrawPanelFills to ensure alignment
            var panelWidth = (int)(PanelWidth * scale);
            var panelHeight = (int)(PanelHeight * scale);
            var lineHeight = (int)(13 * scale);
            var visibleLinesHeight = VisibleLines * lineHeight + (int)(8 * scale);
            var inputHeight = (int)(InputBarHeight * scale);
            var padding = (int)(4 * scale);

            var borderWidth = Math.Max(1, (int)(2 * scale));

            _spriteBatch.Begin();

            // Draw panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, borderWidth);

            // Draw input bar border - use same calculation as DrawPanelFills for alignment
            var inputBarY = (int)scaledPos.Y + padding + visibleLinesHeight + padding;
            var inputBarRect = new Rectangle(
                (int)scaledPos.X + padding,
                inputBarY,
                panelWidth - (int)(12 * scale),
                inputHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, inputBarRect, _styleProvider.PanelBorder, 1);

            // Draw tabs (scaled) - they have their own fills/borders
            DrawTabsScaled(scaledPos, scale);

            _spriteBatch.End();
        }

        private void DrawPanelBackground(Vector2 pos, float scale, bool skipMessageAreaFill = false)
        {
            // Scale dimensions
            var panelWidth = (int)(PanelWidth * scale);
            var panelHeight = (int)(PanelHeight * scale);
            var lineHeight = (int)(13 * scale);
            var visibleLinesHeight = VisibleLines * lineHeight + (int)(8 * scale); // +8 for text descenders
            var inputHeight = (int)(InputBarHeight * scale);
            var padding = (int)(4 * scale);
            var borderWidth = Math.Max(1, (int)(2 * scale));

            _spriteBatch.Begin();

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, borderWidth);

            // Draw message area
            var messageAreaRect = new Rectangle(
                (int)pos.X + padding,
                (int)pos.Y + padding,
                panelWidth - (int)(30 * scale),
                visibleLinesHeight);
            if (!skipMessageAreaFill)
            {
                DrawingPrimitives.DrawFilledRect(_spriteBatch, messageAreaRect, _styleProvider.PanelBackgroundAlt);
            }

            // Draw input bar area
            var inputBarY = (int)pos.Y + padding + visibleLinesHeight + padding;
            var inputBarRect = new Rectangle(
                (int)pos.X + padding,
                inputBarY,
                panelWidth - (int)(12 * scale),
                inputHeight);
            if (!skipMessageAreaFill)
            {
                DrawingPrimitives.DrawFilledRect(_spriteBatch, inputBarRect, _styleProvider.InputBackground);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, inputBarRect, _styleProvider.PanelBorder, 1);

                // Draw ">" prompt
                _spriteBatch.DrawString(_labelFont, ">", new Vector2(inputBarRect.X + padding, inputBarRect.Y + (int)(3 * scale)), _styleProvider.InputText);
            }

            // Draw tabs (scaled)
            DrawTabsScaled(pos, scale);

            _spriteBatch.End();
        }

        private void DrawChatMessages(Vector2 pos)
        {
            // Calculate message area bounds for clipping
            // Use DrawPosition (not pos parameter) because that's what RenderWithClipping uses
            const int gamePadding = 4;
            const int gameMessageAreaWidth = 460; // Panel width minus scrollbar and padding
            const int gameMessageAreaHeight = VisibleLines * 13 + 8; // Extra height for text descenders

            // Scissor rect must match where the renderable draws (using DrawPosition)
            var messageAreaPos = DrawPosition + new Vector2(gamePadding, gamePadding);

            // Set up scissor rectangle to clip text that overflows
            var scissorRect = new Rectangle(
                (int)messageAreaPos.X,
                (int)messageAreaPos.Y,
                gameMessageAreaWidth,
                gameMessageAreaHeight);

            var graphicsDevice = _graphicsDeviceProvider.GraphicsDevice;
            var previousScissorRectangle = graphicsDevice.ScissorRectangle;

            graphicsDevice.ScissorRectangle = scissorRect;

            // Begin spritebatch with scissor test enabled - all renderables draw within this batch
            _spriteBatch.Begin(rasterizerState: _scissorRasterizerState);

            var activeTabInfo = _tabs[CurrentTab];
            foreach (var (ndx, renderable) in activeTabInfo.Renderables.Skip(_scrollOffset).Take(VisibleLines).Select((r, i) => (i, r)))
            {
                renderable.DisplayIndex = ndx;
                renderable.RenderWithClipping(this, _spriteBatch, _chatFont);
            }

            _spriteBatch.End();

            // Restore previous state
            graphicsDevice.ScissorRectangle = previousScissorRectangle;
        }

        private void DrawChatMessagesScaled(Vector2 scaledPos, float scaleFactor)
        {
            // Calculate the scaled message area bounds for clipping
            const int gamePadding = 4;
            const int gameMessageAreaWidth = 460; // Panel width minus scrollbar and padding
            const int gameMessageAreaHeight = VisibleLines * 13 + 8; // Extra height for text descenders

            var messageAreaPos = new Vector2(
                scaledPos.X + gamePadding * scaleFactor,
                scaledPos.Y + gamePadding * scaleFactor);

            // Set up scissor rectangle to clip text that overflows
            var scissorRect = new Rectangle(
                (int)messageAreaPos.X,
                (int)messageAreaPos.Y,
                (int)(gameMessageAreaWidth * scaleFactor),
                (int)(gameMessageAreaHeight * scaleFactor));

            var graphicsDevice = _graphicsDeviceProvider.GraphicsDevice;
            var previousScissorRectangle = graphicsDevice.ScissorRectangle;
            var previousRasterizerState = graphicsDevice.RasterizerState;

            graphicsDevice.ScissorRectangle = scissorRect;

            // Begin spritebatch with scissor test enabled - all renderables draw within this batch
            _spriteBatch.Begin(rasterizerState: _scissorRasterizerState);

            var activeTabInfo = _tabs[CurrentTab];
            foreach (var (ndx, renderable) in activeTabInfo.Renderables.Skip(_scrollOffset).Take(VisibleLines).Select((r, i) => (i, r)))
            {
                renderable.DisplayIndex = ndx;
                renderable.RenderScaledWithClipping(_spriteBatch, FontScaleHelper.GetScaledFont(_contentProvider, BaseFontPixelSize, scaleFactor), messageAreaPos, scaleFactor);
            }

            _spriteBatch.End();

            // Restore previous state
            graphicsDevice.ScissorRectangle = previousScissorRectangle;
        }

        private static readonly RasterizerState _scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };

        private void DrawInputTextScaled(Vector2 scaledPos, float scaleFactor)
        {
            // Calculate using same approach as DrawPanelFills/DrawPanelBordersOnly for alignment
            var lineHeight = (int)(13 * scaleFactor);
            var visibleLinesHeight = VisibleLines * lineHeight + (int)(8 * scaleFactor);
            var padding = (int)(4 * scaleFactor);
            var inputBarY = (int)scaledPos.Y + padding + visibleLinesHeight + padding;

            const int gamePromptWidth = 18; // Width of ">" prompt area
            const int gameInputTextWidth = 440; // Available width for text

            // Get the text from the input textbox
            var text = _inputTextBox?.Text ?? "";
            if (string.IsNullOrEmpty(text))
                return;

            // Calculate the text width and whether we need horizontal scrolling
            var scaledFont = FontScaleHelper.GetScaledFont(_contentProvider, BaseFontPixelSize, scaleFactor);
            var textSize = scaledFont.MeasureString(text);
            var availableWidth = gameInputTextWidth * scaleFactor;
            var textOffsetX = 0f;

            // If text is wider than available space, scroll to show the end (right side)
            if (textSize.Width > availableWidth)
            {
                textOffsetX = availableWidth - textSize.Width;
            }

            var inputTextPos = new Vector2(
                scaledPos.X + padding + (int)(gamePromptWidth * scaleFactor) + textOffsetX,
                inputBarY + (int)(3 * scaleFactor));

            // Set up scissor rectangle to clip text that overflows
            var scissorRect = new Rectangle(
                (int)(scaledPos.X + padding + gamePromptWidth * scaleFactor),
                inputBarY,
                (int)(gameInputTextWidth * scaleFactor),
                (int)(20 * scaleFactor)); // Height of input bar text area

            var graphicsDevice = _graphicsDeviceProvider.GraphicsDevice;
            var previousScissorRectangle = graphicsDevice.ScissorRectangle;

            graphicsDevice.ScissorRectangle = scissorRect;

            _spriteBatch.Begin(rasterizerState: _scissorRasterizerState);
            _spriteBatch.DrawString(scaledFont, text, inputTextPos, _styleProvider.InputText);
            _spriteBatch.End();

            graphicsDevice.ScissorRectangle = previousScissorRectangle;
        }



        private void DrawTabs(Vector2 pos)
        {
            foreach (var pair in _tabs.Where(x => x.Value.Visible))
            {
                var tab = pair.Key;
                var info = pair.Value;
                var tabRect = GetTabRect(tab);

                var absRect = new Rectangle((int)pos.X + tabRect.X, (int)pos.Y + tabRect.Y, tabRect.Width, tabRect.Height);

                // Draw tab background
                var bgColor = info.Active ? _styleProvider.ButtonPressed : info.HasUnread ? new Color(180, 140, 60) : _styleProvider.ButtonNormal;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, absRect, bgColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, absRect, _styleProvider.PanelBorder, 1);

                // Draw tab label
                var labelColor = info.Active ? _styleProvider.TabText : _styleProvider.TextSecondary;
                var textPos = new Vector2(absRect.X + 16, absRect.Y + 2);
                _spriteBatch.DrawString(_labelFont, info.Label, textPos, labelColor);

                // Draw close button for PM tabs
                if ((tab == ChatTab.Private1 || tab == ChatTab.Private2) && info.Active)
                {
                    var closeRect = new Rectangle(absRect.X + 3, absRect.Y + 3, 12, 12);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, closeRect, new Color(150, 50, 50));
                    _spriteBatch.DrawString(_labelFont, "X", new Vector2(closeRect.X + 2, closeRect.Y - 1), _styleProvider.TabText);
                }
            }
        }

        private void DrawTabsScaled(Vector2 pos, float scale)
        {
            foreach (var pair in _tabs.Where(x => x.Value.Visible))
            {
                var tab = pair.Key;
                var info = pair.Value;
                var tabRect = GetTabRect(tab);

                // Scale the tab rectangle
                var absRect = new Rectangle(
                    (int)(pos.X + tabRect.X * scale),
                    (int)(pos.Y + tabRect.Y * scale),
                    (int)(tabRect.Width * scale),
                    (int)(tabRect.Height * scale));

                // Draw tab background
                var bgColor = info.Active ? _styleProvider.ButtonPressed : info.HasUnread ? new Color(180, 140, 60) : _styleProvider.ButtonNormal;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, absRect, bgColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, absRect, _styleProvider.PanelBorder, 1);

                // Draw tab label
                var labelColor = info.Active ? _styleProvider.TabText : _styleProvider.TextSecondary;
                var textPos = new Vector2(absRect.X + (int)(16 * scale), absRect.Y + (int)(2 * scale));
                _spriteBatch.DrawString(_labelFont, info.Label, textPos, labelColor);

                // Draw close button for PM tabs
                if ((tab == ChatTab.Private1 || tab == ChatTab.Private2) && info.Active)
                {
                    var closeSize = (int)(12 * scale);
                    var closeRect = new Rectangle(absRect.X + (int)(3 * scale), absRect.Y + (int)(3 * scale), closeSize, closeSize);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, closeRect, new Color(150, 50, 50));
                    _spriteBatch.DrawString(_labelFont, "X", new Vector2(closeRect.X + (int)(2 * scale), closeRect.Y - (int)(1 * scale)), _styleProvider.TabText);
                }
            }
        }

        private void DrawCollapsedButton(Vector2 scaledPos, float scale)
        {
            var btnSize = (int)(CollapsedButtonSize * scale);
            // When collapsed, DrawArea is already positioned at the right side, so scaledPos is correct
            var btnRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, btnSize, btnSize);

            _spriteBatch.Begin();

            // Button background with border
            DrawingPrimitives.DrawFilledRect(_spriteBatch, btnRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, btnRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Draw chat bubble icon using lines/shapes
            var iconMargin = (int)(6 * scale);
            var iconRect = new Rectangle(btnRect.X + iconMargin, btnRect.Y + iconMargin,
                                          btnSize - iconMargin * 2, btnSize - iconMargin * 2);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, iconRect, _styleProvider.ButtonNormal);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, iconRect, _styleProvider.PanelBorder, 1);

            // Draw "..." dots inside the chat bubble
            var dotSize = Math.Max(2, (int)(2 * scale));
            var dotY = iconRect.Y + (iconRect.Height - dotSize) / 2;
            var dotSpacing = (iconRect.Width - dotSize * 3) / 4;
            for (int i = 0; i < 3; i++)
            {
                var dotX = iconRect.X + dotSpacing * (i + 1) + dotSize * i;
                var dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, dotRect, _styleProvider.TextPrimary);
            }

            // Draw unread badge if there are unread messages
            if (_unreadCount > 0)
            {
                var badgeRadius = (int)(BadgeSize * scale / 2);
                var badgeCenterX = btnRect.Right - badgeRadius + (int)(2 * scale);
                var badgeCenterY = btnRect.Top + badgeRadius - (int)(2 * scale);
                var badgeRect = new Rectangle(badgeCenterX - badgeRadius, badgeCenterY - badgeRadius,
                                               badgeRadius * 2, badgeRadius * 2);

                // Red badge circle (approximated with filled rect — matches code-drawn style)
                DrawingPrimitives.DrawFilledRect(_spriteBatch, badgeRect, new Color(220, 50, 50));
                DrawingPrimitives.DrawRectBorder(_spriteBatch, badgeRect, new Color(180, 30, 30), 1);

                // Badge text
                var badgeText = _unreadCount > 99 ? "99+" : _unreadCount.ToString();
                var textSize = _labelFont.MeasureString(badgeText);
                var textPos = new Vector2(
                    badgeCenterX - textSize.Width / 2,
                    badgeCenterY - textSize.Height / 2);
                _spriteBatch.DrawString(_labelFont, badgeText, textPos, Color.White);
            }

            _spriteBatch.End();
        }

        private void DrawMinimizeButton(Vector2 scaledPos, float scale)
        {
            var btnW = (int)(MinimizeButtonSize * scale);
            var btnH = (int)(MinimizeButtonSize * scale);
            var btnX = (int)(scaledPos.X + 4 * scale);
            var btnY = (int)(scaledPos.Y + 2 * scale);
            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);

            // Check hover state for visual feedback
            var mouseState = Mouse.GetState();
            var isHovered = mouseState.X >= btnX && mouseState.X < btnX + btnW
                         && mouseState.Y >= btnY && mouseState.Y < btnY + btnH;

            _spriteBatch.Begin();

            var bgColor = isHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, btnRect, bgColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, btnRect, _styleProvider.PanelBorder, 1);

            // Draw "—" line in center of button
            var lineY = btnY + btnH / 2;
            var lineMargin = (int)(3 * scale);
            var lineRect = new Rectangle(btnX + lineMargin, lineY, btnW - lineMargin * 2, Math.Max(1, (int)(2 * scale)));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, lineRect, _styleProvider.TextPrimary);

            _spriteBatch.End();
        }

        private void DrawScrollbar(Vector2 pos, float scale)
        {
            var sbX = (int)(pos.X + ScrollBarLeft * scale);
            var sbY = (int)(pos.Y + ScrollBarTop * scale);
            var sbW = (int)(ScrollBarWidth * scale);
            var sbH = (int)(ScrollBarHeight * scale);
            var arrowH = (int)(ScrollArrowHeight * scale);

            var trackColor = _styleProvider.PanelBackground;
            var borderColor = _styleProvider.PanelBorder;
            var btnColor = _styleProvider.ButtonNormal;
            var btnHover = _styleProvider.ButtonHover;

            _spriteBatch.Begin();

            // Track background
            var trackRect = new Rectangle(sbX, sbY, sbW, sbH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, trackRect, trackColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, trackRect, borderColor, 1);

            // Check mouse hover for button highlights
            var mouseState = Mouse.GetState();
            var mouseOverUp = mouseState.X >= sbX && mouseState.X < sbX + sbW
                           && mouseState.Y >= sbY && mouseState.Y < sbY + arrowH;
            var mouseOverDown = mouseState.X >= sbX && mouseState.X < sbX + sbW
                             && mouseState.Y >= sbY + sbH - arrowH && mouseState.Y < sbY + sbH;

            // Up arrow button
            var upRect = new Rectangle(sbX, sbY, sbW, arrowH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, upRect, mouseOverUp ? btnHover : btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, upRect, borderColor, 1);
            var arrowSize = (int)(5 * scale);
            DrawArrow(_spriteBatch, sbX + sbW / 2, sbY + arrowH / 2, arrowSize, true, _styleProvider.TextPrimary);

            // Down arrow button
            var downRect = new Rectangle(sbX, sbY + sbH - arrowH, sbW, arrowH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, downRect, mouseOverDown ? btnHover : btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, downRect, borderColor, 1);
            DrawArrow(_spriteBatch, sbX + sbW / 2, sbY + sbH - arrowH / 2, arrowSize, false, _styleProvider.TextPrimary);

            // Thumb
            if (_totalLines > VisibleLines)
            {
                var thumbHeight = (int)(GetThumbHeight() * scale);
                var thumbYPos = sbY + arrowH + (int)(GetThumbY() * scale);
                var thumbRect = new Rectangle(sbX + (int)(2 * scale), thumbYPos, sbW - (int)(4 * scale), thumbHeight);

                var thumbColor = _isDraggingThumb ? _styleProvider.ButtonPressed : btnHover;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, thumbColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, borderColor, 1);
            }

            _spriteBatch.End();
        }

        private static void DrawArrow(SpriteBatch sb, int cx, int cy, int size, bool up, Color color)
        {
            var dir = up ? -1 : 1;
            for (int row = 0; row < size; row++)
            {
                var width = (row * 2) + 1;
                var x = cx - row;
                var y = cy + dir * (size / 2 - row);
                DrawingPrimitives.DrawFilledRect(sb, new Rectangle(x, y, width, 1), color);
            }
        }

        private int GetThumbHeight()
        {
            if (_totalLines <= VisibleLines)
                return ScrollTrackHeight;
            return Math.Max(20, (int)(ScrollTrackHeight * ((float)VisibleLines / _totalLines)));
        }

        private int GetThumbY()
        {
            if (MaxScrollOffset <= 0) return 0;
            var thumbHeight = GetThumbHeight();
            var usableTrack = ScrollTrackHeight - thumbHeight;
            return (int)(usableTrack * ((float)_scrollOffset / MaxScrollOffset));
        }

        private void ScrollUp(int lines = 1)
        {
            if (_totalLines <= VisibleLines) return;
            _scrollOffset = Math.Max(0, _scrollOffset - lines);
        }

        private void ScrollDown(int lines = 1)
        {
            if (_totalLines <= VisibleLines) return;
            _scrollOffset = Math.Min(MaxScrollOffset, _scrollOffset + lines);
        }

        private void ScrollToEnd()
        {
            _scrollOffset = MaxScrollOffset;
        }

        private Rectangle GetTabRect(ChatTab tab)
        {
            // Tabs positioned below input bar: message area (130 + 8 buffer) + gap (4) + input bar (22) + gap (4) = 168
            var tabY = 4 + VisibleLines * 13 + 8 + 4 + InputBarHeight + 4;
            return tab switch
            {
                ChatTab.Private1 => new Rectangle(23, tabY, 110, 14),
                ChatTab.Private2 => new Rectangle(136, tabY, 110, 14),
                ChatTab.Local => new Rectangle(249, tabY, 50, 14),
                ChatTab.Global => new Rectangle(302, tabY, 50, 14),
                ChatTab.Group => new Rectangle(355, tabY, 50, 14),
                ChatTab.System => new Rectangle(408, tabY, 50, 14),
                _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
            };
        }

        public void TryStartNewPrivateChat(string targetCharacter)
        {
            if (_tabs[ChatTab.Private1].Visible && _tabs[ChatTab.Private2].Visible)
                return;

            if (!string.Equals(_chatProvider.PMTarget1, targetCharacter, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_chatProvider.PMTarget2, targetCharacter, StringComparison.OrdinalIgnoreCase))
            {
                if (_tabs[ChatTab.Private1].Visible)
                {
                    SelectTab(ChatTab.Private2);
                    _tabs[ChatTab.Private2].Label = char.ToUpper(targetCharacter[0]) + targetCharacter[1..];
                }
                else
                {
                    SelectTab(ChatTab.Private1);
                    _tabs[ChatTab.Private1].Label = char.ToUpper(targetCharacter[0]) + targetCharacter[1..];
                }
            }
        }

        public void SelectTab(ChatTab clickedTab)
        {
            if (CurrentTab == ChatTab.Global && clickedTab != ChatTab.Global)
            {
                _chatActions.SetGlobalActive(false);
            }
            else if (CurrentTab != ChatTab.Global && clickedTab == ChatTab.Global)
            {
                _chatActions.SetGlobalActive(true);
            }

            var currentInfo = _tabs[CurrentTab];
            currentInfo.Active = false;
            currentInfo.CachedScrollOffset = _scrollOffset;

            var newInfo = _tabs[clickedTab];
            newInfo.Visible = true;
            newInfo.Active = true;
            newInfo.HasUnread = false;
            _scrollOffset = newInfo.CachedScrollOffset;

            _totalLines = _chatProvider.AllChat[clickedTab].Count;
        }

        public void ClosePMTab(ChatTab whichTab)
        {
            if (whichTab != ChatTab.Private1 && whichTab != ChatTab.Private2)
                throw new InvalidOperationException("Unable to close chat tab that isn't a PM tab");

            SelectTab(ChatTab.Local);

            var info = _tabs[whichTab];
            info.Visible = false;
            info.CachedChat.Clear();
            info.Label = string.Empty;
            info.CachedScrollOffset = 0;

            _chatActions.ClosePMTab(whichTab);
        }

        private class CodeDrawnChatTabInfo
        {
            public string Label { get; set; }
            public bool Active { get; set; }
            public bool Visible { get; set; } = true;
            public bool IsLarge { get; set; }
            public HashSet<ChatData> CachedChat { get; set; } = new HashSet<ChatData>();
            public List<IChatRenderable> Renderables { get; set; } = new List<IChatRenderable>();
            public int CachedScrollOffset { get; set; }
            public bool HasUnread { get; set; }

            public CodeDrawnChatTabInfo(string label, bool active)
            {
                Label = label;
                Active = active;
            }
        }

        private Point TransformMousePosition(Point position)
        {
            var offset = WindowSizeProvider.RenderOffset;
            var scale = WindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Clamp(gameX, 0, WindowSizeProvider.GameWidth - 1),
                Math.Clamp(gameY, 0, WindowSizeProvider.GameHeight - 1));
        }
    }
}
