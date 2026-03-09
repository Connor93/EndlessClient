using System.Collections.Generic;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Party;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Party panel. When solo, shows detailed character stats (HP/TP/SP bars
    /// with numbers). In a party, shows each member with a labeled HP bar and level.
    /// </summary>
    public class MyraPartyPanel : MyraHudPanelBase
    {
        private readonly IPartyActions _partyActions;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IMyraFontProvider _fontProvider;

        private VerticalStackPanel _memberList;
        private TextButton _leaveBtn;
        private HashSet<PartyMember> _cachedMembers = new();
        private bool _needsRebuild = true;
        private CharacterStats _lastSoloStats;

        // Colors
        private static readonly Color HpGreen = new(0x22, 0xAA, 0x44);
        private static readonly Color TpBlue = new(0x33, 0x88, 0xDD);
        private static readonly Color SpGold = new(0xD4, 0xA5, 0x37);
        private static readonly Color BarBackground = new(0x33, 0x33, 0x3A);
        private static readonly Color LabelDim = new(0x88, 0x88, 0x99);
        private static readonly Color LeaderGold = new(0xD4, 0xA5, 0x37);

        public MyraPartyPanel(Game game,
                              IMyraUIManager uiManager,
                              IMyraFontProvider fontProvider,
                              IPartyActions partyActions,
                              IPartyDataProvider partyDataProvider,
                              ICharacterProvider characterProvider)
            : base(game, uiManager, "Party")
        {
            _fontProvider = fontProvider;
            _partyActions = partyActions;
            _partyDataProvider = partyDataProvider;
            _characterProvider = characterProvider;
        }

        public override void Initialize()
        {
            Window.Width = 240;
            Window.Height = 200;
            Window.TitleFont = _fontProvider.Large;

            var root = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Height = 140,
            };

            _memberList = new VerticalStackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            scrollViewer.Content = _memberList;
            root.Widgets.Add(scrollViewer);

            // Leave button (hidden when solo)
            _leaveBtn = new TextButton
            {
                Text = "Leave Party",
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 110,
                Visible = false,
            };
            _leaveBtn.Click += (_, _) =>
            {
                _partyActions.RemovePartyMember(_characterProvider.MainCharacter.ID);
            };
            root.Widgets.Add(_leaveBtn);

            Window.Content = root;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (Window.Visible)
            {
                var partyChanged = _needsRebuild || !_cachedMembers.SetEquals(_partyDataProvider.Members);

                // For solo view, also rebuild when stats change (HP/TP)
                if (!partyChanged && _cachedMembers.Count == 0)
                {
                    var currentStats = _characterProvider.MainCharacter.Stats;
                    if (_lastSoloStats != currentStats)
                        partyChanged = true;
                }

                if (partyChanged)
                {
                    _cachedMembers = _partyDataProvider.Members.ToHashSet();
                    _lastSoloStats = _characterProvider.MainCharacter.Stats;
                    _needsRebuild = false;
                    RebuildMemberList();
                }
            }

            base.Update(gameTime);
        }

        private void RebuildMemberList()
        {
            _memberList.Widgets.Clear();

            if (_cachedMembers.Count == 0)
            {
                BuildSoloView();
                _leaveBtn.Visible = false;
                return;
            }

            _leaveBtn.Visible = true;
            var isLeader = _cachedMembers.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);

            foreach (var member in _cachedMembers.OrderByDescending(m => m.IsLeader).ThenBy(m => m.Name))
            {
                _memberList.Widgets.Add(CreatePartyMemberRow(member, isLeader));
            }
        }

        /// <summary>
        /// Solo view: show detailed character stats with HP, TP, SP bars and numbers.
        /// </summary>
        private void BuildSoloView()
        {
            var stats = _characterProvider.MainCharacter.Stats;
            var name = _characterProvider.MainCharacter.Name;
            var level = stats[CharacterStat.Level];

            // Name + Level header
            var header = new HorizontalStackPanel { Spacing = 6 };
            header.Widgets.Add(new Label
            {
                Text = name,
                Font = _fontProvider.Normal,
                TextColor = Color.White,
            });
            header.Widgets.Add(new Label
            {
                Text = $"Lv.{level}",
                Font = _fontProvider.Small,
                TextColor = LabelDim,
            });
            _memberList.Widgets.Add(header);

            _memberList.Widgets.Add(new HorizontalSeparator());

            // HP bar
            var hp = stats[CharacterStat.HP];
            var maxHp = stats[CharacterStat.MaxHP];
            _memberList.Widgets.Add(CreateStatBar("HP", hp, maxHp, HpGreen));

            // TP bar
            var tp = stats[CharacterStat.TP];
            var maxTp = stats[CharacterStat.MaxTP];
            _memberList.Widgets.Add(CreateStatBar("TP", tp, maxTp, TpBlue));

            // SP (Stat Points) + Skill Points
            var sp = stats[CharacterStat.StatPoints];
            var skp = stats[CharacterStat.SkillPoints];
            var pointsRow = new HorizontalStackPanel { Spacing = 12 };
            pointsRow.Widgets.Add(CreatePointLabel("Stat Pts", sp, SpGold));
            pointsRow.Widgets.Add(CreatePointLabel("Skill Pts", skp, LabelDim));
            _memberList.Widgets.Add(pointsRow);

            // Weight
            var weight = stats[CharacterStat.Weight];
            var maxWeight = stats[CharacterStat.MaxWeight];
            _memberList.Widgets.Add(CreateStatBar("Wt", weight, maxWeight, LabelDim));
        }

        /// <summary>
        /// Creates a labeled bar: "HP  350 / 500  (70%)"
        /// </summary>
        private Widget CreateStatBar(string label, int current, int max, Color barColor)
        {
            var container = new VerticalStackPanel
            {
                Spacing = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Label row: "HP  350 / 500"
            var labelRow = new HorizontalStackPanel { Spacing = 4 };
            labelRow.Widgets.Add(new Label
            {
                Text = label,
                Font = _fontProvider.Small,
                TextColor = barColor,
                Width = 24,
            });
            labelRow.Widgets.Add(new Label
            {
                Text = $"{current} / {max}",
                Font = _fontProvider.Small,
                TextColor = Color.White,
            });

            var pct = max > 0 ? (current * 100 / max) : 0;
            labelRow.Widgets.Add(new Label
            {
                Text = $"({pct}%)",
                Font = _fontProvider.Small,
                TextColor = LabelDim,
            });

            container.Widgets.Add(labelRow);

            // Bar
            var barValue = max > 0 ? (float)current / max * 100f : 0f;
            var bar = new HorizontalProgressBar
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 10,
                Minimum = 0,
                Maximum = 100,
                Value = barValue,
            };
            container.Widgets.Add(bar);

            return container;
        }

        /// <summary>
        /// Simple "Stat Pts: 5" label pair.
        /// </summary>
        private Widget CreatePointLabel(string label, int value, Color color)
        {
            var row = new HorizontalStackPanel { Spacing = 4 };
            row.Widgets.Add(new Label
            {
                Text = $"{label}:",
                Font = _fontProvider.Small,
                TextColor = LabelDim,
            });
            row.Widgets.Add(new Label
            {
                Text = $"{value}",
                Font = _fontProvider.Small,
                TextColor = color,
            });
            return row;
        }

        /// <summary>
        /// Party member row: name, level, HP bar with percentage.
        /// </summary>
        private Widget CreatePartyMemberRow(PartyMember member, bool viewerIsLeader)
        {
            var row = new VerticalStackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(2),
            };

            // Name row
            var nameRow = new HorizontalStackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            nameRow.Widgets.Add(new Label
            {
                Text = (member.IsLeader ? "★ " : "") + member.Name,
                Font = _fontProvider.Normal,
                TextColor = member.IsLeader ? LeaderGold : Color.White,
            });

            nameRow.Widgets.Add(new Label
            {
                Text = $"Lv.{member.Level}",
                Font = _fontProvider.Small,
                TextColor = LabelDim,
            });

            // Remove button (leader only, not self)
            if (viewerIsLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
            {
                var removeBtn = new TextButton
                {
                    Text = "×",
                    Width = 20,
                    Height = 18,
                };
                var memberId = member.CharacterID;
                removeBtn.Click += (_, _) => _partyActions.RemovePartyMember(memberId);
                nameRow.Widgets.Add(removeBtn);
            }

            row.Widgets.Add(nameRow);

            // HP bar with percentage label
            var hpRow = new HorizontalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            hpRow.Widgets.Add(new Label
            {
                Text = "HP",
                Font = _fontProvider.Small,
                TextColor = HpGreen,
                Width = 24,
            });

            var bar = new HorizontalProgressBar
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 10,
                Minimum = 0,
                Maximum = 100,
                Value = member.PercentHealth,
                Width = 140,
            };
            hpRow.Widgets.Add(bar);

            hpRow.Widgets.Add(new Label
            {
                Text = $"{member.PercentHealth}%",
                Font = _fontProvider.Small,
                TextColor = Color.White,
            });

            row.Widgets.Add(hpRow);

            return row;
        }
    }
}
