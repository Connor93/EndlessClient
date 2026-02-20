using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.HUD.Panels;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Compact draggable grind session tracker overlay.
    /// Shows live session stats: duration, EXP/hr, time-to-level, kills, gold, and level progress.
    /// Supports start/pause/reset session controls.
    /// </summary>
    public class CodeDrawnExpTrackerWindow : DraggableHudPanel, IZOrderedWindow
    {
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterSessionRepository _sessionRepository;
        private readonly ICharacterSessionProvider _sessionProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ICharacterInventoryProvider _inventoryProvider;
        private readonly BitmapFont _font;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _labelFont;

        private const int TrackerWidth = 260;
        private const int HeaderHeight = 18;
        private const int RowHeight = 14;
        private const int Padding = 6;
        private const int ProgressBarHeight = 10;
        private const int NumStatRows = 5;

        // Total height: header + 5 stat rows + progress bar row + padding
        private static readonly int TrackerHeight = HeaderHeight + (NumStatRows * RowHeight) + ProgressBarHeight + Padding * 3;

        // Rolling EXP/hr calculation
        private readonly Queue<(DateTime Time, int TotalExp)> _expSamples = new Queue<(DateTime, int)>();
        private const int RollingWindowMinutes = 5;

        // Header button hit areas (set during draw, tested during click)
        private Rectangle _startBtnArea;
        private Rectangle _pauseBtnArea;
        private Rectangle _resetBtnArea;

        // Cached stat snapshot (refreshed every 5 seconds while active, frozen when paused)
        private (string[] Labels, string[] Values, Color[] ValueColors) _cachedStats;
        private float _cachedProgress;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private const double RefreshIntervalSeconds = 5.0;
        private bool _statsDirty = true;

        // Theme colors
        private Color HeaderColor => new Color(_styleProvider.TitleBarBackground, 0.90f);
        private Color HeaderAccent => _styleProvider.TitleBarText;

        public CodeDrawnExpTrackerWindow(
            ICharacterProvider characterProvider,
            ICharacterSessionRepository sessionRepository,
            ICharacterSessionProvider sessionProvider,
            IExperienceTableProvider experienceTableProvider,
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            ICharacterInventoryProvider inventoryProvider)
            : base(true)
        {
            _characterProvider = characterProvider;
            _sessionRepository = sessionRepository;
            _sessionProvider = sessionProvider;
            _experienceTableProvider = experienceTableProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _inventoryProvider = inventoryProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _contentProvider = contentProvider;
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            // Position top-right, below where the bounty tracker typically sits
            DrawArea = new Rectangle(
                _clientWindowSizeProvider.Width - TrackerWidth - 10,
                200,
                TrackerWidth,
                TrackerHeight);

            Visible = false;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            _cachedStats = ComputeStatData();
            _cachedProgress = GetLevelProgress();
            base.Initialize();
        }

        // IZOrderedWindow implementation
        private int _zOrder = 100;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => true;

        public void BringToFront()
        {
        }

        public void Toggle()
        {
            Visible = !Visible;
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Only sample and refresh while session is active
            if (_sessionProvider.GrindSessionActive)
            {
                var now = DateTime.Now;
                var currentExp = _characterProvider.MainCharacter.Stats[CharacterStat.Experience];

                // Add EXP sample if value changed or no samples yet
                if (_expSamples.Count == 0 || _expSamples.Last().TotalExp != currentExp)
                {
                    _expSamples.Enqueue((now, currentExp));
                }

                // Trim samples older than rolling window
                var cutoff = now.AddMinutes(-RollingWindowMinutes);
                while (_expSamples.Count > 1 && _expSamples.Peek().Time < cutoff)
                {
                    _expSamples.Dequeue();
                }

                // Refresh cached stats every 5 seconds (or on first run / dirty flag)
                if (_statsDirty || (now - _lastRefreshTime).TotalSeconds >= RefreshIntervalSeconds)
                {
                    _cachedStats = ComputeStatData();
                    _cachedProgress = GetLevelProgress();
                    _lastRefreshTime = now;
                    _statsDirty = false;
                }
            }

            base.OnUpdateControl(gameTime);
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            var clickPos = eventArgs.Position;

            // Convert screen-space click to game-space coordinates
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var gameClickX = (int)((clickPos.X - offset.X) / scale);
            var gameClickY = (int)((clickPos.Y - offset.Y) / scale);

            var panelPos = DrawPositionWithParentOffset;
            var localX = gameClickX - (int)panelPos.X;
            var localY = gameClickY - (int)panelPos.Y;
            var localPoint = new Point(localX, localY);

            if (_startBtnArea.Contains(localPoint))
            {
                StartSession();
                return true;
            }
            if (_pauseBtnArea.Contains(localPoint))
            {
                PauseSession();
                return true;
            }
            if (_resetBtnArea.Contains(localPoint))
            {
                ResetSession();
                return true;
            }

            return true;
        }

        private void StartSession()
        {
            if (_sessionProvider.GrindSessionActive) return;

            if (_sessionProvider.GrindSessionPausedElapsed == TimeSpan.Zero)
            {
                // Fresh start - capture baselines
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

            // Accumulate elapsed time since last resume
            _sessionRepository.GrindSessionPausedElapsed += DateTime.Now - _sessionProvider.GrindSessionResumeTime;
            _sessionRepository.GrindSessionActive = false;

            // Snapshot stats at the moment of pause so display is frozen
            _cachedStats = ComputeStatData();
            _cachedProgress = GetLevelProgress();
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
            _cachedStats = ComputeStatData();
            _cachedProgress = GetLevelProgress();
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
            {
                elapsed += DateTime.Now - _sessionProvider.GrindSessionResumeTime;
            }
            return elapsed;
        }

        private int GetExpPerHour()
        {
            var currentExp = _characterProvider.MainCharacter.Stats[CharacterStat.Experience];
            var sessionExp = currentExp - _sessionProvider.StartingExp;

            // Try rolling average first (from sampled data over last N minutes)
            if (_expSamples.Count >= 2)
            {
                var oldest = _expSamples.Peek();
                var windowSpan = (DateTime.Now - oldest.Time).TotalHours;
                if (windowSpan > 0.001) // At least ~4 seconds of data
                {
                    var windowExp = currentExp - oldest.TotalExp;
                    return (int)(windowExp / windowSpan);
                }
            }

            // Fall back to full session average
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

        // ─── Drawing ───

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                DrawPanelFills(DrawPositionWithParentOffset);
            }
            else
            {
                DrawPanelComplete(DrawPositionWithParentOffset);
            }

            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawPositionWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawPanelBordersAndText(scaledPos, scaleFactor);
        }

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));
            _spriteBatch.End();
        }

        private void DrawPanelBordersAndText(Vector2 scaledPos, float scale)
        {
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scale);
            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);

            _spriteBatch.Begin();

            // Background + border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Header bar
            var headerRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, (int)(HeaderHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);

            // Title
            _spriteBatch.DrawString(font, "Grind Tracker", new Vector2(scaledPos.X + Padding * scale, scaledPos.Y + 2 * scale), HeaderAccent);

            // Session control buttons in the header
            DrawHeaderButtonsScaled(scaledPos, scale, font);

            // Stat rows
            DrawStatsScaled(scaledPos, scale, font);

            // Progress bar
            DrawProgressBarScaled(scaledPos, scale, font);

            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Background + border
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Header
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);
            _spriteBatch.DrawString(_labelFont, "Grind Tracker", new Vector2(pos.X + Padding, pos.Y + 2), HeaderAccent);

            // Session control buttons
            DrawHeaderButtons(pos);

            // Stats
            DrawStats(pos);

            // Progress bar
            DrawProgressBar(pos);

            _spriteBatch.End();
        }

        // ─── Header Buttons ───

        private void DrawHeaderButtons(Vector2 pos)
        {
            var isActive = _sessionProvider.GrindSessionActive;
            var hasElapsed = _sessionProvider.GrindSessionPausedElapsed > TimeSpan.Zero || isActive;

            var startText = isActive ? "" : "[Start]";
            var pauseText = isActive ? "[Pause]" : "";
            var resetText = hasElapsed ? "[Reset]" : "";

            var rightEdge = (int)(pos.X + DrawArea.Width - Padding);
            var btnY = (int)(pos.Y + 2);

            // Draw from right to left: Reset, Pause/Start
            if (resetText.Length > 0)
            {
                var size = _labelFont.MeasureString(resetText);
                rightEdge -= (int)size.Width;
                _spriteBatch.DrawString(_labelFont, resetText, new Vector2(rightEdge, btnY), _styleProvider.DangerColor);
                _resetBtnArea = new Rectangle(rightEdge - (int)pos.X, btnY - (int)pos.Y, (int)size.Width + 4, HeaderHeight);
                rightEdge -= 6;
            }
            else
            {
                _resetBtnArea = Rectangle.Empty;
            }

            var toggleText = isActive ? pauseText : startText;
            if (toggleText.Length > 0)
            {
                var size = _labelFont.MeasureString(toggleText);
                rightEdge -= (int)size.Width;
                var color = isActive ? _styleProvider.GoldColor : _styleProvider.CompletionColor;
                _spriteBatch.DrawString(_labelFont, toggleText, new Vector2(rightEdge, btnY), color);
                if (isActive)
                {
                    _pauseBtnArea = new Rectangle(rightEdge - (int)pos.X, btnY - (int)pos.Y, (int)size.Width + 4, HeaderHeight);
                    _startBtnArea = Rectangle.Empty;
                }
                else
                {
                    _startBtnArea = new Rectangle(rightEdge - (int)pos.X, btnY - (int)pos.Y, (int)size.Width + 4, HeaderHeight);
                    _pauseBtnArea = Rectangle.Empty;
                }
            }
        }

        private void DrawHeaderButtonsScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var isActive = _sessionProvider.GrindSessionActive;
            var hasElapsed = _sessionProvider.GrindSessionPausedElapsed > TimeSpan.Zero || isActive;

            var startText = isActive ? "" : "[Start]";
            var pauseText = isActive ? "[Pause]" : "";
            var resetText = hasElapsed ? "[Reset]" : "";

            var rightEdge = (int)(scaledPos.X + DrawArea.Width * scale - Padding * scale);
            var btnY = (int)(scaledPos.Y + 2 * scale);
            var panelOriginX = (int)scaledPos.X;
            var panelOriginY = (int)scaledPos.Y;

            if (resetText.Length > 0)
            {
                var size = font.MeasureString(resetText);
                rightEdge -= (int)size.Width;
                _spriteBatch.DrawString(font, resetText, new Vector2(rightEdge, btnY), _styleProvider.DangerColor);
                _resetBtnArea = new Rectangle((int)((rightEdge - panelOriginX) / scale), (int)((btnY - panelOriginY) / scale), (int)(size.Width / scale) + 4, HeaderHeight);
                rightEdge -= (int)(6 * scale);
            }
            else
            {
                _resetBtnArea = Rectangle.Empty;
            }

            var toggleText = isActive ? pauseText : startText;
            if (toggleText.Length > 0)
            {
                var size = font.MeasureString(toggleText);
                rightEdge -= (int)size.Width;
                var color = isActive ? _styleProvider.GoldColor : _styleProvider.CompletionColor;
                _spriteBatch.DrawString(font, toggleText, new Vector2(rightEdge, btnY), color);
                var area = new Rectangle((int)((rightEdge - panelOriginX) / scale), (int)((btnY - panelOriginY) / scale), (int)(size.Width / scale) + 4, HeaderHeight);
                if (isActive)
                {
                    _pauseBtnArea = area;
                    _startBtnArea = Rectangle.Empty;
                }
                else
                {
                    _startBtnArea = area;
                    _pauseBtnArea = Rectangle.Empty;
                }
            }
        }

        // ─── Stats ───

        private void DrawStats(Vector2 pos)
        {
            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;
            var startY = pos.Y + HeaderHeight + Padding;
            var labelX = pos.X + Padding;
            var valueX = pos.X + DrawArea.Width / 2 + 10;

            var (labels, values, valueColors) = GetStatData();

            for (int i = 0; i < labels.Length; i++)
            {
                var y = startY + i * RowHeight;
                _spriteBatch.DrawString(_labelFont, labels[i], new Vector2(labelX, y), labelColor);
                _spriteBatch.DrawString(_font, values[i], new Vector2(valueX, y), valueColors[i]);
            }
        }

        private void DrawStatsScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var labelColor = _styleProvider.TextSecondary;
            var startY = scaledPos.Y + (HeaderHeight + Padding) * scale;
            var labelX = scaledPos.X + Padding * scale;
            var valueX = scaledPos.X + (DrawArea.Width / 2 + 10) * scale;

            var (labels, values, valueColors) = GetStatData();

            for (int i = 0; i < labels.Length; i++)
            {
                var y = startY + i * RowHeight * scale;
                _spriteBatch.DrawString(font, labels[i], new Vector2(labelX, y), labelColor);
                _spriteBatch.DrawString(font, values[i], new Vector2(valueX, y), valueColors[i]);
            }
        }

        private (string[] Labels, string[] Values, Color[] ValueColors) GetStatData()
        {
            // Session time updates live (every frame); other stats use 5s cache
            if (_sessionProvider.GrindSessionActive && _cachedStats.Values != null)
            {
                _cachedStats.Values[0] = FormatDuration(GetActiveSessionTime());
            }
            // When paused, the cached session string already has " (paused)" baked in
            return _cachedStats;
        }

        private (string[] Labels, string[] Values, Color[] ValueColors) ComputeStatData()
        {
            var sessionTime = GetActiveSessionTime();
            var isActive = _sessionProvider.GrindSessionActive;
            var isPaused = !isActive && _sessionProvider.GrindSessionPausedElapsed > TimeSpan.Zero;
            var hasStarted = isActive || isPaused;

            var expPerHour = hasStarted ? GetExpPerHour() : 0;
            var totalHours = sessionTime.TotalHours;
            var killCount = _sessionProvider.KillCount;
            var killsPerHour = totalHours > 0.001 ? (int)(killCount / totalHours) : 0;
            var goldDelta = GetCurrentGold() - _sessionProvider.StartingGold;
            var timeToLevel = hasStarted ? GetTimeToLevel() : "--";

            var sessionStr = hasStarted ? FormatDuration(sessionTime) : "Ready";
            if (isPaused) sessionStr += " (paused)";

            var defaultValue = _styleProvider.TextPrimary;
            var goldColor = goldDelta >= 0 ? _styleProvider.GoldColor : _styleProvider.DangerColor;

            return (
                new[] { "Session", "EXP/hr", "To Level", "Kills", "Gold" },
                new[]
                {
                    sessionStr,
                    hasStarted ? FormatNumber(expPerHour) : "--",
                    timeToLevel,
                    hasStarted ? $"{killCount} ({killsPerHour}/hr)" : "--",
                    hasStarted ? $"{(goldDelta >= 0 ? "+" : "")}{FormatNumber(goldDelta)}" : "--"
                },
                new[] { defaultValue, defaultValue, defaultValue, defaultValue, goldColor }
            );
        }

        // ─── Progress Bar ───

        private void DrawProgressBar(Vector2 pos)
        {
            var progress = _cachedProgress;
            var barY = (int)(pos.Y + HeaderHeight + NumStatRows * RowHeight + Padding * 2);
            var barX = (int)(pos.X + Padding);
            var barWidth = DrawArea.Width - Padding * 2;

            // Background
            var bgRect = new Rectangle(barX, barY, barWidth, ProgressBarHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.ProgressBarBackground);

            // Fill
            var fillWidth = (int)(barWidth * progress);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(barX, barY, fillWidth, ProgressBarHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, fillRect, _styleProvider.ProgressBarFill);
            }

            // Border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 1);

            // Percentage text
            var pctText = $"{(int)(progress * 100)}%";
            var pctSize = _font.MeasureString(pctText);
            var pctX = barX + (barWidth - pctSize.Width) / 2;
            var pctY = barY + (ProgressBarHeight - pctSize.Height) / 2;
            _spriteBatch.DrawString(_font, pctText, new Vector2(pctX, pctY), _styleProvider.TextPrimary);
        }

        private void DrawProgressBarScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var progress = _cachedProgress;
            var barY = (int)(scaledPos.Y + (HeaderHeight + NumStatRows * RowHeight + Padding * 2) * scale);
            var barX = (int)(scaledPos.X + Padding * scale);
            var barWidth = (int)((DrawArea.Width - Padding * 2) * scale);
            var barHeight = (int)(ProgressBarHeight * scale);

            // Background
            var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.ProgressBarBackground);

            // Fill
            var fillWidth = (int)(barWidth * progress);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(barX, barY, fillWidth, barHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, fillRect, _styleProvider.ProgressBarFill);
            }

            // Border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)scale));

            // Percentage text
            var pctText = $"{(int)(progress * 100)}%";
            var pctSize = font.MeasureString(pctText);
            var pctX = barX + (barWidth - pctSize.Width) / 2;
            var pctY = barY + (barHeight - pctSize.Height) / 2;
            _spriteBatch.DrawString(font, pctText, new Vector2(pctX, pctY), _styleProvider.TextPrimary);
        }
    }
}
