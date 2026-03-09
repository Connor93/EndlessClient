using System;
using System.Diagnostics;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact.Quest;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Myra-based guild info window. Shows guild name/tag, level, EXP bar, points, contribution, and buffs.
    /// Polls independently for data updates.
    /// </summary>
    public class MyraGuildInfoWindow : DrawableGameComponent, IGuildInfoWindow
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;

        private Window _window;
        private VerticalStackPanel _contentPanel;
        private GuildInfoData _guildInfo;
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        private const double PollIntervalSeconds = 2.0;

        public MyraGuildInfoWindow(
            Game game,
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IBountyDataProvider bountyDataProvider,
            IQuestActions questActions)
            : base(game)
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _bountyDataProvider = bountyDataProvider;
            _questActions = questActions;
        }

        public override void Initialize()
        {
            _window = new Window
            {
                Title = "Guild Info",
                TitleFont = _fontProvider.Header,
                Width = 200,
                Height = 160,
                Left = 100,
                Top = 280,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            _contentPanel = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _window.Content = _contentPanel;
            _uiManager.Desktop.Widgets.Add(_window);

            base.Initialize();
        }

        public void Toggle()
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible)
            {
                _window.BringToFront();
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _pollStopwatch.Restart();
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

                var currentInfo = _bountyDataProvider.GuildInfo;
                if (currentInfo != _guildInfo)
                {
                    _guildInfo = currentInfo;
                    RebuildContent();
                }
            }
            else
            {
                _pollStopwatch.Stop();
            }

            base.Update(gameTime);
        }

        private void RebuildContent()
        {
            _contentPanel.Widgets.Clear();

            if (_guildInfo == null)
            {
                _contentPanel.Widgets.Add(new Label
                {
                    Text = "No guild data",
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            // Guild Name [TAG]
            var title = _guildInfo.GuildName;
            if (title.Length > 18) title = title.Substring(0, 16) + "..";
            title += " [" + _guildInfo.GuildTag + "]";
            _contentPanel.Widgets.Add(new Label
            {
                Text = title,
                Font = _fontProvider.Header,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            // Level
            AddRow("Level", _guildInfo.Level.ToString());

            // EXP progress bar
            var expMax = _guildInfo.Exp + _guildInfo.ExpToNext;
            var expPct = expMax > 0 ? (int)(100.0 * _guildInfo.Exp / expMax) : 100;
            var expText = _guildInfo.ExpToNext > 0 ? $"{_guildInfo.Exp}/{expMax}" : "MAX";

            var bar = new HorizontalProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = expPct,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _contentPanel.Widgets.Add(bar);
            _contentPanel.Widgets.Add(new Label
            {
                Text = expText,
                Font = _fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(0xB0, 0xB0, 0xB0),
            });

            // Points + Contribution
            AddRow("Points", _guildInfo.Points.ToString());
            AddRow("My Contrib", _guildInfo.Contribution.ToString(), new Color(0xB0, 0xB0, 0xB0));

            // Active Buffs
            if (!string.IsNullOrEmpty(_guildInfo.ActiveBuffs))
            {
                var buffs = _guildInfo.ActiveBuffs.Split(',');
                var buffRow = new HorizontalStackPanel { Spacing = 4 };
                buffRow.Widgets.Add(new Label { Text = "Buffs:", Font = _fontProvider.Normal, TextColor = new Color(0xB0, 0xB0, 0xB0) });

                foreach (var buff in buffs)
                {
                    var trimmed = buff.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var label = trimmed.Contains("tier1") ? "T1"
                              : trimmed.Contains("tier2") ? "T2"
                              : trimmed.Contains("tier3") ? "T3"
                              : trimmed;
                    buffRow.Widgets.Add(new Label { Text = label, Font = _fontProvider.Normal, TextColor = new Color(0x66, 0xBB, 0x6A) });
                }

                _contentPanel.Widgets.Add(buffRow);
            }
        }

        private void AddRow(string label, string value, Color? valueColor = null)
        {
            var row = new HorizontalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            row.Widgets.Add(new Label { Text = label, Font = _fontProvider.Normal, TextColor = new Color(0xB0, 0xB0, 0xB0) });
            row.Proportions.Add(new Proportion(ProportionType.Fill));
            row.Widgets.Add(new Label
            {
                Text = value,
                Font = _fontProvider.Normal,
                TextColor = valueColor ?? Color.White,
                HorizontalAlignment = HorizontalAlignment.Right,
            });
            row.Proportions.Add(new Proportion(ProportionType.Auto));
            _contentPanel.Widgets.Add(row);
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
