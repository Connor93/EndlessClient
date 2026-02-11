using System;
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
    /// A small, draggable floating window that shows guild stats at a glance:
    /// name/tag, level, EXP bar, points, contribution, and active buffs.
    /// Polls independently so data updates without the quest window being open.
    /// </summary>
    public class CodeDrawnGuildInfoWindow : DraggableHudPanel, IZOrderedWindow
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;

        private const int PanelWidth = 185;
        private const int HeaderHeight = 18;
        private const int RowHeight = 14;
        private const int ExpBarHeight = 8;
        private const int Padding = 4;
        private const double PollIntervalSeconds = 2.0;

        private GuildInfoData _guildInfo;
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        // Blue/silver theme for guild info
        private static readonly Color HeaderColor = new Color(40, 55, 85, 230);
        private static readonly Color HeaderAccent = new Color(180, 200, 230);
        private static readonly Color ExpBarBg = new Color(30, 30, 30, 200);
        private static readonly Color ExpBarFill = new Color(80, 160, 220);
        private static readonly Color BuffActiveColor = new Color(100, 220, 130);
        private static readonly Color BuffInactiveColor = new Color(100, 100, 100);

        public CodeDrawnGuildInfoWindow(
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

            // Position below the bounty tracker
            DrawArea = new Rectangle(
                _clientWindowSizeProvider.Width - PanelWidth - 10,
                260,
                PanelWidth,
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

                var currentInfo = _bountyDataProvider.GuildInfo;
                if (currentInfo != _guildInfo)
                {
                    _guildInfo = currentInfo;
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
            // Header + Level + EXP bar + Points + Contribution + optional Buffs row + padding
            var rows = 4; // level, exp bar, points, contribution
            if (_guildInfo != null && !string.IsNullOrEmpty(_guildInfo.ActiveBuffs))
                rows += 1; // buffs row
            var height = HeaderHeight + rows * RowHeight + ExpBarHeight + Padding * 2;
            DrawArea = new Rectangle(DrawArea.X, DrawArea.Y, PanelWidth, height);
        }

        // IZOrderedWindow implementation
        private int _zOrder = 116;
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

            // Background
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.85f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Header
            var headerRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, (int)(HeaderHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);

            DrawGuildInfoContent(scaledPos, scale, font);

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

            DrawGuildInfoContent(pos, 1f, _labelFont);

            _spriteBatch.End();
        }

        private void DrawGuildInfoContent(Vector2 pos, float scale, BitmapFont font)
        {
            if (_guildInfo == null)
            {
                _spriteBatch.DrawString(font, "No guild data",
                    new Vector2(pos.X + Padding * scale, pos.Y + 2 * scale), HeaderAccent);
                return;
            }

            // Header: Guild Name [TAG]
            var title = _guildInfo.GuildName;
            if (title.Length > 18)
                title = title.Substring(0, 16) + "..";
            title += " [" + _guildInfo.GuildTag + "]";
            _spriteBatch.DrawString(font, title,
                new Vector2(pos.X + Padding * scale, pos.Y + 2 * scale), HeaderAccent);

            var y = pos.Y + HeaderHeight * scale + Padding * scale;

            // Level
            var levelText = "Level " + _guildInfo.Level;
            _spriteBatch.DrawString(font, levelText,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextPrimary);
            y += RowHeight * scale;

            // EXP progress bar
            var barX = (int)(pos.X + Padding * scale);
            var barWidth = (int)((PanelWidth - Padding * 2) * scale);
            var barHeight = (int)(ExpBarHeight * scale);
            var barRect = new Rectangle(barX, (int)y, barWidth, barHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, ExpBarBg);

            if (_guildInfo.ExpToNext > 0)
            {
                var fillWidth = (int)(barWidth * Math.Min(1.0, (double)_guildInfo.Exp / (_guildInfo.Exp + _guildInfo.ExpToNext)));
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(barX, (int)y, fillWidth, barHeight);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, fillRect, ExpBarFill);
                }

                // EXP text centered on bar
                var expText = _guildInfo.Exp + "/" + (_guildInfo.Exp + _guildInfo.ExpToNext);
                var expSize = _font.MeasureString(expText);
                _spriteBatch.DrawString(_font, expText,
                    new Vector2(barX + (barWidth - expSize.Width) / 2, y), Color.White);
            }
            else
            {
                // Max level — fill bar completely
                DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, ExpBarFill);
                var maxText = "MAX";
                var maxSize = _font.MeasureString(maxText);
                _spriteBatch.DrawString(_font, maxText,
                    new Vector2(barX + (barWidth - maxSize.Width) / 2, y), Color.White);
            }

            y += (ExpBarHeight + 2) * scale;

            // Guild Points
            var ptsText = "Points: " + _guildInfo.Points;
            _spriteBatch.DrawString(font, ptsText,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextPrimary);
            y += RowHeight * scale;

            // My Contribution
            var contribText = "My Contrib: " + _guildInfo.Contribution;
            _spriteBatch.DrawString(font, contribText,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
            y += RowHeight * scale;

            // Active Buffs (if any)
            if (!string.IsNullOrEmpty(_guildInfo.ActiveBuffs))
            {
                var buffs = _guildInfo.ActiveBuffs.Split(',');
                var buffX = pos.X + Padding * scale;
                _spriteBatch.DrawString(font, "Buffs: ",
                    new Vector2(buffX, y), _styleProvider.TextSecondary);
                buffX += font.MeasureString("Buffs: ").Width;

                foreach (var buff in buffs)
                {
                    var trimmed = buff.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Display tier indicators
                    var label = trimmed.Contains("tier1") ? "T1"
                              : trimmed.Contains("tier2") ? "T2"
                              : trimmed.Contains("tier3") ? "T3"
                              : trimmed;
                    _spriteBatch.DrawString(font, label + " ",
                        new Vector2(buffX, y), BuffActiveColor);
                    buffX += font.MeasureString(label + " ").Width;
                }
            }
        }
    }
}
