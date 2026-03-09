using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Services;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Online;
using EOLib.Domain.Party;
using EOLib.Shared;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Online Players panel. Shows a scrollable table of online players
    /// with Name, Title, Guild, and Class columns. Supports cycling filter:
    /// All → Friends → Admins → Party → Guild.
    /// </summary>
    public class MyraOnlineListPanel : MyraHudPanelBase
    {
        private enum Filter { All, Friends, Admins, Party, Guild, Max }

        private readonly IOnlinePlayerProvider _onlinePlayerProvider;
        private readonly IFriendIgnoreListService _friendIgnoreListService;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IMyraFontProvider _fontProvider;

        private VerticalStackPanel _listPanel;
        private Label _countLabel;
        private Button _filterBtn;
        private HashSet<OnlinePlayerInfo> _cachedPlayers = new();
        private Filter _filter = Filter.All;

        private static readonly Color HeaderGold = new(0xD4, 0xA5, 0x37);
        private static readonly Color LabelDim = new(0x88, 0x88, 0x99);

        public MyraOnlineListPanel(Game game,
                                   IMyraUIManager uiManager,
                                   IMyraFontProvider fontProvider,
                                   IOnlinePlayerProvider onlinePlayerProvider,
                                   IFriendIgnoreListService friendIgnoreListService,
                                   IPartyDataProvider partyDataProvider,
                                   ICharacterProvider characterProvider)
            : base(game, uiManager, "Online Players")
        {
            _fontProvider = fontProvider;
            _onlinePlayerProvider = onlinePlayerProvider;
            _friendIgnoreListService = friendIgnoreListService;
            _partyDataProvider = partyDataProvider;
            _characterProvider = characterProvider;
        }

        public override void Initialize()
        {
            Window.Width = 484;
            Window.Height = 180;
            Window.TitleFont = _fontProvider.Large;

            var root = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Filter bar
            var filterBar = new HorizontalStackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var filterLabel = new Label
            {
                Text = "Filter: All",
                Font = _fontProvider.Normal,
            };
            _filterBtn = new Button
            {
                Content = filterLabel,
                Width = 140,
                Height = 26,
            };
            _filterBtn.Click += (_, _) =>
            {
                _filter = (Filter)(((int)_filter + 1) % (int)Filter.Max);
                filterLabel.Text = $"Filter: {_filter}";
                RebuildList();
            };
            filterBar.Widgets.Add(_filterBtn);

            _countLabel = new Label
            {
                Text = "0 players online",
                Font = _fontProvider.Small,
                TextColor = LabelDim,
                VerticalAlignment = VerticalAlignment.Center,
            };
            filterBar.Widgets.Add(_countLabel);

            root.Widgets.Add(filterBar);

            // Header row
            var headerGrid = CreatePlayerRow("Name", "Title", "Guild", "Class", isHeader: true);
            root.Widgets.Add(headerGrid);
            root.Widgets.Add(new HorizontalSeparator());

            // Scrollable list
            var scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Height = 100,
            };

            _listPanel = new VerticalStackPanel
            {
                Spacing = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            scrollViewer.Content = _listPanel;
            root.Widgets.Add(scrollViewer);

            Window.Content = root;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (Window.Visible && !_cachedPlayers.SetEquals(_onlinePlayerProvider.OnlinePlayers))
            {
                _cachedPlayers = _onlinePlayerProvider.OnlinePlayers.ToHashSet();
                RebuildList();
            }

            base.Update(gameTime);
        }

        private void RebuildList()
        {
            _listPanel.Widgets.Clear();

            var allPlayers = _cachedPlayers
                .OrderBy(p => p.Name)
                .ToList();

            var filtered = ApplyFilter(allPlayers);

            foreach (var player in filtered)
            {
                _listPanel.Widgets.Add(CreatePlayerRow(player.Name, player.Title, player.Guild, player.Class, isHeader: false));
            }

            _countLabel.Text = $"{filtered.Count} / {allPlayers.Count} online";
        }

        private List<OnlinePlayerInfo> ApplyFilter(List<OnlinePlayerInfo> allPlayers)
        {
            switch (_filter)
            {
                case Filter.Friends:
                    var friendList = _friendIgnoreListService.LoadList(Constants.FriendListFile);
                    return allPlayers.Where(x => friendList.Contains(x.Name, StringComparer.InvariantCultureIgnoreCase)).ToList();

                case Filter.Admins:
                    return allPlayers.Where(IsAdminIcon).ToList();

                case Filter.Party:
                    return allPlayers.Where(x => _partyDataProvider.Members.Any(y =>
                        string.Equals(y.Name, x.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();

                case Filter.Guild:
                    var myGuildTag = _characterProvider.MainCharacter.GuildTag;
                    if (string.IsNullOrWhiteSpace(myGuildTag) || myGuildTag == "   ")
                        return new List<OnlinePlayerInfo>();
                    return allPlayers.Where(x => string.Equals(x.Guild, myGuildTag, StringComparison.InvariantCultureIgnoreCase)).ToList();

                case Filter.All:
                default:
                    return allPlayers;
            }
        }

        private static bool IsAdminIcon(OnlinePlayerInfo info)
        {
            return info.Icon is CharacterIcon.Gm or CharacterIcon.Hgm or CharacterIcon.GmParty or CharacterIcon.HgmParty;
        }

        private Grid CreatePlayerRow(string name, string title, string guild, string className, bool isHeader)
        {
            var grid = new Grid { ColumnSpacing = 4 };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 120)); // Name
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 100)); // Title
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 120)); // Guild
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));        // Class

            var font = isHeader ? _fontProvider.Normal : _fontProvider.Small;
            var color = isHeader ? HeaderGold : (Color?)null;

            AddColumnLabel(grid, name, 0, font, color);
            AddColumnLabel(grid, title, 1, font, color);
            AddColumnLabel(grid, guild, 2, font, color);
            AddColumnLabel(grid, className, 3, font, color);

            return grid;
        }

        private static void AddColumnLabel(Grid grid, string text, int col, SpriteFontBase font, Color? color)
        {
            var label = new Label
            {
                Text = text ?? "",
                Font = font,
                Padding = new Thickness(2, 0),
            };
            if (color.HasValue) label.TextColor = color.Value;
            Grid.SetColumn(label, col);
            grid.Widgets.Add(label);
        }
    }
}
