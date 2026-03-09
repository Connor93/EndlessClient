using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Myra-based grind session tracker. Shows live session stats with start/pause/reset controls.
    /// </summary>
    public class MyraExpTrackerWindow : DrawableGameComponent, IExpTrackerWindow
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterSessionRepository _sessionRepository;
        private readonly ICharacterSessionProvider _sessionProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly ICharacterInventoryProvider _inventoryProvider;

        private Window _window;
        private VerticalStackPanel _contentPanel;
        private Label _sessionLabel;
        private Label _expHrLabel;
        private Label _toLevelLabel;
        private Label _killsLabel;
        private Label _goldLabel;
        private HorizontalProgressBar _progressBar;
        private Label _progressLabel;
        private TextButton _startBtn;
        private TextButton _pauseBtn;
        private TextButton _resetBtn;

        // Rolling EXP/hr calculation
        private readonly Queue<(DateTime Time, int TotalExp)> _expSamples = new Queue<(DateTime, int)>();
        private const int RollingWindowMinutes = 5;
        private const double RefreshIntervalSeconds = 5.0;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private bool _statsDirty = true;

        public MyraExpTrackerWindow(
            Game game,
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ICharacterProvider characterProvider,
            ICharacterSessionRepository sessionRepository,
            ICharacterSessionProvider sessionProvider,
            IExperienceTableProvider experienceTableProvider,
            ICharacterInventoryProvider inventoryProvider)
            : base(game)
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _characterProvider = characterProvider;
            _sessionRepository = sessionRepository;
            _sessionProvider = sessionProvider;
            _experienceTableProvider = experienceTableProvider;
            _inventoryProvider = inventoryProvider;
        }

        public override void Initialize()
        {
            _window = new Window
            {
                Title = "Grind Tracker",
                TitleFont = _fontProvider.Header,
                Width = 220,
                Height = 170,
                Left = 100,
                Top = 200,
                Visible = false,
                DragDirection = DragDirection.Both,
            };

            _contentPanel = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Control buttons row
            var btnRow = new HorizontalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            _startBtn = new TextButton { Text = "Start", Font = _fontProvider.Normal };
            _pauseBtn = new TextButton { Text = "Pause", Font = _fontProvider.Normal, Visible = false };
            _resetBtn = new TextButton { Text = "Reset", Font = _fontProvider.Normal, Visible = false };

            _startBtn.Click += (_, _) => StartSession();
            _pauseBtn.Click += (_, _) => PauseSession();
            _resetBtn.Click += (_, _) => ResetSession();

            btnRow.Widgets.Add(_startBtn);
            btnRow.Widgets.Add(_pauseBtn);
            btnRow.Widgets.Add(_resetBtn);
            _contentPanel.Widgets.Add(btnRow);

            // Stat labels
            _sessionLabel = CreateStatRow("Session", "--");
            _expHrLabel = CreateStatRow("EXP/hr", "--");
            _toLevelLabel = CreateStatRow("To Level", "--");
            _killsLabel = CreateStatRow("Kills", "--");
            _goldLabel = CreateStatRow("Gold", "--");

            // Progress bar
            _progressBar = new HorizontalProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _progressLabel = new Label
            {
                Text = "0%",
                Font = _fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _contentPanel.Widgets.Add(_progressBar);
            _contentPanel.Widgets.Add(_progressLabel);

            _window.Content = _contentPanel;
            _uiManager.Desktop.Widgets.Add(_window);

            base.Initialize();
        }

        private Label CreateStatRow(string label, string value)
        {
            var row = new HorizontalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            row.Widgets.Add(new Label
            {
                Text = label,
                Font = _fontProvider.Normal,
                TextColor = new Color(0xB0, 0xB0, 0xB0),
            });
            row.Proportions.Add(new Proportion(ProportionType.Fill));

            var valueLabel = new Label
            {
                Text = value,
                Font = _fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            row.Widgets.Add(valueLabel);
            row.Proportions.Add(new Proportion(ProportionType.Auto));

            _contentPanel.Widgets.Add(row);
            return valueLabel;
        }

        public void Toggle()
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible)
            {
                _window.BringToFront();
                _statsDirty = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_window.Visible)
            {
                var isActive = _sessionProvider.GrindSessionActive;
                var isPaused = !isActive && _sessionProvider.GrindSessionPausedElapsed > TimeSpan.Zero;
                var hasStarted = isActive || isPaused;

                // Update button visibility
                _startBtn.Visible = !isActive;
                _pauseBtn.Visible = isActive;
                _resetBtn.Visible = hasStarted;

                // Sample EXP while active
                if (isActive)
                {
                    var now = DateTime.Now;
                    var currentExp = _characterProvider.MainCharacter.Stats[CharacterStat.Experience];

                    if (_expSamples.Count == 0 || _expSamples.Last().TotalExp != currentExp)
                        _expSamples.Enqueue((now, currentExp));

                    var cutoff = now.AddMinutes(-RollingWindowMinutes);
                    while (_expSamples.Count > 1 && _expSamples.Peek().Time < cutoff)
                        _expSamples.Dequeue();

                    if (_statsDirty || (now - _lastRefreshTime).TotalSeconds >= RefreshIntervalSeconds)
                    {
                        RefreshStats();
                        _lastRefreshTime = now;
                        _statsDirty = false;
                    }

                    // Session time updates every frame
                    _sessionLabel.Text = FormatDuration(GetActiveSessionTime());
                }
                else if (isPaused)
                {
                    if (_statsDirty)
                    {
                        RefreshStats();
                        _statsDirty = false;
                    }
                }
            }

            base.Update(gameTime);
        }

        private void RefreshStats()
        {
            var isActive = _sessionProvider.GrindSessionActive;
            var isPaused = !isActive && _sessionProvider.GrindSessionPausedElapsed > TimeSpan.Zero;
            var hasStarted = isActive || isPaused;

            var sessionTime = GetActiveSessionTime();
            var expPerHour = hasStarted ? GetExpPerHour() : 0;
            var totalHours = sessionTime.TotalHours;
            var killCount = _sessionProvider.KillCount;
            var killsPerHour = totalHours > 0.001 ? (int)(killCount / totalHours) : 0;
            var goldDelta = GetCurrentGold() - _sessionProvider.StartingGold;
            var timeToLevel = hasStarted ? GetTimeToLevel() : "--";

            var sessionStr = hasStarted ? FormatDuration(sessionTime) : "Ready";
            if (isPaused) sessionStr += " (paused)";

            _sessionLabel.Text = sessionStr;
            _expHrLabel.Text = hasStarted ? FormatNumber(expPerHour) : "--";
            _toLevelLabel.Text = timeToLevel;
            _killsLabel.Text = hasStarted ? $"{killCount} ({killsPerHour}/hr)" : "--";
            _goldLabel.Text = hasStarted ? $"{(goldDelta >= 0 ? "+" : "")}{FormatNumber(goldDelta)}" : "--";
            _goldLabel.TextColor = goldDelta >= 0 ? new Color(0xD4, 0xA5, 0x37) : new Color(0xE5, 0x73, 0x73);

            var progress = GetLevelProgress();
            _progressBar.Value = (int)(progress * 100);
            _progressLabel.Text = $"{(int)(progress * 100)}%";
        }

        private void StartSession()
        {
            if (_sessionProvider.GrindSessionActive) return;

            if (_sessionProvider.GrindSessionPausedElapsed == TimeSpan.Zero)
            {
                var stats = _characterProvider.MainCharacter.Stats;
                _sessionRepository.StartingExp = stats[CharacterStat.Experience];
                _sessionRepository.StartingGold = GetCurrentGold();
                _sessionRepository.KillCount = 0;
                _expSamples.Clear();
            }

            _sessionRepository.GrindSessionResumeTime = DateTime.Now;
            _sessionRepository.GrindSessionActive = true;
            _statsDirty = true;
        }

        private void PauseSession()
        {
            if (!_sessionProvider.GrindSessionActive) return;

            _sessionRepository.GrindSessionPausedElapsed += DateTime.Now - _sessionProvider.GrindSessionResumeTime;
            _sessionRepository.GrindSessionActive = false;
            _statsDirty = true;
        }

        private void ResetSession()
        {
            _sessionRepository.GrindSessionActive = false;
            _sessionRepository.GrindSessionPausedElapsed = TimeSpan.Zero;
            _sessionRepository.GrindSessionResumeTime = DateTime.Now;
            _sessionRepository.KillCount = 0;

            var stats = _characterProvider.MainCharacter.Stats;
            _sessionRepository.StartingExp = stats[CharacterStat.Experience];
            _sessionRepository.StartingGold = GetCurrentGold();
            _expSamples.Clear();
            _statsDirty = true;
        }

        private int GetCurrentGold()
        {
            var goldItem = _inventoryProvider.ItemInventory.FirstOrDefault(i => i.ItemID == 1);
            return goldItem?.Amount ?? 0;
        }

        private TimeSpan GetActiveSessionTime()
        {
            var elapsed = _sessionProvider.GrindSessionPausedElapsed;
            if (_sessionProvider.GrindSessionActive)
                elapsed += DateTime.Now - _sessionProvider.GrindSessionResumeTime;
            return elapsed;
        }

        private int GetExpPerHour()
        {
            var currentExp = _characterProvider.MainCharacter.Stats[CharacterStat.Experience];
            var sessionExp = currentExp - _sessionProvider.StartingExp;

            if (_expSamples.Count >= 2)
            {
                var oldest = _expSamples.Peek();
                var windowSpan = (DateTime.Now - oldest.Time).TotalHours;
                if (windowSpan > 0.001)
                {
                    var windowExp = currentExp - oldest.TotalExp;
                    return (int)(windowExp / windowSpan);
                }
            }

            var totalHours = GetActiveSessionTime().TotalHours;
            return totalHours > 0.001 ? (int)(sessionExp / totalHours) : 0;
        }

        private string GetTimeToLevel()
        {
            var stats = _characterProvider.MainCharacter.Stats;
            var level = stats[CharacterStat.Level];
            var currentExp = stats[CharacterStat.Experience];

            if (level + 1 >= _experienceTableProvider.ExperienceByLevel.Count)
                return "Max";

            var nextLevelExp = _experienceTableProvider.ExperienceByLevel[level + 1];
            var expNeeded = nextLevelExp - currentExp;
            if (expNeeded <= 0) return "Ready!";

            var expPerHour = GetExpPerHour();
            if (expPerHour <= 0) return "--";

            var hoursRemaining = (double)expNeeded / expPerHour;
            var eta = TimeSpan.FromHours(hoursRemaining);

            if (eta.TotalDays >= 1) return $"{(int)eta.TotalDays}d {eta.Hours}h";
            if (eta.TotalHours >= 1) return $"{(int)eta.TotalHours}h {eta.Minutes}m";
            return $"{eta.Minutes}m {eta.Seconds}s";
        }

        private float GetLevelProgress()
        {
            var stats = _characterProvider.MainCharacter.Stats;
            var level = stats[CharacterStat.Level];
            var currentExp = stats[CharacterStat.Experience];

            if (level + 1 >= _experienceTableProvider.ExperienceByLevel.Count) return 1f;

            var thisLevelExp = _experienceTableProvider.ExperienceByLevel[level];
            var nextLevelExp = _experienceTableProvider.ExperienceByLevel[level + 1];
            var range = nextLevelExp - thisLevelExp;

            return range > 0 ? Math.Min(1f, (float)(currentExp - thisLevelExp) / range) : 0f;
        }

        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }

        private string FormatNumber(int value)
        {
            if (value >= 1_000_000) return $"{value / 1_000_000.0:F1}M";
            if (value >= 10_000) return $"{value / 1_000.0:F1}K";
            return $"{value:N0}";
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
