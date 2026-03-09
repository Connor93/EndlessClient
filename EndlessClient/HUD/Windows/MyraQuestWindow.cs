using System.Collections.Generic;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact.Quest;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Myra-based quest window showing quest progress and history with tracker checkbox support.
    /// </summary>
    public class MyraQuestWindow : DrawableGameComponent
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IQuestActions _questActions;

        private Window _window;
        private VerticalStackPanel _listPanel;
        private TextButton _progressBtn;
        private TextButton _historyBtn;

        private QuestPage _currentPage = QuestPage.Progress;
        private IReadOnlyList<QuestProgressData> _cachedProgress;
        private IReadOnlyList<string> _cachedHistory;

        // Tracker linkage
        private IQuestTrackerWindow _questTrackerWindow;
        private bool _questTrackerEnabled;
        private readonly HashSet<string> _trackedQuestNames = new HashSet<string>();

        public MyraQuestWindow(
            Game game,
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IQuestDataProvider questDataProvider,
            IQuestActions questActions)
            : base(game)
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _questDataProvider = questDataProvider;
            _questActions = questActions;
        }

        public override void Initialize()
        {
            _window = new Window
            {
                Title = "Quest Log",
                TitleFont = _fontProvider.Header,
                Width = 280,
                Height = 250,
                Left = 200,
                Top = 100,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Tab buttons
            var tabRow = new HorizontalStackPanel { Spacing = 4 };
            _progressBtn = new TextButton { Text = "Progress", Font = _fontProvider.Normal };
            _historyBtn = new TextButton { Text = "History", Font = _fontProvider.Normal };

            _progressBtn.Click += (_, _) => SwitchToPage(QuestPage.Progress);
            _historyBtn.Click += (_, _) => SwitchToPage(QuestPage.History);

            tabRow.Widgets.Add(_progressBtn);
            tabRow.Widgets.Add(_historyBtn);
            mainPanel.Widgets.Add(tabRow);

            // Tracker toggle checkbox (in progress tab)
            var trackerRow = new HorizontalStackPanel { Spacing = 4 };
            var trackerCheckbox = new CheckButton();
            trackerRow.Widgets.Add(trackerCheckbox);
            trackerRow.Widgets.Add(new Label { Text = "Enable Tracker", Font = _fontProvider.Normal });
            trackerCheckbox.Click += (_, _) =>
            {
                _questTrackerEnabled = trackerCheckbox.IsChecked;
                if (_questTrackerWindow != null)
                {
                    _questTrackerWindow.Visible = _questTrackerEnabled;
                    if (_questTrackerEnabled)
                        UpdateTrackerWindow();
                }
            };
            mainPanel.Widgets.Add(trackerRow);

            // Quest list
            _listPanel = new VerticalStackPanel
            {
                Spacing = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _listPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            mainPanel.Widgets.Add(scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));  // tabs
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));  // tracker toggle
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));  // scroll list

            _window.Content = mainPanel;
            _uiManager.Desktop.Widgets.Add(_window);

            base.Initialize();
        }

        public void SetQuestTrackerWindow(IQuestTrackerWindow trackerWindow)
        {
            _questTrackerWindow = trackerWindow;
        }

        public void Toggle()
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible)
            {
                _window.BringToFront();
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _questActions.RequestQuestHistory(QuestPage.History);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_window.Visible)
            {
                var currentProgress = _questDataProvider.QuestProgress;
                if (currentProgress != _cachedProgress)
                {
                    _cachedProgress = currentProgress;
                    if (_currentPage == QuestPage.Progress)
                        RebuildList();
                }

                if (_questTrackerWindow != null && _questTrackerWindow.Visible)
                    _questTrackerWindow.UpdateQuestProgress(_cachedProgress);

                var currentHistory = _questDataProvider.QuestHistory;
                if (currentHistory != _cachedHistory)
                {
                    _cachedHistory = currentHistory;
                    if (_currentPage == QuestPage.History)
                        RebuildList();
                }
            }

            base.Update(gameTime);
        }

        private void SwitchToPage(QuestPage page)
        {
            _currentPage = page;
            RebuildList();
        }

        private void RebuildList()
        {
            _listPanel.Widgets.Clear();

            if (_currentPage == QuestPage.Progress)
                RebuildProgressList();
            else
                RebuildHistoryList();
        }

        private void RebuildProgressList()
        {
            if (_cachedProgress == null || _cachedProgress.Count == 0)
            {
                _listPanel.Widgets.Add(new Label
                {
                    Text = "No active quests",
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            foreach (var quest in _cachedProgress)
            {
                var isComplete = quest.Progress >= quest.Target;
                var row = new HorizontalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };

                // Tracker checkbox (only if tracker enabled)
                if (_questTrackerEnabled)
                {
                    var isTracked = _trackedQuestNames.Contains(quest.Name);
                    var cb = new CheckButton { IsChecked = isTracked };
                    var capturedName = quest.Name;
                    cb.Click += (_, _) =>
                    {
                        if (cb.IsChecked)
                            _trackedQuestNames.Add(capturedName);
                        else
                            _trackedQuestNames.Remove(capturedName);
                        UpdateTrackerWindow();
                    };
                    row.Widgets.Add(cb);
                }

                // Quest name
                var name = quest.Name.Length > 20 ? quest.Name.Substring(0, 17) + "..." : quest.Name;
                row.Widgets.Add(new Label
                {
                    Text = name,
                    Font = _fontProvider.Normal,
                    TextColor = isComplete ? new Color(0x66, 0xBB, 0x6A) : Color.White,
                });
                row.Proportions.Add(new Proportion(ProportionType.Fill));

                // Progress
                var progressText = quest.Target > 0 ? $"{quest.Progress}/{quest.Target}" : "done";
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

        private void RebuildHistoryList()
        {
            if (_cachedHistory == null || _cachedHistory.Count == 0)
            {
                _listPanel.Widgets.Add(new Label
                {
                    Text = "No completed quests",
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            foreach (var questName in _cachedHistory)
            {
                _listPanel.Widgets.Add(new Label
                {
                    Text = questName,
                    Font = _fontProvider.Normal,
                    TextColor = new Color(0xB0, 0xB0, 0xB0),
                });
            }
        }

        private void UpdateTrackerWindow()
        {
            if (_questTrackerWindow != null)
            {
                _questTrackerWindow.SetTrackedQuests(_trackedQuestNames);
                _questTrackerWindow.UpdateQuestProgress(_cachedProgress);
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
