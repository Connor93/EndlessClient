using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.Rendering.NPC;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Rendering
{
    public class BossHealthBarHUD : DrawableGameComponent
    {
        private const int MaxDisplayed = 6;
        private const int MaxColumns = 3;
        private const int BarHeight = 20;
        private const int BarPadding = 4;
        private const int TopMargin = 18;
        private const int NameMargin = 2;
        private const float WidthFraction = 0.75f;

        private static readonly Color PlateColor = new Color(20, 20, 20, 200);
        private static readonly Color BarFillColor = new Color(200, 35, 35);
        private static readonly Color BarBorderColor = new Color(60, 60, 60);
        private static readonly Color NameColor = new Color(255, 160, 60);

        private readonly IBossHealthBarProvider _bossBarProvider;
        private readonly INPCRendererProvider _npcRendererProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IContentProvider _contentProvider;

        private SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;
        private BitmapFont _nameFont;
        private BitmapFont _hpFont;

        public BossHealthBarHUD(IEndlessGameProvider endlessGameProvider,
                                IBossHealthBarProvider bossBarProvider,
                                INPCRendererProvider npcRendererProvider,
                                IENFFileProvider enfFileProvider,
                                IClientWindowSizeProvider clientWindowSizeProvider,
                                IContentProvider contentProvider)
            : base((Game)endlessGameProvider.Game)
        {
            _bossBarProvider = bossBarProvider;
            _npcRendererProvider = npcRendererProvider;
            _enfFileProvider = enfFileProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _contentProvider = contentProvider;

            DrawOrder = 100;
        }

        public override void Initialize()
        {
            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
            _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            _nameFont = _contentProvider.Fonts[Constants.FontSize08];
            _hpFont = _contentProvider.Fonts[Constants.FontSize08];

            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            var visibleBosses = GetVisibleBosses();
            if (visibleBosses.Count == 0)
                return;

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            var windowWidth = _clientWindowSizeProvider.Width;
            var totalWidth = (int)(windowWidth * WidthFraction);
            var startX = (windowWidth - totalWidth) / 2;

            var count = Math.Min(visibleBosses.Count, MaxDisplayed);
            var columns = count <= MaxColumns ? count : (int)Math.Ceiling(count / 2.0);
            var rows = count <= MaxColumns ? 1 : 2;
            var barWidth = (totalWidth - (columns - 1) * BarPadding) / columns;

            var nameHeight = (int)_nameFont.MeasureString("A").Height;
            var rowHeight = nameHeight + NameMargin + BarHeight + BarPadding;

            for (int i = 0; i < count; i++)
            {
                var boss = visibleBosses[i];

                int row, col;
                if (rows == 1)
                {
                    row = 0;
                    col = i;
                }
                else
                {
                    // For 2 rows: distribute evenly
                    var topRowCount = (int)Math.Ceiling(count / 2.0);
                    if (i < topRowCount)
                    {
                        row = 0;
                        col = i;
                        // Center the top row if it has more items
                        var topRowWidth = topRowCount * barWidth + (topRowCount - 1) * BarPadding;
                        startX = (windowWidth - topRowWidth) / 2;
                    }
                    else
                    {
                        row = 1;
                        col = i - topRowCount;
                        var bottomRowCount = count - topRowCount;
                        var bottomRowWidth = bottomRowCount * barWidth + (bottomRowCount - 1) * BarPadding;
                        startX = (windowWidth - bottomRowWidth) / 2;
                    }
                }

                var x = startX + col * (barWidth + BarPadding);
                var y = TopMargin + row * rowHeight;

                DrawBossBar(boss, x, y, barWidth, nameHeight);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawBossBar(BossBarState boss, int x, int y, int barWidth, int nameHeight)
        {
            // Boss name above bar
            var nameSize = _nameFont.MeasureString(boss.Name);
            var nameX = (float)Math.Round(x + (barWidth - nameSize.Width) / 2f);
            var nameY = (float)Math.Round((double)y);

            // Name backdrop
            var nameBackdropRect = new Rectangle(
                (int)nameX - 4, (int)nameY - 1,
                (int)nameSize.Width + 8, (int)nameSize.Height + 2);
            _spriteBatch.Draw(_pixelTexture, nameBackdropRect, new Color(0, 0, 0, 160));
            _spriteBatch.DrawString(_nameFont, boss.Name, new Vector2(nameX, nameY), NameColor);

            // Bar background
            var barY = y + nameHeight + NameMargin;
            var barRect = new Rectangle(x, barY, barWidth, BarHeight);
            _spriteBatch.Draw(_pixelTexture, barRect, PlateColor);

            // Bar border
            DrawRectBorder(barRect, BarBorderColor);

            // Health fill
            var fillWidth = (int)Math.Round(boss.PercentHealth / 100.0 * (barWidth - 4));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(x + 2, barY + 2, fillWidth, BarHeight - 4);
                _spriteBatch.Draw(_pixelTexture, fillRect, BarFillColor);

                // Subtle highlight on top half
                var highlightRect = new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, fillRect.Height / 2);
                _spriteBatch.Draw(_pixelTexture, highlightRect, new Color(255, 255, 255, 30));
            }

            // HP percentage text
            var hpText = $"{boss.PercentHealth}%";
            var hpSize = _hpFont.MeasureString(hpText);
            var hpX = (float)Math.Round(x + (barWidth - hpSize.Width) / 2f);
            var hpY = (float)Math.Round(barY + (BarHeight - hpSize.Height) / 2f);
            DrawOutlinedString(_hpFont, hpText, new Vector2(hpX, hpY), Color.White, Color.Black);
        }

        private List<BossBarState> GetVisibleBosses()
        {
            var result = new List<BossBarState>();
            var viewport = Game.GraphicsDevice.Viewport;
            var screenBounds = new Rectangle(0, 0, viewport.Width, viewport.Height);

            // Scan all NPC renderers for visible bosses (not just damaged ones)
            foreach (var kvp in _npcRendererProvider.NPCRenderers)
            {
                var npcIndex = kvp.Key;
                var renderer = kvp.Value;

                if (renderer.DrawArea.Width <= 0 || !screenBounds.Intersects(renderer.DrawArea))
                    continue;

                var npcData = _enfFileProvider.ENFFile[renderer.NPC.ID];
                if (npcData.Boss <= 0)
                    continue;

                // Use tracked health if available, otherwise infer from alive state
                if (_bossBarProvider.ActiveBosses.TryGetValue(npcIndex, out var tracked))
                {
                    result.Add(tracked);
                }
                else
                {
                    result.Add(new BossBarState
                    {
                        NpcIndex = npcIndex,
                        NpcId = renderer.NPC.ID,
                        Name = npcData.Name,
                        PercentHealth = renderer.IsAlive ? 100 : 0
                    });
                }

                if (result.Count >= MaxDisplayed)
                    break;
            }

            return result;
        }

        private void DrawRectBorder(Rectangle rect, Color color)
        {
            // Top
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            // Bottom
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            // Left
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            // Right
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        private void DrawOutlinedString(BitmapFont font, string text, Vector2 pos, Color color, Color outlineColor)
        {
            _spriteBatch.DrawString(font, text, pos + new Vector2(-1, 0), outlineColor);
            _spriteBatch.DrawString(font, text, pos + new Vector2(1, 0), outlineColor);
            _spriteBatch.DrawString(font, text, pos + new Vector2(0, -1), outlineColor);
            _spriteBatch.DrawString(font, text, pos + new Vector2(0, 1), outlineColor);
            _spriteBatch.DrawString(font, text, pos, color);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spriteBatch?.Dispose();
                _pixelTexture?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
