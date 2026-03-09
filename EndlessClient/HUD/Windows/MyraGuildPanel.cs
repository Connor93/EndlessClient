using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Map;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Myra-based guild management panel. 5 tabs: Overview, Members, Bounties, Perks, Buffs.
    /// Action buttons with rank-aware visibility. Polls for data updates.
    /// </summary>
    public class MyraGuildPanel : DrawableGameComponent, IGuildPanel
    {
        private enum GuildTab { Overview, Members, Bounties, Perks, Buffs }

        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;
        private readonly IChatActions _chatActions;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ITextMultiInputDialogFactory _textMultiInputDialogFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataRepository _lockerDataRepository;

        private const int WindowWidth = 320;
        private const int WindowHeight = 340;
        private const double PollIntervalSeconds = 2.0;

        private Window _window;
        private ScrollViewer _scrollViewer;
        private VerticalStackPanel _tabContent;
        private HorizontalStackPanel _actionBar;

        private GuildTab _activeTab = GuildTab.Overview;
        private GuildInfoData _guildInfo;
        private IReadOnlyList<CustomBountyData> _customBounties = new List<CustomBountyData>();
        private IReadOnlyList<GuildPerkData> _guildPerks = new List<GuildPerkData>();
        private IReadOnlyList<GuildBuffData> _guildBuffs = new List<GuildBuffData>();
        private IReadOnlyList<GuildMemberInfo> _guildMemberList = new List<GuildMemberInfo>();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        private static readonly Color SecondaryTextColor = new Color(0xB0, 0xB0, 0xB0);
        private static readonly Color GoldColor = new Color(0xD4, 0xA5, 0x37);
        private static readonly Color SuccessColor = new Color(0x4C, 0xAF, 0x50);
        private static readonly Color DangerColor = new Color(0xE5, 0x73, 0x73);
        private static readonly Color HighlightColor = new Color(100, 180, 255);
        private static readonly Color DividerColor = new Color(80, 80, 100, 100);

        public MyraGuildPanel(
            Game game,
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IBountyDataProvider bountyDataProvider,
            IQuestActions questActions,
            IChatActions chatActions,
            ITextInputDialogFactory textInputDialogFactory,
            ITextMultiInputDialogFactory textMultiInputDialogFactory,
            ICharacterProvider characterProvider,
            ILockerDataRepository lockerDataRepository)
            : base(game)
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _bountyDataProvider = bountyDataProvider;
            _questActions = questActions;
            _chatActions = chatActions;
            _textInputDialogFactory = textInputDialogFactory;
            _textMultiInputDialogFactory = textMultiInputDialogFactory;
            _characterProvider = characterProvider;
            _lockerDataRepository = lockerDataRepository;
        }

        public override void Initialize()
        {
            _window = new Window
            {
                Title = "Guild",
                TitleFont = _fontProvider.Header,
                Width = WindowWidth,
                Height = WindowHeight,
                Left = 120,
                Top = 120,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            var mainPanel = new VerticalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Tab bar
            var tabBar = BuildTabBar();
            mainPanel.Widgets.Add(tabBar);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            // Scrollable content area
            _tabContent = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Content = _tabContent,
                ShowHorizontalScrollBar = false,
            };
            mainPanel.Widgets.Add(_scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // Action bar at bottom
            _actionBar = new HorizontalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(6, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 32,
            };
            mainPanel.Widgets.Add(_actionBar);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            _window.Content = mainPanel;
            _uiManager.Desktop.Widgets.Add(_window);

            base.Initialize();
        }

        private HorizontalStackPanel BuildTabBar()
        {
            var tabNames = new[] { "Info", "Members", "Bounty", "Perks", "Buffs" };
            var tabBar = new HorizontalStackPanel
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
                    Width = (WindowWidth - 32) / 5,
                    Height = 22,
                };
                btn.Click += (_, _) =>
                {
                    _activeTab = (GuildTab)tabIndex;
                    _scrollViewer.ScrollPosition = Point.Zero;
                    RebuildTabContent();
                    RebuildActionBar();
                    UpdateTabHighlights();
                };
                tabBar.Widgets.Add(btn);
            }

            return tabBar;
        }

        private void UpdateTabHighlights()
        {
            var parent = _window.Content as VerticalStackPanel;
            if (parent == null) return;
            var tabBar = parent.Widgets[0] as HorizontalStackPanel;
            if (tabBar == null) return;

            var idx = (int)_activeTab;
            for (int i = 0; i < tabBar.Widgets.Count; i++)
            {
                if (tabBar.Widgets[i] is Button btn && btn.Content is Label lbl)
                    lbl.TextColor = i == idx ? Color.White : SecondaryTextColor;
            }
        }

        public void Toggle()
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible)
            {
                _window.BringToFront();
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _pollStopwatch.Restart();
                RebuildTabContent();
                RebuildActionBar();
                UpdateTabHighlights();
            }
            else
            {
                _pollStopwatch.Stop();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_window.Visible)
            {
                if (!_pollStopwatch.IsRunning)
                    _pollStopwatch.Start();

                if (_pollStopwatch.Elapsed.TotalSeconds >= PollIntervalSeconds)
                {
                    _pollStopwatch.Restart();
                    _questActions.RequestQuestHistory(QuestPage.Progress);
                }

                var dirty = false;

                var currentInfo = _bountyDataProvider.GuildInfo;
                if (currentInfo != _guildInfo)
                {
                    _guildInfo = currentInfo;
                    dirty = true;
                }

                if (_bountyDataProvider.CustomBounties != _customBounties)
                {
                    _customBounties = _bountyDataProvider.CustomBounties;
                    if (_activeTab == GuildTab.Bounties) dirty = true;
                }

                if (_bountyDataProvider.GuildPerks != _guildPerks)
                {
                    _guildPerks = _bountyDataProvider.GuildPerks;
                    if (_activeTab == GuildTab.Perks) dirty = true;
                }

                if (_bountyDataProvider.GuildBuffs != _guildBuffs)
                {
                    _guildBuffs = _bountyDataProvider.GuildBuffs;
                    if (_activeTab == GuildTab.Buffs) dirty = true;
                }

                if (_bountyDataProvider.GuildMemberList != _guildMemberList)
                {
                    _guildMemberList = _bountyDataProvider.GuildMemberList;
                    if (_activeTab == GuildTab.Members) dirty = true;
                }

                if (dirty)
                {
                    RebuildTabContent();
                    RebuildActionBar();
                }
            }
            else
            {
                _pollStopwatch.Stop();
            }

            base.Update(gameTime);
        }

        private void RebuildTabContent()
        {
            _tabContent.Widgets.Clear();

            switch (_activeTab)
            {
                case GuildTab.Overview:
                    BuildOverviewContent();
                    break;
                case GuildTab.Members:
                    BuildMembersContent();
                    break;
                case GuildTab.Bounties:
                    BuildBountiesContent();
                    break;
                case GuildTab.Perks:
                    BuildPerksContent();
                    break;
                case GuildTab.Buffs:
                    BuildBuffsContent();
                    break;
            }
        }

        // ────────────────────────────── Overview Tab ──────────────────────────────

        private void BuildOverviewContent()
        {
            if (_guildInfo == null)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "No guild data",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                });
                return;
            }

            // Level
            var levelRow = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            levelRow.Widgets.Add(new Label
            {
                Text = "Level " + _guildInfo.Level,
                Font = _fontProvider.Normal,
                TextColor = Color.White,
            });
            levelRow.Proportions.Add(new Proportion(ProportionType.Fill));

            if (_guildInfo.OnlineCount > 0)
            {
                levelRow.Widgets.Add(new Label
                {
                    Text = _guildInfo.OnlineCount + " Online",
                    Font = _fontProvider.Normal,
                    TextColor = SuccessColor,
                });
                levelRow.Proportions.Add(new Proportion(ProportionType.Auto));
            }
            _tabContent.Widgets.Add(levelRow);

            // EXP bar
            var expProgress = new HorizontalProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            if (_guildInfo.ExpToNext > 0)
            {
                var pct = (int)(100.0 * _guildInfo.Exp / (_guildInfo.Exp + _guildInfo.ExpToNext));
                expProgress.Value = pct;
            }
            else
            {
                expProgress.Value = 100;
            }
            _tabContent.Widgets.Add(expProgress);

            var expText = _guildInfo.ExpToNext > 0
                ? $"{_guildInfo.Exp}/{_guildInfo.Exp + _guildInfo.ExpToNext}"
                : "MAX";
            _tabContent.Widgets.Add(new Label
            {
                Text = expText,
                Font = _fontProvider.Small,
                TextColor = SecondaryTextColor,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            // Points and contribution
            AddStatRow("Guild Points", _guildInfo.Points.ToString(), Color.White);
            AddStatRow("My Contribution", _guildInfo.Contribution.ToString(), SecondaryTextColor);
            AddStatRow("Guild Bank", _guildInfo.Bank.ToString("N0") + "g", GoldColor);

            // Divider
            AddDivider();

            // Active buffs
            AddSectionHeader("Active Buffs");
            if (!string.IsNullOrEmpty(_guildInfo.ActiveBuffs))
            {
                var buffs = _guildInfo.ActiveBuffs.Split(',');
                foreach (var buff in buffs)
                {
                    var trimmed = buff.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var label = trimmed.Contains("tier1") ? "T1"
                              : trimmed.Contains("tier2") ? "T2"
                              : trimmed.Contains("tier3") ? "T3"
                              : trimmed;
                    _tabContent.Widgets.Add(new Label
                    {
                        Text = "• " + label,
                        Font = _fontProvider.Normal,
                        TextColor = SuccessColor,
                    });
                }
            }
            else
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "No active buffs",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                });
            }
        }

        // ────────────────────────────── Members Tab ──────────────────────────────

        private void BuildMembersContent()
        {
            // Column headers
            var headerRow = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            headerRow.Widgets.Add(new Label { Text = "Name", Font = _fontProvider.Normal, TextColor = HighlightColor });
            headerRow.Proportions.Add(new Proportion(ProportionType.Fill));
            headerRow.Widgets.Add(new Label { Text = "Lv", Font = _fontProvider.Normal, TextColor = HighlightColor, Width = 40 });
            headerRow.Proportions.Add(new Proportion(ProportionType.Auto));
            headerRow.Widgets.Add(new Label { Text = "GP", Font = _fontProvider.Normal, TextColor = HighlightColor, Width = 60 });
            headerRow.Proportions.Add(new Proportion(ProportionType.Auto));
            _tabContent.Widgets.Add(headerRow);

            if (_guildMemberList.Count == 0)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "Loading member data...",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                });
                return;
            }

            foreach (var member in _guildMemberList)
            {
                var row = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
                var displayName = member.Name.Length > 16 ? member.Name.Substring(0, 14) + ".." : member.Name;
                row.Widgets.Add(new Label { Text = displayName, Font = _fontProvider.Normal, TextColor = Color.White });
                row.Proportions.Add(new Proportion(ProportionType.Fill));
                row.Widgets.Add(new Label { Text = member.Level.ToString(), Font = _fontProvider.Normal, TextColor = HighlightColor, Width = 40 });
                row.Proportions.Add(new Proportion(ProportionType.Auto));
                row.Widgets.Add(new Label { Text = member.LifetimeGuildPoints.ToString("N0"), Font = _fontProvider.Normal, TextColor = GoldColor, Width = 60 });
                row.Proportions.Add(new Proportion(ProportionType.Auto));
                _tabContent.Widgets.Add(row);
            }
        }

        // ────────────────────────────── Bounties Tab ──────────────────────────────

        private void BuildBountiesContent()
        {
            var myName = _characterProvider?.MainCharacter?.Name ?? "";

            // Daily bounties
            AddSectionHeader("Daily Bounties");
            var dailyBounties = _bountyDataProvider.Bounties;
            if (dailyBounties == null || dailyBounties.Count == 0)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "No daily bounties. Use Refresh.",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                    Padding = new Thickness(4, 0),
                });
            }
            else
            {
                foreach (var bounty in dailyBounties)
                {
                    _tabContent.Widgets.Add(new Label
                    {
                        Text = bounty.Name,
                        Font = _fontProvider.Normal,
                        TextColor = Color.White,
                        Padding = new Thickness(4, 0),
                    });

                    var bar = new HorizontalProgressBar
                    {
                        Minimum = 0,
                        Maximum = Math.Max(1, bounty.Target),
                        Value = bounty.Progress,
                        Height = 8,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };
                    _tabContent.Widgets.Add(bar);

                    var progressText = $"{bounty.Progress}/{bounty.Target}";
                    _tabContent.Widgets.Add(new Label
                    {
                        Text = progressText,
                        Font = _fontProvider.Small,
                        TextColor = bounty.Progress >= bounty.Target ? SuccessColor : SecondaryTextColor,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });
                }
            }

            AddDivider();

            // Request Board
            AddSectionHeader("Request Board");
            if (_customBounties == null || _customBounties.Count == 0)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "No active requests.",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                    Padding = new Thickness(4, 0),
                });
            }
            else
            {
                foreach (var bounty in _customBounties)
                {
                    var bountyRow = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

                    var itemLine = $"{bounty.ItemName} x{bounty.Amount}";
                    bountyRow.Widgets.Add(new Label
                    {
                        Text = itemLine,
                        Font = _fontProvider.Normal,
                        TextColor = Color.White,
                    });
                    bountyRow.Proportions.Add(new Proportion(ProportionType.Fill));

                    // Per-bounty action buttons (rank-aware)
                    if (bounty.Status == CustomBountyStatus.Open &&
                        !string.Equals(bounty.Poster, myName, StringComparison.OrdinalIgnoreCase))
                    {
                        var acceptBtn = CreateSmallButton("Accept");
                        var bId = bounty.Id;
                        acceptBtn.Click += (_, _) => _chatActions.SendChatToServer("#guild accept " + bId, string.Empty, ChatType.Command);
                        bountyRow.Widgets.Add(acceptBtn);
                        bountyRow.Proportions.Add(new Proportion(ProportionType.Auto));
                    }
                    else if (bounty.Status == CustomBountyStatus.Accepted &&
                             string.Equals(bounty.AcceptedBy, myName, StringComparison.OrdinalIgnoreCase))
                    {
                        var deliverBtn = CreateSmallButton("Deliver");
                        var bId = bounty.Id;
                        deliverBtn.Click += (_, _) => _chatActions.SendChatToServer("#guild deliver " + bId, string.Empty, ChatType.Command);
                        bountyRow.Widgets.Add(deliverBtn);
                        bountyRow.Proportions.Add(new Proportion(ProportionType.Auto));
                    }
                    else if (string.Equals(bounty.Poster, myName, StringComparison.OrdinalIgnoreCase))
                    {
                        var cancelBtn = CreateSmallButton("Cancel");
                        var bId = bounty.Id;
                        cancelBtn.Click += (_, _) => _chatActions.SendChatToServer("#guild cancel " + bId, string.Empty, ChatType.Command);
                        bountyRow.Widgets.Add(cancelBtn);
                        bountyRow.Proportions.Add(new Proportion(ProportionType.Auto));
                    }

                    _tabContent.Widgets.Add(bountyRow);

                    // Status line
                    string statusText;
                    Color statusColor;
                    if (bounty.Status == CustomBountyStatus.Open)
                    {
                        statusText = $"  Posted by {bounty.Poster}";
                        statusColor = SecondaryTextColor;
                    }
                    else
                    {
                        statusText = $"  {bounty.AcceptedBy} delivering";
                        statusColor = GoldColor;
                    }
                    _tabContent.Widgets.Add(new Label
                    {
                        Text = statusText,
                        Font = _fontProvider.Small,
                        TextColor = statusColor,
                    });
                }
            }
        }

        // ────────────────────────────── Perks Tab ──────────────────────────────

        private void BuildPerksContent()
        {
            AddSectionHeader("Guild Perks");

            if (_guildPerks.Count == 0)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "Loading perk data...",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                });
                return;
            }

            var guildLevel = _guildInfo?.Level ?? 0;
            var guildBank = _guildInfo?.Bank ?? 0;

            foreach (var perk in _guildPerks)
            {
                var perkRow = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

                string status;
                Color statusColor;
                if (perk.IsUnlocked)
                {
                    status = "\u2713";
                    statusColor = SuccessColor;
                }
                else if (guildLevel >= perk.RequiredLevel)
                {
                    status = "\u25C6";
                    statusColor = GoldColor;
                }
                else
                {
                    status = "\u2717";
                    statusColor = DangerColor;
                }

                perkRow.Widgets.Add(new Label
                {
                    Text = status + " " + perk.DisplayName,
                    Font = _fontProvider.Normal,
                    TextColor = perk.IsUnlocked ? Color.White : SecondaryTextColor,
                });
                perkRow.Proportions.Add(new Proportion(ProportionType.Fill));

                // Unlock button (rank-aware: only if level is sufficient and not yet unlocked)
                if (!perk.IsUnlocked && guildLevel >= perk.RequiredLevel)
                {
                    var unlockBtn = CreateSmallButton("Unlock");
                    var perkIdx = perk.PerkIndex;
                    unlockBtn.Click += (_, _) => _chatActions.SendChatToServer("#guild upgrade " + perkIdx, string.Empty, ChatType.Command);
                    perkRow.Widgets.Add(unlockBtn);
                    perkRow.Proportions.Add(new Proportion(ProportionType.Auto));
                }

                _tabContent.Widgets.Add(perkRow);

                // Detail line
                string detail;
                if (perk.IsUnlocked)
                {
                    detail = "Unlocked";
                }
                else if (guildLevel >= perk.RequiredLevel)
                {
                    detail = "Cost: " + perk.GoldCost.ToString("N0") + "g";
                    if (guildBank < perk.GoldCost)
                        detail += " (need more gold)";
                }
                else
                {
                    detail = "Requires Lv." + perk.RequiredLevel + ", " + perk.GoldCost.ToString("N0") + "g";
                }

                _tabContent.Widgets.Add(new Label
                {
                    Text = "  " + detail,
                    Font = _fontProvider.Small,
                    TextColor = SecondaryTextColor,
                });
            }
        }

        // ────────────────────────────── Buffs Tab ──────────────────────────────

        private void BuildBuffsContent()
        {
            AddSectionHeader("Guild Buffs");

            if (_guildBuffs.Count == 0)
            {
                _tabContent.Widgets.Add(new Label
                {
                    Text = "Loading buff data...",
                    Font = _fontProvider.Normal,
                    TextColor = SecondaryTextColor,
                });
                return;
            }

            foreach (var buff in _guildBuffs)
            {
                var buffRow = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

                string status;
                Color nameColor;
                if (buff.IsActive)
                {
                    status = "[ACTIVE]";
                    nameColor = SuccessColor;
                }
                else if (buff.IsUnlocked)
                {
                    status = "[Ready]";
                    nameColor = GoldColor;
                }
                else
                {
                    status = "[Locked]";
                    nameColor = DangerColor;
                }

                buffRow.Widgets.Add(new Label
                {
                    Text = buff.DisplayName + " " + status,
                    Font = _fontProvider.Normal,
                    TextColor = nameColor,
                });
                buffRow.Proportions.Add(new Proportion(ProportionType.Fill));

                // Toggle button for unlocked buffs (rank-aware)
                if (buff.IsUnlocked)
                {
                    var label = buff.IsActive ? "Deactivate" : "Activate";
                    var toggleBtn = CreateSmallButton(label);
                    var buffName = buff.DisplayName;
                    var isActive = buff.IsActive;
                    toggleBtn.Click += (_, _) =>
                    {
                        var cmd = isActive
                            ? "#guild buffs deactivate " + buffName
                            : "#guild buffs activate " + buffName;
                        _chatActions.SendChatToServer(cmd, string.Empty, ChatType.Command);
                    };
                    buffRow.Widgets.Add(toggleBtn);
                    buffRow.Proportions.Add(new Proportion(ProportionType.Auto));
                }

                _tabContent.Widgets.Add(buffRow);

                // Stats description
                if (!string.IsNullOrEmpty(buff.StatsDescription))
                {
                    _tabContent.Widgets.Add(new Label
                    {
                        Text = "  " + buff.StatsDescription,
                        Font = _fontProvider.Small,
                        TextColor = HighlightColor,
                    });
                }

                // Upkeep
                _tabContent.Widgets.Add(new Label
                {
                    Text = "  Upkeep: " + buff.UpkeepPoints + "pts + " + buff.UpkeepGold.ToString("N0") + "g/day",
                    Font = _fontProvider.Small,
                    TextColor = SecondaryTextColor,
                });
            }
        }

        // ────────────────────────────── Action Bar ──────────────────────────────

        private void RebuildActionBar()
        {
            _actionBar.Widgets.Clear();

            switch (_activeTab)
            {
                case GuildTab.Overview:
                    var donateBtn = new Button { Content = new Label { Text = "Donate Gold", Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center }, Width = 90, Height = 22 };
                    donateBtn.Click += (_, _) => ShowDonateDialog();
                    _actionBar.Widgets.Add(donateBtn);

                    var storageBtn = new Button { Content = new Label { Text = "Storage", Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center }, Width = 70, Height = 22 };
                    storageBtn.Click += (_, _) =>
                    {
                        _lockerDataRepository.Context = LockerContext.GuildStorage;
                        _chatActions.SendChatToServer("#guild storage", string.Empty, ChatType.Command);
                    };
                    _actionBar.Widgets.Add(storageBtn);

                    var inboxBtn = new Button { Content = new Label { Text = "Inbox", Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center }, Width = 60, Height = 22 };
                    inboxBtn.Click += (_, _) =>
                    {
                        _lockerDataRepository.Context = LockerContext.DeliveryInbox;
                        _chatActions.SendChatToServer("#guild inbox", string.Empty, ChatType.Command);
                    };
                    _actionBar.Widgets.Add(inboxBtn);
                    break;

                case GuildTab.Bounties:
                    var refreshBtn = new Button { Content = new Label { Text = "Refresh", Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center }, Width = 80, Height = 22 };
                    refreshBtn.Click += (_, _) => _questActions.RequestQuestHistory(QuestPage.Progress);
                    _actionBar.Widgets.Add(refreshBtn);

                    var postBtn = new Button { Content = new Label { Text = "Post Request", Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center }, Width = 95, Height = 22 };
                    postBtn.Click += (_, _) => ShowPostBountyDialog();
                    _actionBar.Widgets.Add(postBtn);
                    break;
            }
        }

        // ────────────────────────────── Dialogs ──────────────────────────────

        private void ShowDonateDialog()
        {
            var dlg = _textInputDialogFactory.Create("Enter gold amount to donate (max 50,000/day):", maxInputChars: 6);
            dlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var text = dlg.ResponseText?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.All(char.IsDigit) && int.TryParse(text, out var amount) && amount > 0)
                    {
                        _chatActions.SendChatToServer("#guild donate " + amount, string.Empty, ChatType.Command);
                    }
                }
            };
            dlg.Initialize();
            dlg.Show();
        }

        private void ShowPostBountyDialog()
        {
            var dlg = _textMultiInputDialogFactory.Create(
                "Post Bounty Request",
                "Enter the item name and amount you need:",
                TextMultiInputDialog.DialogSize.Two,
                new TextMultiInputDialog.InputInfo("Item Name:", MaxChars: 24),
                new TextMultiInputDialog.InputInfo("Amount:", MaxChars: 6, InputRestriction: TextMultiInputDialog.InputInfo.InputRestrict.Numeric));

            dlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var responses = dlg.Responses;
                    var itemName = responses[0]?.Trim();
                    var amountText = responses[1]?.Trim();
                    if (!string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(amountText) &&
                        int.TryParse(amountText, out var amount) && amount > 0)
                    {
                        _chatActions.SendChatToServer(
                            $"#guild post {itemName} {amount}",
                            string.Empty,
                            ChatType.Command);
                    }
                }
            };
            dlg.Initialize();
            dlg.Show();
        }

        // ────────────────────────────── Helpers ──────────────────────────────

        private void AddStatRow(string label, string value, Color valueColor)
        {
            var row = new HorizontalStackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            row.Widgets.Add(new Label { Text = label, Font = _fontProvider.Normal, TextColor = SecondaryTextColor });
            row.Proportions.Add(new Proportion(ProportionType.Fill));
            row.Widgets.Add(new Label { Text = value, Font = _fontProvider.Normal, TextColor = valueColor });
            row.Proportions.Add(new Proportion(ProportionType.Auto));
            _tabContent.Widgets.Add(row);
        }

        private void AddSectionHeader(string text)
        {
            _tabContent.Widgets.Add(new Label
            {
                Text = text,
                Font = _fontProvider.Normal,
                TextColor = HighlightColor,
                Padding = new Thickness(0, 4, 0, 2),
            });
        }

        private void AddDivider()
        {
            _tabContent.Widgets.Add(new HorizontalSeparator
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 4),
            });
        }

        private Button CreateSmallButton(string text)
        {
            return new Button
            {
                Content = new Label { Text = text, Font = _fontProvider.Small, HorizontalAlignment = HorizontalAlignment.Center },
                Width = 60,
                Height = 18,
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _window != null)
            {
                _uiManager.Desktop.Widgets.Remove(_window);
            }
            base.Dispose(disposing);
        }
    }
}
