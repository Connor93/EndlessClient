using System;
using System.Collections.Generic;
using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based grid dialog — shared base for GridLocker and Shop dialogs.
    /// Provides a titled window with category tabs, a scrollable grid of item tiles,
    /// and a configurable button bar.
    /// Subclasses override to provide data, handle clicks, and customize tabs.
    /// </summary>
    public abstract class MyraGridDialog : MyraDialogAdapter
    {
        protected readonly IMyraFontProvider FontProvider;
        private readonly IMyraUIManager _uiManager;
        private readonly Grid _tabGrid;
        private readonly VerticalStackPanel _gridContainer;
        private readonly ScrollViewer _scrollViewer;
        private readonly HorizontalStackPanel _buttonBar;

        private int _activeTabIndex;
        private readonly List<Button> _tabButtons = new();

        // Floating tooltip that follows the mouse cursor
        private readonly Panel _tooltipPanel;
        private readonly Label _tooltipLabel;
        private GridTileData _hoveredTileData;

        protected const int GridColumns = 5;
        protected const int TileSize = 64;

        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex == value) return;
                _activeTabIndex = value;
                UpdateTabStyles();
                RefreshGrid();
            }
        }

        protected MyraGridDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            string title,
            int width = 380,
            int height = 420)
            : base(uiManager, title)
        {
            FontProvider = fontProvider;
            _uiManager = uiManager;

            Window.Width = width;
            Window.Height = height;
            Window.TitleFont = fontProvider.Header;

            // --- Tab bar (Grid for equal-width columns) ---
            _tabGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 28,
            };

            // --- Grid content area ---
            _gridContainer = new VerticalStackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(4)
            };

            _scrollViewer = new ScrollViewer
            {
                Content = _gridContainer,
                ShowHorizontalScrollBar = false,
                ShowVerticalScrollBar = true
            };

            // --- Button bar ---
            _buttonBar = new HorizontalStackPanel
            {
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 36
            };

            // --- Floating tooltip (added to Desktop for correct absolute positioning) ---
            _tooltipLabel = new Label
            {
                Font = fontProvider.Normal,
                TextColor = new Color(220, 220, 230),
                Wrap = true,
                Padding = new Thickness(6, 4)
            };
            _tooltipPanel = new Panel
            {
                Background = new SolidBrush(new Color(20, 20, 30, 230)),
                Border = new SolidBrush(new Color(100, 100, 130)),
                BorderThickness = new Thickness(1),
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ZIndex = 10000, // on top of everything
            };
            _tooltipPanel.Widgets.Add(_tooltipLabel);

            // --- Main layout (no tooltip inside — it lives on the Desktop) ---
            var mainLayout = new VerticalStackPanel { Spacing = 4 };
            mainLayout.Widgets.Add(_tabGrid);
            mainLayout.Widgets.Add(_scrollViewer);
            mainLayout.Widgets.Add(_buttonBar);
            mainLayout.Proportions.Add(new Proportion(ProportionType.Auto));   // tabs
            mainLayout.Proportions.Add(new Proportion(ProportionType.Fill));   // grid
            mainLayout.Proportions.Add(new Proportion(ProportionType.Auto));   // buttons

            Window.Content = mainLayout;

            // Poll for data changes + position tooltip
            Window.BeforeRender += _ =>
            {
                PollData();
                UpdateTooltipPosition();
            };

            // Clean up tooltip from Desktop when this dialog closes
            DialogClosed += (_, _) =>
            {
                _tooltipPanel.Visible = false;
                _uiManager.Desktop.Widgets.Remove(_tooltipPanel);
            };
        }

        /// <summary>
        /// Add a tab button to the tab bar.
        /// </summary>
        protected void AddTab(string label)
        {
            var index = _tabButtons.Count;

            // Add a new column definition for this tab
            _tabGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));

            var btn = new Button
            {
                Content = new Label { Text = label, Font = FontProvider.Normal, HorizontalAlignment = HorizontalAlignment.Center },
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                GridColumn = index,
            };
            btn.Click += (_, _) => ActiveTabIndex = index;

            _tabButtons.Add(btn);
            _tabGrid.Widgets.Add(btn);

            UpdateTabStyles();
        }

        /// <summary>
        /// Add a button to the bottom button bar.
        /// </summary>
        protected Button AddButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Content = new Label { Text = text, Font = FontProvider.Normal },
                Width = 72,
                Height = 28
            };
            btn.Click += (_, _) => onClick();
            _buttonBar.Widgets.Add(btn);
            return btn;
        }

        /// <summary>
        /// Override to poll for data changes. Called every frame.
        /// </summary>
        protected abstract void PollData();

        /// <summary>
        /// Override to get the items to display in the current tab.
        /// </summary>
        protected abstract IReadOnlyList<GridTileData> GetTileData();

        /// <summary>
        /// Override to handle tile click.
        /// </summary>
        protected abstract void OnTileClicked(GridTileData tileData);

        /// <summary>
        /// Rebuild the grid with current data.
        /// </summary>
        protected void RefreshGrid()
        {
            _gridContainer.Widgets.Clear();

            var tiles = GetTileData();
            if (tiles == null || tiles.Count == 0) return;

            HorizontalStackPanel currentRow = null;
            var colIndex = 0;

            for (int i = 0; i < tiles.Count; i++)
            {
                if (colIndex == 0)
                {
                    currentRow = new HorizontalStackPanel
                    {
                        Spacing = 4,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    _gridContainer.Widgets.Add(currentRow);
                }

                var tile = CreateTile(tiles[i]);
                currentRow.Widgets.Add(tile);
                colIndex++;

                if (colIndex >= GridColumns)
                    colIndex = 0;
            }
        }

        private Widget CreateTile(GridTileData data)
        {
            var tilePanel = new VerticalStackPanel
            {
                Width = TileSize,
                Height = TileSize + 16,
                Spacing = 2,
                Background = new SolidBrush(new Color(40, 40, 55, 140)),
                Border = new SolidBrush(new Color(70, 70, 90)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2)
            };

            // Item icon
            if (data.IconTexture != null)
            {
                var img = new Image
                {
                    Renderable = new TextureRegion(data.IconTexture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 40,
                    MaxHeight = 40,
                };
                tilePanel.Widgets.Add(img);
                tilePanel.Proportions.Add(new Proportion(ProportionType.Fill));
            }
            else
            {
                // Spacer for missing icons
                tilePanel.Widgets.Add(new Panel { Height = 40 });
                tilePanel.Proportions.Add(new Proportion(ProportionType.Fill));
            }

            // Item name (truncated)
            var nameLabel = new Label
            {
                Text = TruncateName(data.Name, 9),
                Font = FontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            tilePanel.Widgets.Add(nameLabel);
            tilePanel.Proportions.Add(new Proportion(ProportionType.Auto));

            // Quantity badge
            if (data.Amount > 1)
            {
                var qtyLabel = new Label
                {
                    Text = $"x{data.Amount}",
                    Font = FontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextColor = new Color(200, 200, 200)
                };
                tilePanel.Widgets.Add(qtyLabel);
            }

            // Hover: highlight tile + show floating tooltip
            tilePanel.MouseEntered += (_, _) =>
            {
                tilePanel.Background = new SolidBrush(new Color(60, 60, 80, 200));
                _hoveredTileData = data;

                var tooltipText = !string.IsNullOrEmpty(data.TooltipText)
                    ? data.TooltipText
                    : data.Name + (data.Amount > 1 ? $" (x{data.Amount})" : "");
                _tooltipLabel.Text = tooltipText;
                _tooltipPanel.Visible = true;
            };
            tilePanel.MouseLeft += (_, _) =>
            {
                tilePanel.Background = new SolidBrush(new Color(40, 40, 55, 140));
                if (_hoveredTileData == data)
                {
                    _hoveredTileData = null;
                    _tooltipPanel.Visible = false;
                }
            };

            // Click handler
            tilePanel.TouchDown += (_, _) => OnTileClicked(data);

            return tilePanel;
        }

        /// <summary>
        /// Position the tooltip near the mouse cursor each frame.
        /// </summary>
        private void UpdateTooltipPosition()
        {
            if (!_tooltipPanel.Visible || _hoveredTileData == null)
                return;

            var desktop = _uiManager.Desktop;
            if (desktop == null) return;

            // Ensure tooltip is on the Desktop widget list
            if (!desktop.Widgets.Contains(_tooltipPanel))
                desktop.Widgets.Add(_tooltipPanel);

            // Use the centralized helper for scale-aware mouse coordinates.
            var mousePos = _uiManager.GetLogicalMousePosition();

            _tooltipPanel.Left = (int)(mousePos.X + 16);
            _tooltipPanel.Top = (int)(mousePos.Y + 16);
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var btn = _tabButtons[i];
                if (i == _activeTabIndex)
                {
                    btn.Background = new SolidBrush(new Color(70, 70, 100));
                    btn.OverBackground = new SolidBrush(new Color(80, 80, 110));
                }
                else
                {
                    btn.Background = new SolidBrush(new Color(40, 40, 55));
                    btn.OverBackground = new SolidBrush(new Color(55, 55, 75));
                }
            }
        }

        private static string TruncateName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= maxLength ? name : name[..(maxLength - 2)] + "..";
        }
    }

    /// <summary>
    /// Data for a single tile in the grid.
    /// </summary>
    public class GridTileData
    {
        public int ItemID { get; init; }
        public string Name { get; init; }
        public int Amount { get; init; }
        public Texture2D IconTexture { get; set; }
        public object Tag { get; init; }

        /// <summary>
        /// Multi-line tooltip text shown on hover. If empty, Name + Amount is used.
        /// </summary>
        public string TooltipText { get; init; }
    }
}
