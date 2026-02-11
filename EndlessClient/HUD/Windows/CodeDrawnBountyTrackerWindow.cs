using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// A small, draggable floating window that shows guild bounty progress.
    /// Always displays all bounties (no track/untrack). Reads from IBountyDataProvider
    /// which is populated by the quest progress poll cycle.
    /// Polls independently so bounties update without the quest window being open.
    /// </summary>
    public class CodeDrawnBountyTrackerWindow : DraggableHudPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;

        private const int TrackerWidth = 180;
        private const int HeaderHeight = 18;
        private const int RowHeight = 14;
        private const int Padding = 4;
        private const int MaxBounties = 10;
        private const double PollIntervalSeconds = 2.0;

        private IReadOnlyList<BountyProgressData> _bounties = new List<BountyProgressData>();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        // Header accent color (purple/gold theme for guild bounties)
        private static readonly Color HeaderColor = new Color(70, 45, 80, 230);
        private static readonly Color HeaderAccent = new Color(200, 170, 80);

        public CodeDrawnBountyTrackerWindow(
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IBountyDataProvider bountyDataProvider,
            IQuestActions questActions)
            : base(true)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _bountyDataProvider = bountyDataProvider;
            _questActions = questActions;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];

            // Position below the quest tracker by default
            DrawArea = new Rectangle(
                _clientWindowSizeProvider.Width - TrackerWidth - 10,
                120,
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
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                UpdatePanelHeight();
                // Immediately request data when opened
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _pollStopwatch.Restart();
            }
            else
            {
                _pollStopwatch.Stop();
            }
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Poll for bounty updates independently when visible
            if (Visible)
            {
                if (!_pollStopwatch.IsRunning)
                {
                    _pollStopwatch.Start();
                }

                if (_pollStopwatch.Elapsed.TotalSeconds >= PollIntervalSeconds)
                {
                    _pollStopwatch.Restart();
                    _questActions.RequestQuestHistory(QuestPage.Progress);
                }

                var currentBounties = _bountyDataProvider.Bounties;
                if (currentBounties != _bounties)
                {
                    _bounties = currentBounties;
                    UpdatePanelHeight();
                }
            }
            else
            {
                _pollStopwatch.Stop();
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdatePanelHeight()
        {
            var count = Math.Min(_bounties.Count, MaxBounties);
            var height = HeaderHeight + Math.Max(1, count) * RowHeight + Padding;
            DrawArea = new Rectangle(DrawArea.X, DrawArea.Y, TrackerWidth, height);
        }

        // IZOrderedWindow implementation
        private int _zOrder = 115;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => true;

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

            // Header with purple/gold theme
            var headerRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, (int)(HeaderHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);

            // Title
            _spriteBatch.DrawString(font, "Guild Bounties", new Vector2(scaledPos.X + Padding * scale, scaledPos.Y + 2 * scale), HeaderAccent);

            // Draw bounties
            DrawBountiesScaled(scaledPos, scale, font);

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
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);
            _spriteBatch.DrawString(_labelFont, "Guild Bounties", new Vector2(pos.X + Padding, pos.Y + 2), HeaderAccent);

            // Draw bounties
            DrawBounties(pos);

            _spriteBatch.End();
        }

        private void DrawBounties(Vector2 pos)
        {
            var startY = pos.Y + HeaderHeight + 2;

            if (_bounties.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No guild bounties", new Vector2(pos.X + Padding, startY), _styleProvider.TextSecondary);
                return;
            }

            for (int i = 0; i < Math.Min(_bounties.Count, MaxBounties); i++)
            {
                var bounty = _bounties[i];
                var y = startY + i * RowHeight;

                // Bounty name (truncated if needed)
                var nameColor = bounty.Status == BountyStatus.Complete ? new Color(100, 200, 100) : _styleProvider.TextPrimary;
                var name = bounty.Name.Length > 18 ? bounty.Name.Substring(0, 15) + "..." : bounty.Name;
                _spriteBatch.DrawString(_font, name, new Vector2(pos.X + Padding, y), nameColor);

                // Progress
                var progressText = bounty.Status == BountyStatus.Complete
                    ? "\u2713"
                    : $"{bounty.Progress}/{bounty.Target}";
                var progressSize = _font.MeasureString(progressText);
                var progressColor = bounty.Status == BountyStatus.Complete ? new Color(100, 200, 100) : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(_font, progressText, new Vector2(pos.X + DrawArea.Width - Padding - progressSize.Width, y), progressColor);
            }
        }

        private void DrawBountiesScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var startY = scaledPos.Y + (HeaderHeight + 2) * scale;

            if (_bounties.Count == 0)
            {
                _spriteBatch.DrawString(font, "No guild bounties", new Vector2(scaledPos.X + Padding * scale, startY), _styleProvider.TextSecondary);
                return;
            }

            for (int i = 0; i < Math.Min(_bounties.Count, MaxBounties); i++)
            {
                var bounty = _bounties[i];
                var y = startY + i * RowHeight * scale;

                // Bounty name (truncated if needed)
                var nameColor = bounty.Status == BountyStatus.Complete ? new Color(100, 200, 100) : _styleProvider.TextPrimary;
                var name = bounty.Name.Length > 18 ? bounty.Name.Substring(0, 15) + "..." : bounty.Name;
                _spriteBatch.DrawString(font, name, new Vector2(scaledPos.X + Padding * scale, y), nameColor);

                // Progress
                var progressText = bounty.Status == BountyStatus.Complete
                    ? "\u2713"
                    : $"{bounty.Progress}/{bounty.Target}";
                var progressSize = font.MeasureString(progressText);
                var progressColor = bounty.Status == BountyStatus.Complete ? new Color(100, 200, 100) : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(font, progressText, new Vector2(scaledPos.X + DrawArea.Width * scale - Padding * scale - progressSize.Width, y), progressColor);
            }
        }
    }
}
