using System;
using EndlessClient.Content;
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
    /// Code-drawn experience tracker window showing session statistics.
    /// Implements IPostScaleDrawable for crisp text rendering at any scale.
    /// </summary>
    public class CodeDrawnExpTrackerWindow : XNAControl, IZOrderedWindow
    {
        public event Action Activated;
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterSessionProvider _sessionProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;

        private const int WindowWidth = 240;
        private const int WindowHeight = 180;
        private const int HeaderHeight = 24;
        private const int RowHeight = 16;
        private const int Padding = 8;

        public CodeDrawnExpTrackerWindow(
            ICharacterProvider characterProvider,
            ICharacterSessionProvider sessionProvider,
            IExperienceTableProvider experienceTableProvider,
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _characterProvider = characterProvider;
            _sessionProvider = sessionProvider;
            _experienceTableProvider = experienceTableProvider;
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

            Visible = false;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        // IZOrderedWindow implementation
        private int _zOrder = 100;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                DrawFills(DrawPositionWithParentOffset);
            }
            else
            {
                DrawComplete(DrawPositionWithParentOffset, _font, _labelFont);
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

            // Draw title
            var titleText = "Experience Tracker";
            var titleSize = font.MeasureString(titleText);
            var titleX = scaledPos.X + (scaledWidth - titleSize.Width) / 2;
            _spriteBatch.DrawString(font, titleText, new Vector2(titleX, scaledPos.Y + 4 * scale), _styleProvider.TextPrimary);

            // Draw stats
            DrawStatsScaled(scaledPos, scale, font);

            _spriteBatch.End();
        }

        private void DrawComplete(Vector2 pos, BitmapFont font, BitmapFont labelFont)
        {
            _spriteBatch.Begin();

            // Background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, WindowWidth, WindowHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Header line
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X, (int)pos.Y + HeaderHeight, WindowWidth, 1), _styleProvider.PanelBorder);

            // Title
            var titleText = "Experience Tracker";
            var titleSize = labelFont.MeasureString(titleText);
            var titleX = pos.X + (WindowWidth - titleSize.Width) / 2;
            _spriteBatch.DrawString(labelFont, titleText, new Vector2(titleX, pos.Y + 4), _styleProvider.TextPrimary);

            // Stats
            DrawStats(pos, font, labelFont);

            _spriteBatch.End();
        }

        private void DrawStats(Vector2 pos, BitmapFont font, BitmapFont labelFont)
        {
            var stats = _characterProvider.MainCharacter.Stats;
            var level = stats[CharacterStat.Level];
            var exp = stats[CharacterStat.Experience];
            var usage = stats[CharacterStat.Usage];

            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            var startY = pos.Y + HeaderHeight + Padding;
            var labelX = pos.X + Padding;
            var valueX = pos.X + WindowWidth / 2;

            string[] labels = { "Total Exp", "Next Level", "Exp Needed", "Today's Exp", "Total Avg", "Today Avg", "Best Kill", "Last Kill" };
            string[] values = GetStatValues(stats, level, exp, usage);

            for (int i = 0; i < labels.Length; i++)
            {
                var y = startY + i * RowHeight;
                _spriteBatch.DrawString(labelFont, labels[i], new Vector2(labelX, y), labelColor);
                _spriteBatch.DrawString(font, values[i], new Vector2(valueX, y), valueColor);
            }
        }

        private void DrawStatsScaled(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var stats = _characterProvider.MainCharacter.Stats;
            var level = stats[CharacterStat.Level];
            var exp = stats[CharacterStat.Experience];
            var usage = stats[CharacterStat.Usage];

            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            var startY = scaledPos.Y + (HeaderHeight + Padding) * scale;
            var labelX = scaledPos.X + Padding * scale;
            var valueX = scaledPos.X + (WindowWidth / 2) * scale;

            string[] labels = { "Total Exp", "Next Level", "Exp Needed", "Today's Exp", "Total Avg", "Today Avg", "Best Kill", "Last Kill" };
            string[] values = GetStatValues(stats, level, exp, usage);

            for (int i = 0; i < labels.Length; i++)
            {
                var y = startY + i * RowHeight * scale;
                _spriteBatch.DrawString(font, labels[i], new Vector2(labelX, y), labelColor);
                _spriteBatch.DrawString(font, values[i], new Vector2(valueX, y), valueColor);
            }
        }

        private string[] GetStatValues(CharacterStats stats, int level, int exp, int usage)
        {
            var nextLevelExp = _experienceTableProvider.ExperienceByLevel[level + 1];
            var expNeeded = nextLevelExp - exp;
            var todayTotalExp = _sessionProvider.TodayTotalExp;
            var totalAvg = usage > 0 ? (int)(exp / (usage / 60.0)) : 0;
            var sessionMinutes = (int)(DateTime.Now - _sessionProvider.SessionStartTime).TotalMinutes;
            var todayAvg = sessionMinutes > 0 ? (int)(todayTotalExp / (sessionMinutes / 60.0)) : 0;

            return new[]
            {
                $"{exp}",
                $"{nextLevelExp}",
                $"{expNeeded}",
                $"{todayTotalExp}",
                $"{totalAvg}",
                $"{todayAvg}",
                $"{_sessionProvider.BestKillExp}",
                $"{_sessionProvider.LastKillExp}"
            };
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                // Re-center when opened
                DrawArea = new Rectangle(
                    (_clientWindowSizeProvider.Width - WindowWidth) / 2,
                    (_clientWindowSizeProvider.Height - WindowHeight) / 2,
                    WindowWidth,
                    WindowHeight);
            }
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            // Bring to front on click and consume mouse events
            Activated?.Invoke();
            return true;
        }

        public void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
            Activated?.Invoke();
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            return true;
        }
    }
}
