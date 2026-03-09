using System;
using System.Collections.Generic;
using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based scrolling list dialog — replaces CodeDrawnScrollingListDialog.
    /// Provides a titled window with a scrollable item list and configurable buttons.
    /// Subclasses add items via AddItem() and handle events via BackAction/OkAction/CancelAction.
    /// </summary>
    public class MyraScrollingListDialog : MyraDialogAdapter
    {
        private readonly IMyraFontProvider _fontProvider;
        private readonly VerticalStackPanel _listPanel;
        private readonly VerticalStackPanel _mainPanel;
        private readonly ScrollViewer _scrollViewer;
        private readonly HorizontalStackPanel _buttonBar;
        private readonly List<MyraListItem> _items = new();

        private Button _okButton;
        private Button _cancelButton;
        private Button _backButton;
        private Button _addButton;
        private Button _deleteButton;
        private Button _historyButton;
        private Button _progressButton;

        public event EventHandler BackAction;
        public event EventHandler OkAction;
        public event EventHandler CancelAction;
        public event EventHandler AddAction;
        public event EventHandler DeleteAction;
        public event EventHandler HistoryAction;
        public event EventHandler ProgressAction;

        /// <summary>
        /// When true (default), clicking Cancel will close the dialog.
        /// </summary>
        public bool CancelClosesDialog { get; set; } = true;

        /// <summary>
        /// When true (default), clicking OK will close the dialog after invoking OkAction.
        /// </summary>
        public bool OkClosesDialog { get; set; } = true;

        /// <summary>
        /// Gets or sets the dialog title.
        /// </summary>
        public string Title
        {
            get => Window.Title;
            set => Window.Title = value;
        }

        /// <summary>
        /// Exposes scroll position for derived classes.
        /// </summary>
        protected int ScrollOffset => _scrollViewer?.ScrollPosition.Y ?? 0;

        public IReadOnlyList<string> NamesList
        {
            get
            {
                var names = new List<string>();
                foreach (var item in _items)
                    names.Add(item.PrimaryText);
                return names;
            }
        }

        public MyraScrollingListDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            string title,
            int width = 320,
            int height = 380,
            bool showAdd = false)
            : base(uiManager, title)
        {
            _fontProvider = fontProvider;

            Window.Width = width;
            Window.Height = height;
            Window.TitleFont = fontProvider.Header;

            // Main layout: vertical stack with list area + button bar
            _mainPanel = new VerticalStackPanel
            {
                Spacing = 4
            };

            // Scrollable list area
            _listPanel = new VerticalStackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Top
            };

            _scrollViewer = new ScrollViewer
            {
                Content = _listPanel,
                ShowHorizontalScrollBar = false,
                ShowVerticalScrollBar = true,
            };

            // Button bar at the bottom
            _buttonBar = new HorizontalStackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            _mainPanel.Widgets.Add(_scrollViewer);
            _mainPanel.Widgets.Add(_buttonBar);

            // Make scroll viewer fill available space
            _mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));
            _mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = _mainPanel;

            SetupButtons(showOk: false, showCancel: true, showAdd: showAdd);
        }

        /// <summary>
        /// Configure which buttons are visible at the bottom of the dialog.
        /// </summary>
        public void SetupButtons(bool showOk = true, bool showCancel = true, bool showBack = false, bool showAdd = false, bool showDelete = false, bool showHistory = false, bool showProgress = false)
        {
            _buttonBar.Widgets.Clear();
            _okButton = null;
            _cancelButton = null;
            _backButton = null;
            _addButton = null;
            _deleteButton = null;
            _historyButton = null;
            _progressButton = null;

            var buttonFont = _fontProvider.Normal;

            if (showBack)
            {
                _backButton = new Button
                {
                    Content = new Label { Text = "Back", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _backButton.Click += (_, _) => BackAction?.Invoke(this, EventArgs.Empty);
                _buttonBar.Widgets.Add(_backButton);
            }

            if (showOk)
            {
                _okButton = new Button
                {
                    Content = new Label { Text = "OK", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _okButton.Click += (_, _) =>
                {
                    OkAction?.Invoke(this, EventArgs.Empty);
                    if (OkClosesDialog)
                        Close(XNADialogResult.OK);
                };
                _buttonBar.Widgets.Add(_okButton);
            }

            if (showCancel)
            {
                _cancelButton = new Button
                {
                    Content = new Label { Text = "Cancel", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _cancelButton.Click += (_, _) =>
                {
                    var shouldClose = CancelClosesDialog;
                    CancelAction?.Invoke(this, EventArgs.Empty);
                    if (shouldClose)
                        Close(XNADialogResult.Cancel);
                };
                _buttonBar.Widgets.Add(_cancelButton);
            }

            if (showAdd)
            {
                _addButton = new Button
                {
                    Content = new Label { Text = "Add", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _addButton.Click += (_, _) => AddAction?.Invoke(this, EventArgs.Empty);
                _buttonBar.Widgets.Add(_addButton);
            }

            if (showDelete)
            {
                _deleteButton = new Button
                {
                    Content = new Label { Text = "Delete", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _deleteButton.Click += (_, _) => DeleteAction?.Invoke(this, EventArgs.Empty);
                _buttonBar.Widgets.Add(_deleteButton);
            }

            if (showHistory)
            {
                _historyButton = new Button
                {
                    Content = new Label { Text = "History", Font = buttonFont },
                    Width = 72,
                    Height = 28
                };
                _historyButton.Click += (_, _) => HistoryAction?.Invoke(this, EventArgs.Empty);
                _buttonBar.Widgets.Add(_historyButton);
            }

            if (showProgress)
            {
                _progressButton = new Button
                {
                    Content = new Label { Text = "Progress", Font = buttonFont },
                    Width = 80,
                    Height = 28
                };
                _progressButton.Click += (_, _) => ProgressAction?.Invoke(this, EventArgs.Empty);
                _buttonBar.Widgets.Add(_progressButton);
            }
        }

        /// <summary>
        /// Update the dialog title text.
        /// </summary>
        public void SetTitle(string title)
        {
            Window.Title = title;
        }

        /// <summary>
        /// Show or hide the Add button without rebuilding all buttons.
        /// </summary>
        public void ShowAddButton(bool visible)
        {
            if (_addButton != null)
                _addButton.Visible = visible;
        }

        /// <summary>
        /// Show or hide the scrolling list panel.
        /// </summary>
        public void ShowListPanel(bool visible)
        {
            _scrollViewer.Visible = visible;
        }

        /// <summary>
        /// Provides access to the main content panel for subclasses
        /// that need to add custom widgets (e.g., text editors).
        /// </summary>
        protected VerticalStackPanel ContentPanel => _mainPanel;

        /// <summary>
        /// Add an item row to the scrolling list.
        /// </summary>
        public MyraListItem AddItem(
            string primaryText,
            string subText = "",
            object data = null,
            Action<MyraListItem> onClick = null,
            Action<MyraListItem> onRightClick = null,
            bool isLink = false,
            Texture2D icon = null)
        {
            var item = new MyraListItem(_fontProvider, primaryText, subText, data, isLink, icon);

            if (onClick != null)
            {
                item.TouchDown += (_, _) => onClick(item);
            }

            if (onRightClick != null)
            {
                item.RightClick += (_, _) => onRightClick(item);
            }

            _items.Add(item);
            _listPanel.Widgets.Add(item.Widget);

            return item;
        }

        /// <summary>
        /// Clear all items from the list.
        /// </summary>
        public void ClearItems()
        {
            _items.Clear();
            _listPanel.Widgets.Clear();
        }

        /// <summary>
        /// Remove a specific item from the list.
        /// </summary>
        public void RemoveItem(MyraListItem item)
        {
            _items.Remove(item);
            _listPanel.Widgets.Remove(item.Widget);
        }

        /// <summary>
        /// Update the sub-text of an item at a specific index.
        /// </summary>
        public void UpdateItemSubText(int index, string subText)
        {
            if (index >= 0 && index < _items.Count)
                _items[index].SubText = subText;
        }

        /// <summary>
        /// Highlight items whose PrimaryText matches any name in the list (case-insensitive).
        /// Highlighted items get a different text color.
        /// </summary>
        public void HighlightItemsByName(IEnumerable<string> names)
        {
            var nameSet = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items)
            {
                item.SetHighlighted(nameSet.Contains(item.PrimaryText));
            }
        }

        /// <summary>
        /// Clear all name highlights.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (var item in _items)
                item.SetHighlighted(false);
        }

        /// <summary>
        /// Add a horizontal separator line to the list.
        /// </summary>
        public void AddSeparator()
        {
            var separator = new HorizontalSeparator
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 4)
            };
            _listPanel.Widgets.Add(separator);
        }

        /// <summary>
        /// Close the dialog with Cancel result.
        /// </summary>
        public new void Close()
        {
            Close(XNADialogResult.Cancel);
        }
    }

    /// <summary>
    /// Represents a single row in a MyraScrollingListDialog.
    /// </summary>
    public class MyraListItem
    {
        private string _primaryText;
        public string PrimaryText
        {
            get => _primaryText;
            set
            {
                _primaryText = value;
                _primaryLabel.Text = value;
            }
        }
        private string _subText;
        public string SubText
        {
            get => _subText;
            set
            {
                _subText = value;
                if (_subLabel != null)
                    _subLabel.Text = value;
            }
        }
        public object Data { get; }
        public bool IsLink { get; }

        private readonly Label _primaryLabel;
        private Label _subLabel;
        private readonly Color _defaultTextColor;

        /// <summary>
        /// The Myra widget representing this row. Add to VerticalStackPanel.
        /// </summary>
        public Widget Widget { get; }

        /// <summary>
        /// Raised when the item is clicked.
        /// </summary>
        public event EventHandler<EventArgs> TouchDown;

        /// <summary>
        /// Raised when the item is right-clicked.
        /// </summary>
        public event EventHandler<EventArgs> RightClick;

        public MyraListItem(
            IMyraFontProvider fontProvider,
            string primaryText,
            string subText,
            object data,
            bool isLink,
            Texture2D icon)
        {
            _primaryText = primaryText;
            _subText = subText;
            Data = data;
            IsLink = isLink;

            var font = fontProvider.Normal;
            var linkColor = new Color(180, 200, 255);
            var linkHoverColor = new Color(220, 230, 255);
            var hoverBg = new Color(60, 60, 80, 120);

            // Build the row widget
            var row = new HorizontalStackPanel
            {
                Spacing = 6,
                Padding = new Thickness(4, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var primaryLabel = new Label
            {
                Text = primaryText,
                Font = font,
                VerticalAlignment = VerticalAlignment.Center,
                Wrap = true
            };

            _primaryLabel = primaryLabel;
            _defaultTextColor = isLink ? linkColor : primaryLabel.TextColor;

            if (isLink)
            {
                primaryLabel.TextColor = linkColor;

                // Hover effects for links
                row.MouseEntered += (_, _) =>
                {
                    row.Background = new SolidBrush(hoverBg);
                    primaryLabel.TextColor = linkHoverColor;
                };
                row.MouseLeft += (_, _) =>
                {
                    row.Background = null;
                    primaryLabel.TextColor = linkColor;
                };
            }

            row.Widgets.Add(primaryLabel);

            // Sub text (right side)
            if (!string.IsNullOrEmpty(subText))
            {
                // Spacer to push sub text to right
                var spacer = new Panel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var subLabel = new Label
                {
                    Text = subText,
                    Font = font,
                    TextColor = new Color(160, 160, 180),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                _subLabel = subLabel;
                row.Widgets.Add(spacer);
                row.Widgets.Add(subLabel);

                // Make spacer fill (proportion index 1 = after primary label)
                while (row.Proportions.Count <= 1)
                    row.Proportions.Add(new Proportion(ProportionType.Auto));
                row.Proportions[1] = new Proportion(ProportionType.Fill);
            }

            // Click handling
            row.TouchDown += (sender, args) =>
            {
                var mouseState = Mouse.GetState();
                if (mouseState.RightButton == ButtonState.Pressed)
                    RightClick?.Invoke(this, EventArgs.Empty);
                else
                    TouchDown?.Invoke(this, EventArgs.Empty);
            };

            Widget = row;
        }

        /// <summary>
        /// Set whether this item should be highlighted (e.g. online friends).
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            _primaryLabel.TextColor = highlighted
                ? new Color(100, 255, 100)
                : _defaultTextColor;
        }
    }
}
