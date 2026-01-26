using System;
using System.Collections.Generic;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Quest;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Code-drawn quest status window showing quest progress and history.
    /// Implements IPostScaleDrawable for crisp text rendering at any scale.
    /// </summary>
    public class CodeDrawnQuestWindow : XNAControl, IZOrderedWindow
    {
        public event Action Activated;
        private readonly ICharacterProvider _characterProvider;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IQuestActions _questActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;

        private readonly CodeDrawnButton _progressButton;
        private readonly CodeDrawnButton _historyButton;

        private QuestPage _currentPage = QuestPage.Progress;
        private IReadOnlyList<QuestProgressData> _cachedProgress = new List<QuestProgressData>();
        private IReadOnlyList<string> _cachedHistory = new List<string>();
        private int _scrollOffset = 0;

        // Quest Tracker state
        private bool _questTrackerEnabled = false;
        private readonly HashSet<string> _trackedQuestNames = new HashSet<string>();
        private CodeDrawnQuestTrackerWindow _questTrackerWindow;

        private const int WindowWidth = 320;
        private const int WindowHeight = 250;
        private const int HeaderHeight = 24;
        private const int TabHeight = 28;
        private const int RowHeight = 18;
        private const int Padding = 8;
        private const int MaxVisibleRows = 9;
        private const int CheckboxSize = 12;
        private const int QuestCheckboxOffset = 20; // Offset for quest name when checkboxes visible

        // Hit areas for checkboxes
        private Rectangle _trackerCheckboxRect;
        private readonly Dictionary<int, Rectangle> _questCheckboxRects = new Dictionary<int, Rectangle>();
        private bool _wasMouseDown = false; // For direct click detection

        public CodeDrawnQuestWindow(
            ICharacterProvider characterProvider,
            IQuestDataProvider questDataProvider,
            IQuestActions questActions,
            ILocalizedStringFinder localizedStringFinder,
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _characterProvider = characterProvider;
            _questDataProvider = questDataProvider;
            _questActions = questActions;
            _localizedStringFinder = localizedStringFinder;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];

            // Center the window
            DrawArea = new Rectangle(
                (_clientWindowSizeProvider.Width - WindowWidth) / 2,
                (_clientWindowSizeProvider.Height - WindowHeight) / 2,
                WindowWidth,
                WindowHeight);

            // Create tab buttons
            _progressButton = new CodeDrawnButton(styleProvider, _labelFont)
            {
                Text = "Progress",
                DrawArea = new Rectangle(Padding, HeaderHeight + 2, 70, 20)
            };

            _historyButton = new CodeDrawnButton(styleProvider, _labelFont)
            {
                Text = "History",
                DrawArea = new Rectangle(Padding + 75, HeaderHeight + 2, 70, 20)
            };

            Visible = false;
        }

        public void SetQuestTrackerWindow(CodeDrawnQuestTrackerWindow trackerWindow)
        {
            _questTrackerWindow = trackerWindow;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            _progressButton.OnClick += (_, _) => SwitchToPage(QuestPage.Progress);
            _progressButton.SetParentControl(this);
            _progressButton.Initialize();

            _historyButton.OnClick += (_, _) => SwitchToPage(QuestPage.History);
            _historyButton.SetParentControl(this);
            _historyButton.Initialize();

            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Check for data updates
            var currentProgress = _questDataProvider.QuestProgress;
            var progressChanged = currentProgress != _cachedProgress;

            if (progressChanged)
            {
                _cachedProgress = currentProgress;
                _scrollOffset = 0;
            }

            // Always update tracker if visible to catch content changes
            if (_questTrackerWindow != null && _questTrackerWindow.Visible)
            {
                _questTrackerWindow.UpdateQuestProgress(_cachedProgress);
            }

            if (_questDataProvider.QuestHistory != _cachedHistory)
            {
                _cachedHistory = _questDataProvider.QuestHistory;
                _scrollOffset = 0;
            }

            // Direct mouse click detection for checkboxes (bypasses XNAControl event system)
            var mouseState = Mouse.GetState();
            var isMouseDown = mouseState.LeftButton == ButtonState.Pressed;
            var mousePos = TransformMousePosition(new Point(mouseState.X, mouseState.Y));

            // Fire Activated on mouse down inside window to bring to front
            if (isMouseDown && !_wasMouseDown && Visible)
            {
                var windowBounds = new Rectangle(
                    (int)DrawPositionWithParentOffset.X,
                    (int)DrawPositionWithParentOffset.Y,
                    DrawArea.Width,
                    DrawArea.Height);
                if (windowBounds.Contains(mousePos))
                {
                    Activated?.Invoke();
                }
            }

            // Detect click (mouse up after mouse was down)
            if (_wasMouseDown && !isMouseDown && Visible)
            {
                HandleCheckboxClick(mousePos);
            }
            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
        }

        private void HandleCheckboxClick(Point mousePos)
        {
            // Check tracker checkbox click
            if (_trackerCheckboxRect.Contains(mousePos))
            {
                _questTrackerEnabled = !_questTrackerEnabled;
                if (_questTrackerWindow != null)
                {
                    _questTrackerWindow.Visible = _questTrackerEnabled;
                    if (_questTrackerEnabled)
                    {
                        UpdateTrackerWindow();
                    }
                }
                return;
            }

            // Check quest checkboxes (only on Progress page when tracker enabled)
            if (_questTrackerEnabled && _currentPage == QuestPage.Progress)
            {
                foreach (var kvp in _questCheckboxRects)
                {
                    if (kvp.Value.Contains(mousePos))
                    {
                        var questIndex = kvp.Key;
                        if (questIndex < _cachedProgress.Count)
                        {
                            var questName = _cachedProgress[questIndex].Name;
                            if (_trackedQuestNames.Contains(questName))
                                _trackedQuestNames.Remove(questName);
                            else
                                _trackedQuestNames.Add(questName);

                            UpdateTrackerWindow();
                        }
                        return;
                    }
                }
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

        // IZOrderedWindow implementation
        private int _zOrder = 100;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

        protected override void OnDrawControl(GameTime gameTime)
        {
            // Always update hit areas in game coordinates (not scaled)
            var pos = DrawPositionWithParentOffset;
            _trackerCheckboxRect = new Rectangle(
                (int)(pos.X + WindowWidth - 90),
                (int)(pos.Y + HeaderHeight + 6),
                CheckboxSize,
                CheckboxSize);

            _questCheckboxRects.Clear();
            if (_questTrackerEnabled && _currentPage == QuestPage.Progress)
            {
                var startY = pos.Y + HeaderHeight + TabHeight + Padding;
                for (int i = _scrollOffset; i < _cachedProgress.Count && i < _scrollOffset + MaxVisibleRows; i++)
                {
                    var rowIndex = i - _scrollOffset;
                    var y = startY + rowIndex * RowHeight;
                    var checkRect = new Rectangle(
                        (int)(pos.X + Padding),
                        (int)(y + 2),
                        CheckboxSize,
                        CheckboxSize);
                    _questCheckboxRects[i] = checkRect;
                }
            }

            if (SkipRenderTargetDraw)
            {
                DrawFills(pos);
            }
            else
            {
                DrawComplete(pos);
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

            DrawBordersAndText(scaledPos, scaleFactor);
        }

        private void DrawFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, WindowWidth, WindowHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            _spriteBatch.End();
        }

        private void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            var font = scale >= 1.75f ? _scaledFont : _labelFont;

            var scaledWidth = (int)(WindowWidth * scale);
            var scaledHeight = (int)(WindowHeight * scale);

            _spriteBatch.Begin();

            // Draw background fill
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            // Draw border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Draw header line
            var headerY = (int)(scaledPos.Y + HeaderHeight * scale);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)scaledPos.X, headerY, scaledWidth, Math.Max(1, (int)scale)), _styleProvider.PanelBorder);

            // Draw tab area line
            var tabY = (int)(scaledPos.Y + (HeaderHeight + TabHeight) * scale);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)scaledPos.X, tabY, scaledWidth, Math.Max(1, (int)scale)), _styleProvider.PanelBorder);

            // Draw title
            var title = GetTitle();
            var titleSize = font.MeasureString(title);
            var titleX = scaledPos.X + (scaledWidth - titleSize.Width) / 2;
            _spriteBatch.DrawString(font, title, new Vector2(titleX, scaledPos.Y + 4 * scale), _styleProvider.TextPrimary);

            // Draw Quest Tracker checkbox (right side of tab area)
            DrawTrackerCheckboxScaled(scaledPos, scale, font);

            // Draw quest list
            DrawQuestListScaled(scaledPos, scale, font);

            _spriteBatch.End();
        }

        private void DrawTrackerCheckboxScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var checkboxX = scaledPos.X + (WindowWidth - 90) * scale;
            var checkboxY = scaledPos.Y + (HeaderHeight + 6) * scale;
            var checkboxSize = (int)(CheckboxSize * scale);

            var checkRect = new Rectangle((int)checkboxX, (int)checkboxY, checkboxSize, checkboxSize);

            // Checkbox background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, checkRect, new Color(40, 40, 40));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, checkRect, _styleProvider.PanelBorder, 1);

            // Checkmark if enabled
            if (_questTrackerEnabled)
            {
                _spriteBatch.DrawString(font, "x", new Vector2(checkboxX + 2, checkboxY), new Color(100, 200, 100));
            }

            // Label
            _spriteBatch.DrawString(font, "Tracker", new Vector2(checkboxX + checkboxSize + 4, checkboxY), _styleProvider.TextSecondary);
        }

        private void DrawComplete(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, WindowWidth, WindowHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Header line
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X, (int)pos.Y + HeaderHeight, WindowWidth, 1), _styleProvider.PanelBorder);

            // Tab area line
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X, (int)pos.Y + HeaderHeight + TabHeight, WindowWidth, 1), _styleProvider.PanelBorder);

            // Title
            var title = GetTitle();
            var titleSize = _labelFont.MeasureString(title);
            var titleX = pos.X + (WindowWidth - titleSize.Width) / 2;
            _spriteBatch.DrawString(_labelFont, title, new Vector2(titleX, pos.Y + 4), _styleProvider.TextPrimary);

            // Draw Quest Tracker checkbox
            DrawTrackerCheckbox(pos);

            // Quest list
            DrawQuestList(pos);

            _spriteBatch.End();
        }

        private void DrawTrackerCheckbox(Vector2 pos)
        {
            var checkboxX = pos.X + WindowWidth - 90;
            var checkboxY = pos.Y + HeaderHeight + 6;

            _trackerCheckboxRect = new Rectangle((int)checkboxX, (int)checkboxY, CheckboxSize, CheckboxSize);

            // Checkbox background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _trackerCheckboxRect, new Color(40, 40, 40));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _trackerCheckboxRect, _styleProvider.PanelBorder, 1);

            // Checkmark if enabled
            if (_questTrackerEnabled)
            {
                _spriteBatch.DrawString(_font, "x", new Vector2(checkboxX + 2, checkboxY), new Color(100, 200, 100));
            }

            // Label
            _spriteBatch.DrawString(_font, "Tracker", new Vector2(checkboxX + CheckboxSize + 4, checkboxY), _styleProvider.TextSecondary);
        }

        private void DrawQuestList(Vector2 pos)
        {
            var startY = pos.Y + HeaderHeight + TabHeight + Padding;
            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            if (_currentPage == QuestPage.Progress)
            {
                if (_cachedProgress.Count == 0)
                {
                    var noProgressText = _localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_START_ANY);
                    _spriteBatch.DrawString(_font, noProgressText, new Vector2(pos.X + Padding, startY), labelColor);
                    return;
                }

                for (int i = _scrollOffset; i < _cachedProgress.Count && i < _scrollOffset + MaxVisibleRows; i++)
                {
                    var quest = _cachedProgress[i];
                    var rowIndex = i - _scrollOffset;
                    var y = startY + rowIndex * RowHeight;

                    // Draw quest checkbox if tracker is enabled
                    if (_questTrackerEnabled)
                    {
                        var checkX = (int)(pos.X + Padding);
                        var checkY = (int)(y + 2);
                        var checkRect = new Rectangle(checkX, checkY, CheckboxSize, CheckboxSize);
                        _questCheckboxRects[i] = checkRect;

                        DrawingPrimitives.DrawFilledRect(_spriteBatch, checkRect, new Color(40, 40, 40));
                        DrawingPrimitives.DrawRectBorder(_spriteBatch, checkRect, _styleProvider.PanelBorder, 1);

                        if (_trackedQuestNames.Contains(quest.Name))
                        {
                            _spriteBatch.DrawString(_font, "x", new Vector2(checkX + 2, checkY), new Color(100, 200, 100));
                        }
                    }

                    var nameX = pos.X + Padding + (_questTrackerEnabled ? QuestCheckboxOffset : 0);
                    _spriteBatch.DrawString(_labelFont, quest.Name, new Vector2(nameX, y), valueColor);

                    var progressText = quest.Target > 0 ? $"{quest.Progress} / {quest.Target}" : "n / a";
                    var progressSize = _font.MeasureString(progressText);
                    _spriteBatch.DrawString(_font, progressText, new Vector2(pos.X + WindowWidth - Padding - progressSize.Width, y), labelColor);
                }
            }
            else
            {
                if (_cachedHistory.Count == 0)
                {
                    var noHistoryText = _localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_FINISH_ANY);
                    _spriteBatch.DrawString(_font, noHistoryText, new Vector2(pos.X + Padding, startY), labelColor);
                    return;
                }

                for (int i = _scrollOffset; i < _cachedHistory.Count && i < _scrollOffset + MaxVisibleRows; i++)
                {
                    var questName = _cachedHistory[i];
                    var y = startY + (i - _scrollOffset) * RowHeight;

                    _spriteBatch.DrawString(_labelFont, questName, new Vector2(pos.X + Padding, y), valueColor);

                    var completedText = _localizedStringFinder.GetString(EOResourceID.QUEST_COMPLETED);
                    var completedSize = _font.MeasureString(completedText);
                    _spriteBatch.DrawString(_font, completedText, new Vector2(pos.X + WindowWidth - Padding - completedSize.Width, y), labelColor);
                }
            }
        }

        private void DrawQuestListScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var startY = scaledPos.Y + (HeaderHeight + TabHeight + Padding) * scale;
            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            if (_currentPage == QuestPage.Progress)
            {
                if (_cachedProgress.Count == 0)
                {
                    var noProgressText = _localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_START_ANY);
                    _spriteBatch.DrawString(font, noProgressText, new Vector2(scaledPos.X + Padding * scale, startY), labelColor);
                    return;
                }

                for (int i = _scrollOffset; i < _cachedProgress.Count && i < _scrollOffset + MaxVisibleRows; i++)
                {
                    var quest = _cachedProgress[i];
                    var rowIndex = i - _scrollOffset;
                    var y = startY + rowIndex * RowHeight * scale;

                    // Draw quest checkbox if tracker is enabled
                    if (_questTrackerEnabled)
                    {
                        var checkboxSize = (int)(CheckboxSize * scale);
                        var checkX = (int)(scaledPos.X + Padding * scale);
                        var checkY = (int)(y + 2 * scale);
                        var checkRect = new Rectangle(checkX, checkY, checkboxSize, checkboxSize);

                        DrawingPrimitives.DrawFilledRect(_spriteBatch, checkRect, new Color(40, 40, 40));
                        DrawingPrimitives.DrawRectBorder(_spriteBatch, checkRect, _styleProvider.PanelBorder, 1);

                        if (_trackedQuestNames.Contains(quest.Name))
                        {
                            _spriteBatch.DrawString(font, "x", new Vector2(checkX + 2, checkY), new Color(100, 200, 100));
                        }
                    }

                    var nameX = scaledPos.X + (Padding + (_questTrackerEnabled ? QuestCheckboxOffset : 0)) * scale;
                    _spriteBatch.DrawString(font, quest.Name, new Vector2(nameX, y), valueColor);

                    var progressText = quest.Target > 0 ? $"{quest.Progress} / {quest.Target}" : "n / a";
                    var progressSize = font.MeasureString(progressText);
                    _spriteBatch.DrawString(font, progressText, new Vector2(scaledPos.X + WindowWidth * scale - Padding * scale - progressSize.Width, y), labelColor);
                }
            }
            else
            {
                if (_cachedHistory.Count == 0)
                {
                    var noHistoryText = _localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_FINISH_ANY);
                    _spriteBatch.DrawString(font, noHistoryText, new Vector2(scaledPos.X + Padding * scale, startY), labelColor);
                    return;
                }

                for (int i = _scrollOffset; i < _cachedHistory.Count && i < _scrollOffset + MaxVisibleRows; i++)
                {
                    var questName = _cachedHistory[i];
                    var y = startY + (i - _scrollOffset) * RowHeight * scale;

                    _spriteBatch.DrawString(font, questName, new Vector2(scaledPos.X + Padding * scale, y), valueColor);

                    var completedText = _localizedStringFinder.GetString(EOResourceID.QUEST_COMPLETED);
                    var completedSize = font.MeasureString(completedText);
                    _spriteBatch.DrawString(font, completedText, new Vector2(scaledPos.X + WindowWidth * scale - Padding * scale - completedSize.Width, y), labelColor);
                }
            }
        }

        private string GetTitle()
        {
            var pageText = _currentPage == QuestPage.Progress
                ? _localizedStringFinder.GetString(EOResourceID.QUEST_PROGRESS)
                : _localizedStringFinder.GetString(EOResourceID.QUEST_HISTORY);
            return $"{_characterProvider.MainCharacter.Name}'s {pageText}";
        }

        private void SwitchToPage(QuestPage page)
        {
            _currentPage = page;
            _scrollOffset = 0;
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                // Request quest data when opening
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _questActions.RequestQuestHistory(QuestPage.History);

                // Re-center when opened
                DrawArea = new Rectangle(
                    (_clientWindowSizeProvider.Width - WindowWidth) / 2,
                    (_clientWindowSizeProvider.Height - WindowHeight) / 2,
                    WindowWidth,
                    WindowHeight);
            }
        }

        private Point TransformMousePosition(Point position)
        {
            if (!_clientWindowSizeProvider.IsScaledMode)
                return position;

            var offset = _clientWindowSizeProvider.RenderOffset;
            var scale = _clientWindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Max(0, Math.Min(gameX, _clientWindowSizeProvider.GameWidth - 1)),
                Math.Max(0, Math.Min(gameY, _clientWindowSizeProvider.GameHeight - 1)));
        }

        // NOTE: HandleClick and HandleMouseDown intentionally not overridden
        // Click detection is handled directly in OnUpdateControl via _wasMouseDown tracking
        // to avoid XNAControl event system issues with child controls

        public void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
            Activated?.Invoke();
        }

        protected override bool HandleMouseWheelMoved(IXNAControl control, MouseEventArgs eventArgs)
        {
            var maxItems = _currentPage == QuestPage.Progress ? _cachedProgress.Count : _cachedHistory.Count;
            var maxScroll = Math.Max(0, maxItems - MaxVisibleRows);

            if (eventArgs.ScrollWheelDelta > 0)
                _scrollOffset = Math.Max(0, _scrollOffset - 1);
            else if (eventArgs.ScrollWheelDelta < 0)
                _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);

            return true;
        }
    }
}
