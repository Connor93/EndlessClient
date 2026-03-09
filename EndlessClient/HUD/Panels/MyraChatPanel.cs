using System;
using System.Collections.Generic;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using EOLib.Domain.Chat;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based chat panel with embedded text input, tab bar at the bottom,
    /// and scrollable message display reading directly from IChatProvider.
    /// Uses IUIStyleProvider for DarkParchment theme consistency.
    /// </summary>
    public class MyraChatPanel : MyraHudPanelBase, IChatPanel
    {
        private readonly IChatActions _chatActions;
        private readonly IChatProvider _chatProvider;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IUIStyleProvider _style;

        private readonly Dictionary<ChatTab, TabState> _tabStates;
        private readonly Dictionary<ChatTab, Label> _tabLabels;
        private VerticalStackPanel _messagePanel;
        private ScrollViewer _scrollViewer;
        private TextBox _inputTextBox;
        private ChatTab _currentTab;
        private bool _wasEnterDown;

        public ChatTab CurrentTab => _currentTab;

        public event EventHandler OnEnterPressed;

        public string InputText
        {
            get => _inputTextBox?.Text ?? "";
            set { if (_inputTextBox != null) _inputTextBox.Text = value; }
        }

        public bool InputSelected
        {
            get => _inputTextBox?.IsKeyboardFocused ?? false;
            set { if (_inputTextBox != null && value) _inputTextBox.SetKeyboardFocus(); }
        }

        private class TabState
        {
            public int LastSeenCount;
            public bool HasUnread;
            public bool IsVisible;
        }

        public MyraChatPanel(Game game,
                             IMyraUIManager myraUIManager,
                             IMyraFontProvider fontProvider,
                             IChatActions chatActions,
                             IChatProvider chatProvider,
                             IUIStyleProvider styleProvider)
            : base(game, myraUIManager, "")
        {
            _fontProvider = fontProvider;
            _chatActions = chatActions;
            _chatProvider = chatProvider;
            _style = styleProvider;

            _tabStates = new Dictionary<ChatTab, TabState>
            {
                { ChatTab.Local, new TabState { IsVisible = true } },
                { ChatTab.Global, new TabState { IsVisible = true } },
                { ChatTab.Group, new TabState { IsVisible = true } },
                { ChatTab.System, new TabState { IsVisible = true } },
                { ChatTab.Private1, new TabState { IsVisible = false } },
                { ChatTab.Private2, new TabState { IsVisible = false } },
            };

            _tabLabels = new Dictionary<ChatTab, Label>();
            _currentTab = ChatTab.Local;
        }

        public override void Initialize()
        {
            Window.Width = 480;
            Window.Height = 180;

            // Hide the title bar
            if (Window.TitlePanel != null)
            {
                Window.TitlePanel.Visible = false;
                Window.TitlePanel.Height = 0;
            }

            // Theme-consistent window styling
            Window.Background = new SolidBrush(_style.PanelBackground);
            Window.Border = new SolidBrush(_style.PanelBorder);
            Window.BorderThickness = new Thickness(_style.BorderThickness);
            Window.Padding = new Thickness(0);

            // Root layout: messages | separator | input | tab bar
            var root = new Grid();
            root.RowsProportions.Add(new Proportion(ProportionType.Fill));       // messages
            root.RowsProportions.Add(new Proportion(ProportionType.Pixels, 1));  // separator
            root.RowsProportions.Add(new Proportion(ProportionType.Pixels, 26)); // input row
            root.RowsProportions.Add(new Proportion(ProportionType.Pixels, 24)); // tab bar

            // === Message area ===
            _messagePanel = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(6, 4),
            };
            _scrollViewer = new ScrollViewer
            {
                Content = _messagePanel,
                ShowHorizontalScrollBar = false,
                ShowVerticalScrollBar = true,
            };
            Grid.SetRow(_scrollViewer, 0);
            root.Widgets.Add(_scrollViewer);

            // === Separator line ===
            var separator = new Panel
            {
                Background = new SolidBrush(_style.PanelBorder),
                Height = 1,
            };
            Grid.SetRow(separator, 1);
            root.Widgets.Add(separator);

            // === Input row ===
            var inputRow = new Grid
            {
                Background = new SolidBrush(_style.InputBackground),
            };
            inputRow.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 3));  // accent bar
            inputRow.ColumnsProportions.Add(new Proportion(ProportionType.Fill));        // textbox

            // Accent bar on the left edge of input
            var accentBar = new Panel
            {
                Background = new SolidBrush(_style.TextHighlight),
                Width = 3,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetColumn(accentBar, 0);
            inputRow.Widgets.Add(accentBar);

            _inputTextBox = new TextBox
            {
                Font = _fontProvider.Normal,
                TextColor = _style.InputText,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Background = null,
                FocusedBackground = null,
                Border = null,
                FocusedBorder = null,
                Padding = new Thickness(6, 3),
            };
            _inputTextBox.KeyDown += (_, e) =>
            {
                if (e.Data == Microsoft.Xna.Framework.Input.Keys.Enter)
                {
                    OnEnterPressed?.Invoke(this, EventArgs.Empty);
                }
            };
            Grid.SetColumn(_inputTextBox, 1);
            inputRow.Widgets.Add(_inputTextBox);

            Grid.SetRow(inputRow, 2);
            root.Widgets.Add(inputRow);

            // === Tab bar (bottom, right-aligned) ===
            var tabBarContainer = new Grid
            {
                Background = new SolidBrush(_style.TitleBarBackground),
            };
            tabBarContainer.ColumnsProportions.Add(new Proportion(ProportionType.Fill)); // spacer
            tabBarContainer.ColumnsProportions.Add(new Proportion(ProportionType.Auto)); // tabs

            var tabBar = new HorizontalStackPanel
            {
                Spacing = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 0, 10, 0),
            };
            AddTab(tabBar, ChatTab.Local, "scr");
            AddTab(tabBar, ChatTab.Global, "glb");
            AddTab(tabBar, ChatTab.Group, "grp");
            AddTab(tabBar, ChatTab.System, "sys");
            AddTab(tabBar, ChatTab.Private1, "");
            AddTab(tabBar, ChatTab.Private2, "");

            Grid.SetColumn(tabBar, 1);
            tabBarContainer.Widgets.Add(tabBar);

            Grid.SetRow(tabBarContainer, 3);
            root.Widgets.Add(tabBarContainer);

            Window.Content = root;

            // Hide PM tabs initially
            _tabLabels[ChatTab.Private1].Visible = false;
            _tabLabels[ChatTab.Private2].Visible = false;

            UpdateTabStyles();
            base.Initialize();
        }

        private void AddTab(HorizontalStackPanel tabBar, ChatTab tab, string label)
        {
            var tabLabel = new Label
            {
                Text = label,
                Font = _fontProvider.Normal,
                TextColor = _style.TabText,
                VerticalAlignment = VerticalAlignment.Center,
            };
            tabLabel.TouchDown += (_, _) => SelectTab(tab);
            tabLabel.MouseEntered += (_, _) =>
            {
                if (tab != _currentTab)
                    tabLabel.TextColor = _style.LinkHoverColor;
            };
            tabLabel.MouseLeft += (_, _) => UpdateSingleTabStyle(tab);
            _tabLabels[tab] = tabLabel;
            tabBar.Widgets.Add(tabLabel);
        }

        public void TryStartNewPrivateChat(string targetCharacter)
        {
            if (_tabStates[ChatTab.Private1].IsVisible && _tabStates[ChatTab.Private2].IsVisible)
                return;

            if (!string.Equals(_chatProvider.PMTarget1, targetCharacter, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_chatProvider.PMTarget2, targetCharacter, StringComparison.OrdinalIgnoreCase))
            {
                var formattedName = char.ToUpper(targetCharacter[0]) + targetCharacter[1..];

                if (_tabStates[ChatTab.Private1].IsVisible)
                {
                    ShowPMTab(ChatTab.Private2, formattedName);
                    SelectTab(ChatTab.Private2);
                }
                else
                {
                    ShowPMTab(ChatTab.Private1, formattedName);
                    SelectTab(ChatTab.Private1);
                }
            }
        }

        private void ShowPMTab(ChatTab tab, string name)
        {
            _tabStates[tab].IsVisible = true;
            _tabLabels[tab].Text = name;
            _tabLabels[tab].Visible = true;
        }

        public void SelectTab(ChatTab clickedTab)
        {
            if (_currentTab == ChatTab.Global && clickedTab != ChatTab.Global)
                _chatActions.SetGlobalActive(false);
            else if (_currentTab != ChatTab.Global && clickedTab == ChatTab.Global)
                _chatActions.SetGlobalActive(true);

            _currentTab = clickedTab;
            _tabStates[clickedTab].HasUnread = false;

            UpdateTabStyles();
            RefreshMessages();
        }

        public void ClosePMTab(ChatTab whichTab)
        {
            if (whichTab != ChatTab.Private1 && whichTab != ChatTab.Private2)
                return;

            _tabStates[whichTab].IsVisible = false;
            _tabLabels[whichTab].Visible = false;

            if (_currentTab == whichTab)
                SelectTab(ChatTab.Local);
        }

        public override void Update(GameTime gameTime)
        {
            // Auto-focus chat input when Enter is pressed and no other input has focus
            var keyState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            if (keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) && !_wasEnterDown)
            {
                var focusedWidget = UIManager.Desktop.FocusedKeyboardWidget;
                if (focusedWidget == null || focusedWidget == _inputTextBox)
                {
                    _inputTextBox.SetKeyboardFocus();
                }
            }
            _wasEnterDown = keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter);

            foreach (var tab in _tabStates.Keys)
            {
                if (!_tabStates[tab].IsVisible)
                    continue;

                var messages = _chatProvider.AllChat[tab];
                var currentCount = messages.Count;

                if (currentCount != _tabStates[tab].LastSeenCount)
                {
                    if (tab == _currentTab)
                    {
                        _tabStates[tab].LastSeenCount = currentCount;
                        RefreshMessages();
                    }
                    else
                    {
                        _tabStates[tab].HasUnread = true;
                        _tabStates[tab].LastSeenCount = currentCount;
                        UpdateTabStyles();
                    }
                }
            }

            base.Update(gameTime);
        }

        private void RefreshMessages()
        {
            _messagePanel.Widgets.Clear();

            var messages = _chatProvider.AllChat[_currentTab];
            foreach (var msg in messages)
            {
                var text = string.IsNullOrEmpty(msg.Who)
                    ? msg.Message
                    : $"{msg.Who}: {msg.Message}";

                var label = new Label
                {
                    Text = text,
                    Font = _fontProvider.Normal,
                    TextColor = GetChatColor(msg.ChatColor),
                    Wrap = true,
                };

                _messagePanel.Widgets.Add(label);
            }

            // Scroll to bottom
            _scrollViewer.ScrollPosition = new Point(0, int.MaxValue / 2);
        }

        private void UpdateTabStyles()
        {
            foreach (var pair in _tabLabels)
            {
                if (!_tabStates[pair.Key].IsVisible)
                    continue;
                UpdateSingleTabStyle(pair.Key);
            }
        }

        private void UpdateSingleTabStyle(ChatTab tab)
        {
            if (!_tabLabels.TryGetValue(tab, out var label))
                return;

            if (tab == _currentTab)
                label.TextColor = _style.TextHighlight; // active tab = highlight color
            else if (_tabStates[tab].HasUnread)
                label.TextColor = _style.GoldColor;     // unread = gold
            else
                label.TextColor = _style.TextSecondary; // inactive = secondary text
        }

        private Color GetChatColor(ChatColor chatColor)
        {
            return chatColor switch
            {
                EOLib.Domain.Chat.ChatColor.Server => _style.ChatServer,
                EOLib.Domain.Chat.ChatColor.Error => _style.ChatError,
                EOLib.Domain.Chat.ChatColor.PM => _style.ChatPM,
                EOLib.Domain.Chat.ChatColor.ServerGlobal => _style.ChatServerGlobal,
                EOLib.Domain.Chat.ChatColor.Admin => _style.ChatAdmin,
                _ => _style.ChatDefault,
            };
        }
    }
}
