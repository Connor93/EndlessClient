using System;
using System.Diagnostics;
using System.Collections.Generic;
using EndlessClient.Content;
using EndlessClient.HUD.Panels;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Interact.Quest;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// A small, draggable floating window that shows tracked quest progress.
    /// Implements IPostScaleDrawable for crisp text rendering at any scale.
    /// </summary>
    public class CodeDrawnQuestTrackerWindow : DraggableHudPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IQuestActions _questActions;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;

        private const int TrackerWidth = 180;
        private const int HeaderHeight = 18;
        private const int RowHeight = 14;
        private const int Padding = 4;
        private const int MaxTrackedQuests = 5;
        private const double PollIntervalSeconds = 2.0; // Poll server every 2 seconds

        // Tracked quests shared with CodeDrawnQuestWindow
        private HashSet<string> _trackedQuestNames = new HashSet<string>();
        private IReadOnlyList<QuestProgressData> _questProgress = new List<QuestProgressData>();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        public CodeDrawnQuestTrackerWindow(
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IQuestDataProvider questDataProvider,
            IQuestActions questActions)
            : base(true) // Enable dragging
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _questDataProvider = questDataProvider;
            _questActions = questActions;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];

            // Position in top-right corner by default
            DrawArea = new Rectangle(
                _clientWindowSizeProvider.Width - TrackerWidth - 10,
                50,
                TrackerWidth,
                HeaderHeight);

            Visible = false;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        public void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
            // DraggableHudPanel already has Activated event from HandleMouseDown
        }

        public void SetTrackedQuests(HashSet<string> trackedNames)
        {
            _trackedQuestNames = trackedNames ?? new HashSet<string>();
            UpdatePanelHeight();
        }

        public void UpdateQuestProgress(IReadOnlyList<QuestProgressData> progress)
        {
            _questProgress = progress ?? new List<QuestProgressData>();
            UpdatePanelHeight();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Periodically poll server for quest updates when tracker is visible
            if (Visible && _trackedQuestNames.Count > 0)
            {
                // Start stopwatch if not running
                if (!_pollStopwatch.IsRunning)
                {
                    _pollStopwatch.Start();
                }

                // Use real wall-clock time for polling (gameTime.ElapsedGameTime doesn't match real time at high FPS)
                if (_pollStopwatch.Elapsed.TotalSeconds >= PollIntervalSeconds)
                {
                    _pollStopwatch.Restart();
                    _questActions.RequestQuestHistory(QuestPage.Progress);
                }

                // Check for data updates from the provider
                if (_questDataProvider.QuestProgress != _questProgress)
                {
                    _questProgress = _questDataProvider.QuestProgress;
                    UpdatePanelHeight();
                }
            }
            else
            {
                // Stop stopwatch when not tracking
                _pollStopwatch.Stop();
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdatePanelHeight()
        {
            var trackedCount = 0;
            foreach (var quest in _questProgress)
            {
                if (_trackedQuestNames.Contains(quest.Name))
                    trackedCount++;
            }

            trackedCount = Math.Min(trackedCount, MaxTrackedQuests);
            var height = HeaderHeight + Math.Max(1, trackedCount) * RowHeight + Padding;
            DrawArea = new Rectangle(DrawArea.X, DrawArea.Y, TrackerWidth, height);
        }

        // IZOrderedWindow implementation
        private int _zOrder = 110;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

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
            var font = scale >= 1.75f ? _scaledFont : _labelFont;

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);

            _spriteBatch.Begin();

            // Background fill
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));

            // Border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Header
            var headerRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, (int)(HeaderHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));

            // Title
            var titleText = "Quest Tracker";
            _spriteBatch.DrawString(font, titleText, new Vector2(scaledPos.X + Padding * scale, scaledPos.Y + 2 * scale), Color.White);

            // Draw tracked quests
            DrawTrackedQuestsScaled(scaledPos, scale, font);

            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Header
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));
            _spriteBatch.DrawString(_labelFont, "Quest Tracker", new Vector2(pos.X + Padding, pos.Y + 2), Color.White);

            // Draw tracked quests
            DrawTrackedQuests(pos);

            _spriteBatch.End();
        }

        private void DrawTrackedQuests(Vector2 pos)
        {
            var startY = pos.Y + HeaderHeight + 2;
            var questIndex = 0;

            foreach (var quest in _questProgress)
            {
                if (!_trackedQuestNames.Contains(quest.Name))
                    continue;

                if (questIndex >= MaxTrackedQuests)
                    break;

                var y = startY + questIndex * RowHeight;

                // Quest name (truncated if needed)
                var name = quest.Name.Length > 18 ? quest.Name.Substring(0, 15) + "..." : quest.Name;
                _spriteBatch.DrawString(_font, name, new Vector2(pos.X + Padding, y), _styleProvider.TextPrimary);

                // Progress
                var progressText = quest.Target > 0 ? $"{quest.Progress}/{quest.Target}" : "done";
                var progressSize = _font.MeasureString(progressText);
                var progressColor = quest.Progress >= quest.Target ? new Color(100, 200, 100) : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(_font, progressText, new Vector2(pos.X + DrawArea.Width - Padding - progressSize.Width, y), progressColor);

                questIndex++;
            }

            if (questIndex == 0)
            {
                _spriteBatch.DrawString(_font, "No quests tracked", new Vector2(pos.X + Padding, startY), _styleProvider.TextSecondary);
            }
        }

        private void DrawTrackedQuestsScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var startY = scaledPos.Y + (HeaderHeight + 2) * scale;
            var questIndex = 0;

            foreach (var quest in _questProgress)
            {
                if (!_trackedQuestNames.Contains(quest.Name))
                    continue;

                if (questIndex >= MaxTrackedQuests)
                    break;

                var y = startY + questIndex * RowHeight * scale;

                // Quest name (truncated if needed)
                var name = quest.Name.Length > 18 ? quest.Name.Substring(0, 15) + "..." : quest.Name;
                _spriteBatch.DrawString(font, name, new Vector2(scaledPos.X + Padding * scale, y), _styleProvider.TextPrimary);

                // Progress
                var progressText = quest.Target > 0 ? $"{quest.Progress}/{quest.Target}" : "done";
                var progressSize = font.MeasureString(progressText);
                var progressColor = quest.Progress >= quest.Target ? new Color(100, 200, 100) : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(font, progressText, new Vector2(scaledPos.X + DrawArea.Width * scale - Padding * scale - progressSize.Width, y), progressColor);

                questIndex++;
            }

            if (questIndex == 0)
            {
                _spriteBatch.DrawString(font, "No quests tracked", new Vector2(scaledPos.X + Padding * scale, startY), _styleProvider.TextSecondary);
            }
        }
    }
}
