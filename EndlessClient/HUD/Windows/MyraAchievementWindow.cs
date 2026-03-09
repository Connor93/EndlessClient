using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Rendering.Character;
using EndlessClient.UI.Myra;
using EOLib.Domain.Achievement;
using EOLib.Domain.Character;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Myra-based achievement window. Shows all achievements with filter tabs,
    /// progress bars, tier rewards, badge selection, and leaderboard overlay.
    /// </summary>
    public class MyraAchievementWindow : DrawableGameComponent, IAchievementWindow
    {
        private enum AchievementTab { All, NpcKills, Quests, Maps, Equipment, Crafting, Pets, Badges }

        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IAchievementProvider _achievementProvider;
        private readonly IAchievementActions _achievementActions;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IContentProvider _contentProvider;
        private readonly ICharacterRepository _characterRepository;
        private Texture2D _badgeSheet;

        private const int BadgeIconSize = 12;
        private const int WindowWidth = 380;
        private const int WindowHeight = 500;
        private const double PollIntervalSeconds = 3.0;

        private Window _window;
        private VerticalStackPanel _tabBar;
        private ScrollViewer _scrollViewer;
        private VerticalStackPanel _achievementList;
        private HorizontalStackPanel _badgeFooter;
        private Label _badgeCountLabel;
        private Button _badgeSaveBtn;
        private Panel _leaderboardOverlay;

        private AchievementTab _activeTab = AchievementTab.All;
        private int _selectedAchievementId = -1;
        private int _leaderboardAchievementId = -1;
        private IReadOnlyList<LeaderboardEntry> _leaderboardEntries = Array.Empty<LeaderboardEntry>();
        private IReadOnlyList<AchievementDefinition> _lastAchievements = Array.Empty<AchievementDefinition>();
        private IReadOnlyList<AchievementDefinition> _filteredAchievements = Array.Empty<AchievementDefinition>();
        private readonly HashSet<int> _selectedBadgeIds = new HashSet<int>();
        private bool _badgesDirty;
        private double _pollTimer;
        private bool _dataRequested;

        // Tier colors
        private static readonly Color[] TierColors = new[]
        {
            new Color(220, 220, 240),  // Tier 1 - Silver
            new Color(255, 215, 0),    // Tier 2 - Gold
            new Color(0, 191, 255),    // Tier 3 - Diamond
            new Color(148, 103, 189),  // Tier 4 - Purple
            new Color(255, 69, 0),     // Tier 5 - Legendary
        };

        private static readonly Color ProgressBarBg = new Color(40, 40, 50);
        private static readonly Color ProgressBarFill = new Color(0x4C, 0xAF, 0x50);
        private static readonly Color ProgressBarComplete = new Color(0xFF, 0xD7, 0x00);
        private static readonly Color CardBorderColor = new Color(80, 80, 100, 150);
        private static readonly Color SecondaryTextColor = new Color(0xB0, 0xB0, 0xB0);
        private static readonly Color GoldColor = new Color(0xD4, 0xA5, 0x37);
        private static readonly Color SelectedBorder = new Color(0x4C, 0xAF, 0x50, 180);

        public MyraAchievementWindow(
            Game game,
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IAchievementProvider achievementProvider,
            IAchievementActions achievementActions,
            IEIFFileProvider eifFileProvider,
            IContentProvider contentProvider,
            ICharacterRepository characterRepository)
            : base(game)
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _achievementProvider = achievementProvider;
            _achievementActions = achievementActions;
            _eifFileProvider = eifFileProvider;
            _contentProvider = contentProvider;
            _characterRepository = characterRepository;
        }

        public override void Initialize()
        {
            if (_contentProvider.Textures.ContainsKey(ContentProvider.IconBadges))
                _badgeSheet = _contentProvider.Textures[ContentProvider.IconBadges];

            _window = new Window
            {
                Title = "Achievements",
                TitleFont = _fontProvider.Header,
                Width = WindowWidth,
                Height = WindowHeight,
                Left = 100,
                Top = 100,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            var mainPanel = new VerticalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Tab bar
            _tabBar = BuildTabBar();
            mainPanel.Widgets.Add(_tabBar);

            // Achievement list in scroll viewer
            _achievementList = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Content = _achievementList,
                ShowHorizontalScrollBar = false,
            };
            mainPanel.Widgets.Add(_scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto)); // tab bar
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill)); // scroll view

            // Badge footer (hidden until Badges tab)
            _badgeFooter = BuildBadgeFooter();
            _badgeFooter.Visible = false;
            mainPanel.Widgets.Add(_badgeFooter);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto)); // footer

            // Leaderboard overlay (invisible by default)
            _leaderboardOverlay = new Panel
            {
                Visible = false,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(25, 25, 35, 240)),
            };

            _window.Content = mainPanel;
            _uiManager.Desktop.Widgets.Add(_window);

            base.Initialize();
        }

        private VerticalStackPanel BuildTabBar()
        {
            var tabNames = new[] { "All", "Kills", "Quest", "Maps", "Equip", "Craft", "Pets", "Badge" };
            var container = new VerticalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // First row
            var row1 = new HorizontalStackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            // Second row
            var row2 = new HorizontalStackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            for (int i = 0; i < tabNames.Length; i++)
            {
                var tabIndex = i;
                var btn = new Button
                {
                    Content = new Label { Text = tabNames[i], Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center },
                    Width = (WindowWidth - 32) / 4,
                    Height = 22,
                };
                btn.Click += (_, _) =>
                {
                    _activeTab = (AchievementTab)tabIndex;
                    _scrollViewer.ScrollPosition = Point.Zero;
                    _selectedAchievementId = -1;
                    _leaderboardAchievementId = -1;
                    _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                    HideLeaderboardOverlay();

                    if (_activeTab == AchievementTab.Badges)
                    {
                        _selectedBadgeIds.Clear();
                        foreach (var id in _achievementProvider.SelectedBadgeIds)
                            _selectedBadgeIds.Add(id);
                        _badgesDirty = false;
                    }

                    _badgeFooter.Visible = _activeTab == AchievementTab.Badges;
                    UpdateTabHighlights();
                    RebuildFilteredList();
                    RebuildAchievementList();
                };

                if (i < 4)
                    row1.Widgets.Add(btn);
                else
                    row2.Widgets.Add(btn);
            }

            container.Widgets.Add(row1);
            container.Widgets.Add(row2);
            return container;
        }

        private void UpdateTabHighlights()
        {
            var idx = (int)_activeTab;
            var allBtns = new List<Button>();
            foreach (var row in _tabBar.Widgets.OfType<HorizontalStackPanel>())
                allBtns.AddRange(row.Widgets.OfType<Button>());

            for (int i = 0; i < allBtns.Count; i++)
            {
                if (allBtns[i].Content is Label lbl)
                    lbl.TextColor = i == idx ? Color.White : SecondaryTextColor;
            }
        }

        private HorizontalStackPanel BuildBadgeFooter()
        {
            var footer = new HorizontalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(6, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 36,
            };

            _badgeCountLabel = new Label
            {
                Text = "0/3 selected",
                Font = _fontProvider.Normal,
                TextColor = SecondaryTextColor,
                VerticalAlignment = VerticalAlignment.Center,
            };
            footer.Widgets.Add(_badgeCountLabel);
            footer.Proportions.Add(new Proportion(ProportionType.Fill));

            var btnLabel = new Label { Text = "Saved", Font = _fontProvider.Normal, HorizontalAlignment = HorizontalAlignment.Center };
            _badgeSaveBtn = new Button
            {
                Content = btnLabel,
                Width = 80,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _badgeSaveBtn.Click += (_, _) =>
            {
                if (_badgesDirty)
                {
                    _achievementActions.SendBadgeSelection(_selectedBadgeIds.ToArray());
                    _achievementActions.RequestBadgeData();

                    // Immediately update the local character's badge names so the
                    // nameplate reflects the change without waiting for the server roundtrip.
                    var badgeNames = _selectedBadgeIds
                        .Select(id => _achievementProvider.Achievements.FirstOrDefault(a => a.Id == id))
                        .Where(a => a != null)
                        .Select(a => a.Name)
                        .ToArray();
                    _characterRepository.MainCharacter = _characterRepository.MainCharacter
                        .WithBadgeNames(badgeNames);

                    _badgesDirty = false;
                    btnLabel.Text = "Saved";
                }
            };
            footer.Widgets.Add(_badgeSaveBtn);
            footer.Proportions.Add(new Proportion(ProportionType.Auto));

            return footer;
        }

        public void Toggle()
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible)
            {
                _window.BringToFront();
                _achievementActions.RequestAchievements();
                _dataRequested = true;
                _pollTimer = 0;
                _selectedAchievementId = -1;
                _leaderboardAchievementId = -1;
                _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                HideLeaderboardOverlay();
                UpdateTabHighlights();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_window.Visible)
            {
                // Poll for achievement data
                _pollTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_pollTimer >= PollIntervalSeconds)
                {
                    _pollTimer = 0;
                    _achievementActions.RequestAchievements();
                }

                // Check for data changes
                var current = _achievementProvider.Achievements;
                if (current != _lastAchievements)
                {
                    _lastAchievements = current;
                    RebuildFilteredList();
                    RebuildAchievementList();

                    if (_leaderboardAchievementId > 0)
                        _achievementActions.RequestLeaderboard(_leaderboardAchievementId);
                }

                // Check for leaderboard data update
                if (_leaderboardAchievementId > 0 &&
                    _achievementProvider.LeaderboardAchievementId == _leaderboardAchievementId)
                {
                    var newEntries = _achievementProvider.LeaderboardEntries;
                    if (newEntries != _leaderboardEntries)
                    {
                        _leaderboardEntries = newEntries;
                        ShowLeaderboardOverlay();
                    }
                }
            }

            base.Update(gameTime);
        }

        private void RebuildFilteredList()
        {
            var all = _lastAchievements;
            if (all == null || all.Count == 0)
            {
                _filteredAchievements = Array.Empty<AchievementDefinition>();
                return;
            }

            IEnumerable<AchievementDefinition> filtered = all;

            if (_activeTab != AchievementTab.All && _activeTab != AchievementTab.Badges)
            {
                filtered = _activeTab switch
                {
                    AchievementTab.NpcKills => filtered.Where(a => a.Type == "kill_npc"),
                    AchievementTab.Quests => filtered.Where(a => a.Type == "unique_quests"),
                    AchievementTab.Maps => filtered.Where(a => a.Type == "unique_maps"),
                    AchievementTab.Equipment => filtered.Where(a =>
                        a.Type == "unique_weapons" || a.Type == "unique_armors" ||
                        a.Type == "unique_shields" || a.Type == "unique_hats" ||
                        a.Type == "unique_boots"),
                    AchievementTab.Crafting => filtered.Where(a => a.Type == "unique_crafts"),
                    AchievementTab.Pets => filtered.Where(a => a.Type == "unique_pets"),
                    _ => filtered
                };
            }

            if (_activeTab == AchievementTab.Badges)
            {
                var maxedIds = _achievementProvider.MaxedAchievementIds;
                filtered = filtered.Where(a => maxedIds.Contains(a.Id));
            }

            _filteredAchievements = filtered
                .OrderByDescending(a => a.CurrentTier)
                .ThenByDescending(a => a.CurrentProgress)
                .ToList();
        }

        private void RebuildAchievementList()
        {
            _achievementList.Widgets.Clear();

            if (_filteredAchievements.Count == 0)
            {
                var msg = _dataRequested ? "No achievements found" : "Loading...";
                _achievementList.Widgets.Add(new Label
                {
                    Text = msg,
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                    Padding = new Thickness(8),
                });
                return;
            }

            foreach (var ach in _filteredAchievements)
            {
                _achievementList.Widgets.Add(BuildAchievementCard(ach));
            }
        }

        private Panel BuildAchievementCard(AchievementDefinition ach)
        {
            var isBadgeTab = _activeTab == AchievementTab.Badges;
            var isBadgeSelected = isBadgeTab && _selectedBadgeIds.Contains(ach.Id);
            var isSelected = ach.Id == _selectedAchievementId;

            var card = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(6, 4),
                Height = 72,
                Border = new Myra.Graphics2D.Brushes.SolidBrush(isBadgeSelected ? SelectedBorder : (isSelected ? SelectedBorder : CardBorderColor)),
                Background = new Myra.Graphics2D.Brushes.SolidBrush(isSelected || isBadgeSelected ? new Color(50, 50, 60, 240) : new Color(35, 35, 45, 230)),
            };

            var content = new VerticalStackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Row 1: Name + Type label
            var headerRow = new HorizontalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var nameLabel = new Label
            {
                Text = ach.Name,
                Font = _fontProvider.Normal,
                TextColor = Color.White,
            };
            headerRow.Widgets.Add(nameLabel);
            headerRow.Proportions.Add(new Proportion(ProportionType.Fill));

            // Badge icon
            if (_badgeSheet != null && CharacterNamePlate.BadgeIconIndex.TryGetValue(ach.Name, out _))
            {
                // Note: Myra doesn't natively support sprite sheet sub-regions easily.
                // We'll indicate badge-eligible with a text marker for now.
                var badgeMarker = new Label
                {
                    Text = "\u2605",
                    Font = _fontProvider.Small,
                    TextColor = GoldColor,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                headerRow.Widgets.Add(badgeMarker);
                headerRow.Proportions.Add(new Proportion(ProportionType.Auto));
            }

            // Type label
            var typeLabel = new Label
            {
                Text = GetTypeLabel(ach.Type),
                Font = _fontProvider.Small,
                TextColor = SecondaryTextColor,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            headerRow.Widgets.Add(typeLabel);
            headerRow.Proportions.Add(new Proportion(ProportionType.Auto));

            // Badge selected indicator
            if (isBadgeTab && isBadgeSelected)
            {
                var selLabel = new Label
                {
                    Text = " SELECTED",
                    Font = _fontProvider.Small,
                    TextColor = GoldColor,
                };
                headerRow.Widgets.Add(selLabel);
                headerRow.Proportions.Add(new Proportion(ProportionType.Auto));
            }

            content.Widgets.Add(headerRow);

            // Description (if any)
            if (!string.IsNullOrEmpty(ach.Description))
            {
                content.Widgets.Add(new Label
                {
                    Text = ach.Description,
                    Font = _fontProvider.Small,
                    TextColor = SecondaryTextColor,
                });
            }

            // Progress bar
            float progressRatio = 0;
            if (ach.Tiers.Length > 0)
            {
                var idx = ach.CurrentTier;
                if (idx < ach.Tiers.Length)
                {
                    var next = ach.Tiers[idx].Threshold;
                    var prev = idx > 0 ? ach.Tiers[idx - 1].Threshold : 0;
                    var range = next - prev;
                    if (range > 0)
                        progressRatio = Math.Clamp((float)(ach.CurrentProgress - prev) / range, 0, 1);
                }
                else
                {
                    progressRatio = 1;
                }
            }

            var progressBar = new HorizontalProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)(progressRatio * 100),
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            content.Widgets.Add(progressBar);

            // Tier info row
            var tierRow = new HorizontalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var tierColorIdx = Math.Max(0, Math.Min(ach.CurrentTier - 1, TierColors.Length - 1));
            var tierColor = ach.CurrentTier > 0 ? TierColors[tierColorIdx] : SecondaryTextColor;
            var tierText = $"Tier {ach.CurrentTier}/{ach.Tiers.Length}";

            tierRow.Widgets.Add(new Label
            {
                Text = tierText,
                Font = _fontProvider.Small,
                TextColor = tierColor,
            });
            tierRow.Proportions.Add(new Proportion(ProportionType.Auto));

            // Next tier reward or complete
            if (ach.CurrentTier < ach.Tiers.Length)
            {
                var nextTier = ach.Tiers[ach.CurrentTier];
                var rewardParts = new List<string>();
                if (nextTier.ExpReward > 0) rewardParts.Add($"{nextTier.ExpReward} EXP");
                if (nextTier.ItemId > 0)
                {
                    var itemName = ResolveItemName(nextTier.ItemId);
                    rewardParts.Add($"{itemName} x{nextTier.ItemAmount}");
                }
                if (rewardParts.Count > 0)
                {
                    tierRow.Widgets.Add(new Label
                    {
                        Text = " \u279C " + string.Join(", ", rewardParts),
                        Font = _fontProvider.Small,
                        TextColor = GoldColor,
                    });
                    tierRow.Proportions.Add(new Proportion(ProportionType.Fill));
                }
            }
            else
            {
                tierRow.Widgets.Add(new Label
                {
                    Text = " \u2605 Complete!",
                    Font = _fontProvider.Small,
                    TextColor = ProgressBarComplete,
                });
                tierRow.Proportions.Add(new Proportion(ProportionType.Fill));
            }

            // Progress count
            var progressText = $"{ach.CurrentProgress}";
            if (ach.CurrentTier < ach.Tiers.Length)
                progressText += $"/{ach.Tiers[ach.CurrentTier].Threshold}";

            tierRow.Widgets.Add(new Label
            {
                Text = progressText,
                Font = _fontProvider.Small,
                TextColor = SecondaryTextColor,
                HorizontalAlignment = HorizontalAlignment.Right,
            });
            tierRow.Proportions.Add(new Proportion(ProportionType.Auto));

            content.Widgets.Add(tierRow);

            card.Widgets.Add(content);

            // Click handler
            var achId = ach.Id;
            card.TouchDown += (_, _) =>
            {
                if (isBadgeTab)
                {
                    // Toggle badge selection
                    if (_selectedBadgeIds.Contains(achId))
                    {
                        _selectedBadgeIds.Remove(achId);
                        _badgesDirty = true;
                    }
                    else if (_selectedBadgeIds.Count < 3)
                    {
                        _selectedBadgeIds.Add(achId);
                        _badgesDirty = true;
                    }
                    UpdateBadgeFooter();
                    RebuildAchievementList();
                    return;
                }

                if (_selectedAchievementId == achId)
                {
                    _selectedAchievementId = -1;
                    _leaderboardAchievementId = -1;
                    _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                    HideLeaderboardOverlay();
                }
                else
                {
                    _selectedAchievementId = achId;
                    _leaderboardAchievementId = achId;
                    _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                    _achievementActions.RequestLeaderboard(achId);
                }
                RebuildAchievementList();
            };

            return card;
        }

        private void UpdateBadgeFooter()
        {
            _badgeCountLabel.Text = $"{_selectedBadgeIds.Count}/3 selected";
            if (_badgeSaveBtn.Content is Label lbl)
                lbl.Text = _badgesDirty ? "Save" : "Saved";
        }

        private void ShowLeaderboardOverlay()
        {
            // Find the selected achievement
            var selectedAch = _filteredAchievements.FirstOrDefault(a => a.Id == _leaderboardAchievementId);
            if (selectedAch == null) return;

            // Build overlay content in a separate window (tooltip-style)
            if (_leaderboardOverlay.Parent != null)
                ((Panel)_leaderboardOverlay.Parent).Widgets.Remove(_leaderboardOverlay);

            _leaderboardOverlay.Widgets.Clear();

            var overlay = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(8),
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(25, 25, 35, 240)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Tier rewards section
            overlay.Widgets.Add(new Label
            {
                Text = "\u2606 Tier Rewards",
                Font = _fontProvider.Normal,
                TextColor = GoldColor,
            });

            for (int t = 0; t < selectedAch.Tiers.Length; t++)
            {
                var tier = selectedAch.Tiers[t];
                var completed = t < selectedAch.CurrentTier;
                var tColorIdx = Math.Max(0, Math.Min(t, TierColors.Length - 1));
                var col = completed ? TierColors[tColorIdx] : SecondaryTextColor;
                var check = completed ? "\u2713 " : "  ";

                var rewardStr = $"Tier {t + 1} ({tier.Threshold}):";
                if (tier.ExpReward > 0) rewardStr += $" {tier.ExpReward} EXP";
                if (tier.ItemId > 0)
                {
                    var itemName = ResolveItemName(tier.ItemId);
                    rewardStr += $" + {itemName} x{tier.ItemAmount}";
                }

                overlay.Widgets.Add(new Label
                {
                    Text = check + rewardStr,
                    Font = _fontProvider.Small,
                    TextColor = col,
                });
            }

            // Leaderboard section
            overlay.Widgets.Add(new Label
            {
                Text = "\u265F Leaderboard",
                Font = _fontProvider.Normal,
                TextColor = new Color(100, 180, 255),
                Padding = new Thickness(0, 6, 0, 0),
            });

            if (_leaderboardEntries.Count == 0)
            {
                overlay.Widgets.Add(new Label
                {
                    Text = "No entries yet",
                    Font = _fontProvider.Small,
                    TextColor = SecondaryTextColor,
                });
            }
            else
            {
                var maxEntries = Math.Min(_leaderboardEntries.Count, 5);
                for (int i = 0; i < maxEntries; i++)
                {
                    var entry = _leaderboardEntries[i];
                    var entryTierIdx = Math.Max(0, Math.Min(entry.TierReached - 1, TierColors.Length - 1));
                    var entryColor = entry.TierReached > 0 ? TierColors[entryTierIdx] : SecondaryTextColor;

                    var row = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
                    row.Widgets.Add(new Label
                    {
                        Text = $"{i + 1}. {entry.Name}",
                        Font = _fontProvider.Small,
                        TextColor = Color.White,
                    });
                    row.Proportions.Add(new Proportion(ProportionType.Fill));

                    row.Widgets.Add(new Label
                    {
                        Text = $"T{entry.TierReached}",
                        Font = _fontProvider.Small,
                        TextColor = entryColor,
                    });
                    row.Proportions.Add(new Proportion(ProportionType.Auto));

                    overlay.Widgets.Add(row);
                }

                if (_leaderboardEntries.Count > 5)
                {
                    overlay.Widgets.Add(new Label
                    {
                        Text = $"...and {_leaderboardEntries.Count - 5} more",
                        Font = _fontProvider.Small,
                        TextColor = SecondaryTextColor,
                    });
                }
            }

            _leaderboardOverlay.Widgets.Add(overlay);
            _leaderboardOverlay.Visible = true;
            _leaderboardOverlay.Width = 220;
            _leaderboardOverlay.Left = _window.Left + WindowWidth + 4;
            _leaderboardOverlay.Top = _window.Top + 60;

            if (!_uiManager.Desktop.Widgets.Contains(_leaderboardOverlay))
                _uiManager.Desktop.Widgets.Add(_leaderboardOverlay);
        }

        private void HideLeaderboardOverlay()
        {
            _leaderboardOverlay.Visible = false;
        }

        private static string GetTypeLabel(string type)
        {
            return type switch
            {
                "kill_npc" => "Kills",
                "unique_quests" => "Quests",
                "unique_maps" => "Explore",
                "unique_weapons" => "Weapons",
                "unique_armors" => "Armor",
                "unique_shields" => "Shields",
                "unique_hats" => "Hats",
                "unique_boots" => "Boots",
                "unique_crafts" => "Crafting",
                "unique_pets" => "Pets",
                _ => type
            };
        }

        private string ResolveItemName(int itemId)
        {
            try
            {
                var record = _eifFileProvider.EIFFile[itemId];
                return !string.IsNullOrEmpty(record.Name) ? record.Name : $"Item #{itemId}";
            }
            catch
            {
                return $"Item #{itemId}";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_leaderboardOverlay?.Parent != null)
                    _uiManager.Desktop.Widgets.Remove(_leaderboardOverlay);
                if (_window != null)
                    _uiManager.Desktop.Widgets.Remove(_window);
            }
            base.Dispose(disposing);
        }
    }
}
