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
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnChatPanel : DraggableHudPanel
    {
        private readonly IChatActions _chatActions;
        private readonly IChatRenderableGenerator _chatRenderableGenerator;
        private readonly IChatProvider _chatProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _chatFont;
        private readonly BitmapFont _labelFont;

        private readonly ScrollBar _scrollBar;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly Dictionary<ChatTab, CodeDrawnChatTabInfo> _tabs;

        private const int PanelWidth = 489;
        private const int PanelHeight = 182; // 10 lines (130) + gaps + input bar (22) + tabs (14) + padding
        private const int VisibleLines = 10;
        private const int InputBarHeight = 22;

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
            : base(clientWindowSizeProvider.Resizable)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _chatActions = chatActions;
            _chatRenderableGenerator = chatRenderableGenerator;
            _chatProvider = chatProvider;
            _hudControlProvider = hudControlProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _chatFont = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            DrawArea = new Rectangle(102, 280, PanelWidth, PanelHeight); // Y position adjusted for taller panel

            // Message area height: roughly 10 lines * 13px = 130px
            _scrollBar = new ScrollBar(new Vector2(467, 2), new Vector2(16, 127), ScrollBarColors.LightOnMed, _nativeGraphicsManager)
            {
                LinesToRender = VisibleLines,
                Visible = true
            };
            _scrollBar.SetParentControl(this);
            SetScrollWheelHandler(_scrollBar);

            // Create integrated text input box with WASD filtering
            // Position: below message area (130px) + gap (4) = 138, relative to panel
            var inputY = 4 + VisibleLines * 13 + 4 + 3; // message area top + height + gap + padding inside input bar
            _inputTextBox = new ChatInputTextBox(configurationProvider, Rectangle.Empty, Constants.FontSize08, caretTexture: contentProvider.Textures[ContentProvider.Cursor])
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
            _scrollBar.Initialize();
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

                    if (info.Active)
                    {
                        _scrollBar.UpdateDimensions(info.Renderables.Count);
                        _scrollBar.ScrollToEnd();
                    }
                    else
                    {
                        info.CachedScrollOffset = Math.Max(0, info.Renderables.Count - VisibleLines);
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
            if (eventArgs.Button == MouseButton.Right)
            {
                HandleRightClick(eventArgs);
            }

            return base.HandleMouseDown(control, eventArgs);
        }

        private void HandleRightClick(MouseEventArgs eventArgs)
        {
            var clickedYRelativeToTopOfPanel = eventArgs.Position.Y - DrawAreaWithParentOffset.Y;
            var clickedChatRow = (int)Math.Round(clickedYRelativeToTopOfPanel / 13.0) - 1;
            var currentTabInfo = _tabs[CurrentTab];

            if (clickedChatRow >= 0 && _scrollBar.ScrollOffset + clickedChatRow < currentTabInfo.CachedChat.Count)
            {
                var who = _chatProvider.AllChat[CurrentTab][_scrollBar.ScrollOffset + clickedChatRow].Who;
                if (!string.IsNullOrEmpty(who))
                {
                    // Use integrated text input
                    _inputTextBox.Text = $"!{who} ";
                }
            }
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw message area (10 lines * 13px = 130px height)
            var messageAreaHeight = VisibleLines * 13;
            var messageAreaRect = new Rectangle((int)pos.X + 4, (int)pos.Y + 4, PanelWidth - 30, messageAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, messageAreaRect, new Color(120, 110, 100, 255));

            // Draw input bar area
            var inputBarY = (int)pos.Y + 4 + messageAreaHeight + 4;
            var inputBarRect = new Rectangle((int)pos.X + 4, inputBarY, PanelWidth - 12, InputBarHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, inputBarRect, new Color(100, 90, 80, 255));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, inputBarRect, _styleProvider.PanelBorder, 1);

            // Draw ">" prompt
            _spriteBatch.DrawString(_labelFont, ">", new Vector2(inputBarRect.X + 4, inputBarRect.Y + 3), Color.White);

            // Draw tabs
            DrawTabs(pos);

            _spriteBatch.End();

            // Draw chat messages for active tab
            var activeTabInfo = _tabs[CurrentTab];
            foreach (var (ndx, renderable) in activeTabInfo.Renderables.Skip(_scrollBar.ScrollOffset).Take(_scrollBar.LinesToRender).Select((r, i) => (i, r)))
            {
                renderable.DisplayIndex = ndx;
                renderable.Render(this, _spriteBatch, _chatFont);
            }

            base.OnDrawControl(gameTime);
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
                var bgColor = info.Active ? _styleProvider.ButtonPressed : _styleProvider.ButtonNormal;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, absRect, bgColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, absRect, _styleProvider.PanelBorder, 1);

                // Draw tab label
                var labelColor = info.Active ? Color.White : _styleProvider.TextSecondary;
                var textPos = new Vector2(absRect.X + 16, absRect.Y + 2);
                _spriteBatch.DrawString(_labelFont, info.Label, textPos, labelColor);

                // Draw close button for PM tabs
                if ((tab == ChatTab.Private1 || tab == ChatTab.Private2) && info.Active)
                {
                    var closeRect = new Rectangle(absRect.X + 3, absRect.Y + 3, 12, 12);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, closeRect, new Color(150, 50, 50));
                    _spriteBatch.DrawString(_labelFont, "X", new Vector2(closeRect.X + 2, closeRect.Y - 1), Color.White);
                }
            }
        }

        private Rectangle GetTabRect(ChatTab tab)
        {
            // Tabs positioned below input bar: message area (130) + gap (4) + input bar (22) + gap (4) = 160
            var tabY = 4 + VisibleLines * 13 + 4 + InputBarHeight + 4;
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
            currentInfo.CachedScrollOffset = _scrollBar.ScrollOffset;

            var newInfo = _tabs[clickedTab];
            newInfo.Visible = true;
            newInfo.Active = true;
            _scrollBar.SetScrollOffset(newInfo.CachedScrollOffset);

            _scrollBar.UpdateDimensions(_chatProvider.AllChat[clickedTab].Count);
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

            public CodeDrawnChatTabInfo(string label, bool active)
            {
                Label = label;
                Active = active;
            }
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
    }
}
