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
    /// Myra-based quest tracker window. Shows tracked quest progress with polling.
    /// Receives tracked quests from QuestWindow via SetTrackedQuests / UpdateQuestProgress.
    /// </summary>
    public class MyraQuestTrackerWindow : DrawableGameComponent, IQuestTrackerWindow
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IQuestActions _questActions;

        private Window _window;
        private VerticalStackPanel _listPanel;
        private HashSet<string> _trackedQuestNames = new HashSet<string>();
        private IReadOnlyList<QuestProgressData> _questProgress = new List<QuestProgressData>();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        private const double PollIntervalSeconds = 2.0;
        private const int MaxTrackedQuests = 5;

        public new bool Visible
        {
            get => _window?.Visible ?? false;
            set { if (_window != null) _window.Visible = value; }
        }

        public MyraQuestTrackerWindow(
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
                Title = "Quest Tracker",
                TitleFont = _fontProvider.Header,
                Width = 180,
                Height = 120,
                Left = 100,
                Top = 50,
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

        public void SetTrackedQuests(HashSet<string> trackedNames)
        {
            _trackedQuestNames = trackedNames ?? new HashSet<string>();
            RebuildList();
        }

        public void UpdateQuestProgress(IReadOnlyList<QuestProgressData> progress)
        {
            _questProgress = progress ?? new List<QuestProgressData>();
            RebuildList();
        }

        public override void Update(GameTime gameTime)
        {
            if (_window.Visible && _trackedQuestNames.Count > 0)
            {
                if (!_pollStopwatch.IsRunning)
                    _pollStopwatch.Start();

                if (_pollStopwatch.Elapsed.TotalSeconds >= PollIntervalSeconds)
                {
                    _pollStopwatch.Restart();
                    _questActions.RequestQuestHistory(QuestPage.Progress);
                }

                if (_questDataProvider.QuestProgress != _questProgress)
                {
                    _questProgress = _questDataProvider.QuestProgress;
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
            var questIndex = 0;

            foreach (var quest in _questProgress)
            {
                if (!_trackedQuestNames.Contains(quest.Name))
                    continue;

                if (questIndex >= MaxTrackedQuests)
                    break;

                var isComplete = quest.Progress >= quest.Target;

                var row = new HorizontalStackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                var name = quest.Name.Length > 18 ? quest.Name.Substring(0, 15) + "..." : quest.Name;
                row.Widgets.Add(new Label
                {
                    Text = name,
                    Font = _fontProvider.Normal,
                });
                row.Proportions.Add(new Proportion(ProportionType.Fill));

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
                questIndex++;
            }

            if (questIndex == 0)
            {
                _listPanel.Widgets.Add(new Label
                {
                    Text = "No quests tracked",
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
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
