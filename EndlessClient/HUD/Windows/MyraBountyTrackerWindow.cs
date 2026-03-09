using System;
using System.Collections.Generic;
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
    /// Myra-based bounty tracker window. Shows guild bounty progress with polling.
    /// </summary>
    public class MyraBountyTrackerWindow : DrawableGameComponent, IBountyTrackerWindow
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;

        private Window _window;
        private VerticalStackPanel _listPanel;
        private IReadOnlyList<BountyProgressData> _bounties = new List<BountyProgressData>();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        private const double PollIntervalSeconds = 2.0;
        private const int MaxBounties = 10;

        public MyraBountyTrackerWindow(
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
                Title = "Guild Bounties",
                TitleFont = _fontProvider.Header,
                Width = 220,
                Height = 140,
                Left = 100,
                Top = 120,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            _listPanel = new VerticalStackPanel
            {
                Spacing = 1,
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _listPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            _window.Content = scrollViewer;
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

                var currentBounties = _bountyDataProvider.Bounties;
                if (currentBounties != _bounties)
                {
                    _bounties = currentBounties;
                    RebuildList();
                }
            }
            else
            {
                _pollStopwatch.Stop();
            }

            base.Update(gameTime);
        }

        private void RebuildList()
        {
            _listPanel.Widgets.Clear();

            if (_bounties.Count == 0)
            {
                _listPanel.Widgets.Add(new Label
                {
                    Text = "No guild bounties",
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            for (int i = 0; i < Math.Min(_bounties.Count, MaxBounties); i++)
            {
                var bounty = _bounties[i];
                var isComplete = bounty.Status == BountyStatus.Complete;

                var row = new HorizontalStackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                row.Widgets.Add(new Label
                {
                    Text = bounty.Name,
                    Font = _fontProvider.Normal,
                    TextColor = isComplete ? new Color(0x66, 0xBB, 0x6A) : Color.White,
                });
                row.Proportions.Add(new Proportion(ProportionType.Fill));

                var progressText = isComplete ? "✓" : $"{bounty.Progress}/{bounty.Target}";
                row.Widgets.Add(new Label
                {
                    Text = progressText,
                    Font = _fontProvider.Normal,
                    TextColor = isComplete ? new Color(0x66, 0xBB, 0x6A) : new Color(0xB0, 0xB0, 0xB0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                });
                row.Proportions.Add(new Proportion(ProportionType.Auto));

                _listPanel.Widgets.Add(row);
            }
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
