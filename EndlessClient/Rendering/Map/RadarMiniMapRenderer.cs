using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.Rendering.Factories;
using EOLib;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Domain.NPC;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Map;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Rendering.Map
{
    public class RadarMiniMapRenderer : DrawableGameComponent
    {
        // Panel dimensions
        private const int PanelWidth = 240;
        private const int PanelHeight = 240;
        private const int PanelMargin = 10;
        private const int HeaderHeight = 18;

        // Tile sizes in pixels on the minimap (isometric diamond)
        private const int TileW = 14; // width of one tile diamond
        private const int TileH = 7;  // height of one tile diamond

        // Entity dot sizes
        private const int PlayerDotSize = 6;
        private const int OtherPlayerDotSize = 4;
        private const int NpcDotSize = 3;
        private const int BossDotSize = 5;

        // Colors
        private static readonly Color PanelBackground = new Color(15, 15, 20, 210);
        private static readonly Color PanelBorder = new Color(60, 65, 80, 230);
        private static readonly Color HeaderColor = new Color(25, 28, 35, 230);

        private static readonly Color WalkableTileColor = new Color(35, 40, 50);
        private static readonly Color WallTileColor = new Color(140, 145, 160);
        private static readonly Color WarpTileColor = new Color(40, 80, 180);
        private static readonly Color DoorTileColor = new Color(70, 120, 200);
        private static readonly Color WaterTileColor = new Color(25, 55, 120);
        private static readonly Color ChestTileColor = new Color(170, 140, 50);

        private static readonly Color MainPlayerColor = Color.White;
        private static readonly Color OtherPlayerColor = new Color(60, 220, 100);
        private static readonly Color FriendlyNpcColor = new Color(80, 200, 220);
        private static readonly Color EnemyNpcColor = new Color(220, 60, 60);
        private static readonly Color BossNpcColor = new Color(255, 160, 50);

        private static readonly Color DirectionLabelColor = new Color(140, 145, 160, 180);

        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly ICurrentMapStateRepository _currentMapStateRepository;
        private readonly ICharacterProvider _characterProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IRenderTargetFactory _renderTargetFactory;

        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private BitmapFont _font;
        private RenderTarget2D _terrainCache;

        private IReadOnlyList<int> _lastMapChecksum;
        private int _panelX, _panelY;

        // Drag state
        private bool _isDragging;
        private int _dragOffsetX, _dragOffsetY;
        private bool _hasCustomPosition;
        private bool _wasLeftButtonPressed;

        public RadarMiniMapRenderer(IEndlessGameProvider endlessGameProvider,
                                    ICurrentMapProvider currentMapProvider,
                                    ICurrentMapStateRepository currentMapStateRepository,
                                    ICharacterProvider characterProvider,
                                    IENFFileProvider enfFileProvider,
                                    IClientWindowSizeProvider clientWindowSizeProvider,
                                    IContentProvider contentProvider,
                                    IRenderTargetFactory renderTargetFactory)
            : base((Game)endlessGameProvider.Game)
        {
            _currentMapProvider = currentMapProvider;
            _currentMapStateRepository = currentMapStateRepository;
            _characterProvider = characterProvider;
            _enfFileProvider = enfFileProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _contentProvider = contentProvider;
            _renderTargetFactory = renderTargetFactory;

            DrawOrder = 105; // Above BossHealthBarHUD (100)
        }

        public override void Initialize()
        {
            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
            _pixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _font = _contentProvider.Fonts[Constants.FontSize08];

            _clientWindowSizeProvider.GameWindowSizeChanged += (_, __) =>
            {
                if (!_hasCustomPosition)
                    UpdateDefaultPosition();
            };

            UpdateDefaultPosition();

            base.Initialize();
        }

        private void UpdateDefaultPosition()
        {
            _panelX = _clientWindowSizeProvider.GameWidth - PanelWidth - PanelMargin;
            _panelY = PanelMargin;
        }

        public override void Update(GameTime gameTime)
        {
            if (!_currentMapStateRepository.ShowMiniMap)
            {
                _currentMapStateRepository.MouseOverMiniMap = false;
                base.Update(gameTime);
                return;
            }

            // Rebuild terrain cache if map changed
            var checksum = _currentMapProvider.CurrentMap.Properties.Checksum;
            if (_lastMapChecksum == null || !_lastMapChecksum.SequenceEqual(checksum))
            {
                RebuildTerrainCache();
                _lastMapChecksum = checksum;
            }

            // Handle dragging via mouse state
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;

            // Transform mouse position from window space to game-logical space
            var mouseX = (int)((mouseState.X - offset.X) / scale);
            var mouseY = (int)((mouseState.Y - offset.Y) / scale);

            bool mouseInPanel = mouseX >= _panelX && mouseX < _panelX + PanelWidth
                             && mouseY >= _panelY && mouseY < _panelY + PanelHeight;

            _currentMapStateRepository.MouseOverMiniMap = mouseInPanel;

            bool leftPressed = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            bool leftJustPressed = leftPressed && !_wasLeftButtonPressed;

            if (leftPressed)
            {
                // Only start a new drag if the button was just pressed while inside the panel
                if (!_isDragging && leftJustPressed && mouseInPanel)
                {
                    _isDragging = true;
                    _dragOffsetX = mouseX - _panelX;
                    _dragOffsetY = mouseY - _panelY;
                }

                if (_isDragging)
                {
                    _panelX = mouseX - _dragOffsetX;
                    _panelY = mouseY - _dragOffsetY;
                    _hasCustomPosition = true;

                    // Clamp to window bounds
                    _panelX = Math.Max(0, Math.Min(_panelX, _clientWindowSizeProvider.GameWidth - PanelWidth));
                    _panelY = Math.Max(0, Math.Min(_panelY, _clientWindowSizeProvider.GameHeight - PanelHeight));
                }
            }
            else
            {
                _isDragging = false;
            }

            _wasLeftButtonPressed = leftPressed;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!_currentMapStateRepository.ShowMiniMap || _terrainCache == null)
            {
                base.Draw(gameTime);
                return;
            }

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw panel background
            DrawFilledRect(_panelX, _panelY, PanelWidth, PanelHeight, PanelBackground);

            // Draw header bar
            DrawFilledRect(_panelX, _panelY, PanelWidth, HeaderHeight, HeaderColor);

            // Draw header text
            var mapName = _currentMapProvider.CurrentMap.Properties.Name ?? "Map";
            var nameSize = _font.MeasureString(mapName);
            var nameX = (int)Math.Round(_panelX + (PanelWidth - nameSize.Width) / 2);
            var nameY = (int)Math.Round(_panelY + 2.0);
            _spriteBatch.DrawString(_font, mapName,
                new Vector2(nameX, nameY),
                Color.White);

            // Calculate the viewport area (below header)
            var viewX = _panelX + 1;
            var viewY = _panelY + HeaderHeight;
            var viewW = PanelWidth - 2;
            var viewH = PanelHeight - HeaderHeight - 1;

            // Set up scissor rect to clip terrain to the viewport
            _spriteBatch.End();

            var savedScissor = Game.GraphicsDevice.ScissorRectangle;
            var savedRasterizerState = Game.GraphicsDevice.RasterizerState;

            // Scissor rect in game-logical coordinates (Draw runs inside the game render target)
            var scissorRect = new Rectangle(viewX, viewY, viewW, viewH);

            var rasterizerState = new RasterizerState { ScissorTestEnable = true };
            Game.GraphicsDevice.ScissorRectangle = scissorRect;

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: rasterizerState);

            // Calculate where to draw the terrain cache so the player is centered in the viewport
            var playerX = _characterProvider.MainCharacter.X;
            var playerY = _characterProvider.MainCharacter.Y;

            // Convert player position to isometric pixel coordinates within the terrain cache
            // Must include the same row offset used in RebuildTerrainCache
            var rows = _currentMapProvider.CurrentMap.Properties.Height;
            var playerIsoX = (playerX - playerY) * (TileW / 2) + (rows * TileW / 2);
            var playerIsoY = (playerX + playerY) * (TileH / 2);

            // Center the terrain cache so that the player's position appears at the center of the viewport
            var viewCenterX = viewX + viewW / 2;
            var viewCenterY = viewY + viewH / 2;

            var terrainDrawX = viewCenterX - playerIsoX;
            var terrainDrawY = viewCenterY - playerIsoY;

            // Draw the pre-rendered terrain
            _spriteBatch.Draw(_terrainCache, new Vector2(terrainDrawX, terrainDrawY), Color.White);

            // Draw entities
            DrawEntities(viewCenterX, viewCenterY, playerX, playerY);

            _spriteBatch.End();

            // Restore scissor state
            Game.GraphicsDevice.ScissorRectangle = savedScissor;
            Game.GraphicsDevice.RasterizerState = savedRasterizerState;

            // Draw panel border and cardinal directions on top (no clipping)
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            DrawRectBorder(_panelX, _panelY, PanelWidth, PanelHeight, PanelBorder);
            DrawRectBorder(_panelX, _panelY, PanelWidth, HeaderHeight, PanelBorder);

            // Cardinal direction labels (isometric: N is top-right, E is bottom-right, etc.)
            DrawCenteredText("N", viewX + viewW - 12, viewY + 2, DirectionLabelColor);
            DrawCenteredText("S", viewX + 4, viewY + viewH - 12, DirectionLabelColor);
            DrawCenteredText("W", viewX + 4, viewY + 2, DirectionLabelColor);
            DrawCenteredText("E", viewX + viewW - 10, viewY + viewH - 12, DirectionLabelColor);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawEntities(int viewCenterX, int viewCenterY, int playerX, int playerY)
        {
            // Draw other characters
            foreach (var character in _currentMapStateRepository.Characters)
            {
                var dx = character.X - playerX;
                var dy = character.Y - playerY;
                var isoX = (dx - dy) * (TileW / 2);
                var isoY = (dx + dy) * (TileH / 2);

                DrawDot(viewCenterX + isoX, viewCenterY + isoY, OtherPlayerDotSize, OtherPlayerColor);
            }

            // Draw NPCs
            foreach (var npc in _currentMapStateRepository.NPCs)
            {
                var dx = npc.X - playerX;
                var dy = npc.Y - playerY;
                var isoX = (dx - dy) * (TileW / 2);
                var isoY = (dx + dy) * (TileH / 2);

                var npcData = _enfFileProvider.ENFFile[npc.ID];
                var npcType = npcData.Type;
                var isBoss = npcData.Boss > 0;

                Color dotColor;
                int dotSize;

                if (isBoss)
                {
                    dotColor = BossNpcColor;
                    dotSize = BossDotSize;
                }
                else if (npcType == NPCType.Aggressive || npcType == NPCType.Passive)
                {
                    dotColor = EnemyNpcColor;
                    dotSize = NpcDotSize;
                }
                else
                {
                    dotColor = FriendlyNpcColor;
                    dotSize = NpcDotSize;
                }

                DrawDot(viewCenterX + isoX, viewCenterY + isoY, dotSize, dotColor);
            }

            // Draw main player last (always on top, always at center)
            DrawPlayerArrow(viewCenterX, viewCenterY,
                _characterProvider.MainCharacter.RenderProperties.Direction);
        }

        private void DrawPlayerArrow(int cx, int cy, EODirection direction)
        {
            // Draw a small directional chevron at center
            // The arrow points in the player's facing direction (mapped to isometric axes)
            int arrowSize = 4;

            // White center dot
            DrawDot(cx, cy, PlayerDotSize, MainPlayerColor);

            // Direction indicator line extending from center
            int dx = 0, dy = 0;
            switch (direction)
            {
                case EODirection.Down: dx = -1; dy = 1; break;
                case EODirection.Left: dx = -1; dy = -1; break;
                case EODirection.Up: dx = 1; dy = -1; break;
                case EODirection.Right: dx = 1; dy = 1; break;
            }

            // Draw a small direction tick line
            for (int i = 1; i <= arrowSize; i++)
            {
                var px = cx + dx * i * (TileW / 2);
                var py = cy + dy * i * (TileH / 2);
                var alpha = (byte)Math.Max(0, 255 - i * 40);
                DrawFilledRect(px, py, 2, 2, new Color((byte)255, (byte)255, (byte)255, alpha));
            }
        }

        private void RebuildTerrainCache()
        {
            var map = _currentMapProvider.CurrentMap;
            var rows = map.Properties.Height;
            var cols = map.Properties.Width;

            // Calculate the size of the isometric terrain texture
            // In isometric projection, the bounding box is:
            //   width  = (cols + rows) * TileW / 2
            //   height = (cols + rows) * TileH / 2
            var texW = (cols + rows) * TileW / 2 + TileW;
            var texH = (cols + rows) * TileH / 2 + TileH;

            if (texW <= 0 || texH <= 0) return;

            _terrainCache?.Dispose();
            _terrainCache = _renderTargetFactory.CreateRenderTarget(texW, texH);

            var gd = Game.GraphicsDevice;
            var prevTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(_terrainCache);
            gd.Clear(Color.Transparent);

            var sb = new SpriteBatch(gd);
            sb.Begin(samplerState: SamplerState.PointClamp);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var tileSpec = map.Tiles[row, col];

                    if (tileSpec == TileSpec.MapEdge)
                        continue;

                    var color = GetTileColor(tileSpec, map, row, col);

                    // Convert grid (col, row) to isometric pixel position in the cache
                    var isoX = (col - row) * (TileW / 2) + (rows * TileW / 2); // offset so row 0 col 0 isn't negative
                    var isoY = (col + row) * (TileH / 2);

                    // Draw a small diamond for each tile
                    DrawIsoDiamond(sb, isoX, isoY, TileW, TileH, color);
                }
            }

            sb.End();
            sb.Dispose();

            // Restore previous render targets
            if (prevTargets.Length > 0 && prevTargets[0].RenderTarget != null)
                gd.SetRenderTarget((RenderTarget2D)prevTargets[0].RenderTarget);
            else
                gd.SetRenderTarget(null);
        }

        private Color GetTileColor(TileSpec tileSpec, IMapFile map, int row, int col)
        {
            // Check for warps first
            if (map.Warps[row, col] != null)
            {
                var doorType = map.Warps[row, col].DoorType;
                return doorType > 0 ? DoorTileColor : WarpTileColor;
            }

            switch (tileSpec)
            {
                case TileSpec.Wall:
                case TileSpec.FakeWall:
                case TileSpec.VultTypo:
                    return WallTileColor;
                case TileSpec.Water:
                    return WaterTileColor;
                case TileSpec.Chest:
                case TileSpec.BankVault:
                    return ChestTileColor;
                default:
                    return WalkableTileColor;
            }
        }

        private void DrawIsoDiamond(SpriteBatch sb, int cx, int cy, int w, int h, Color color)
        {
            // Draw a filled isometric diamond (small, so we can approximate with horizontal lines)
            int halfW = w / 2;
            int halfH = h / 2;

            for (int dy = -halfH; dy <= halfH; dy++)
            {
                // Width at this scanline
                float t = 1f - Math.Abs(dy) / (float)halfH;
                int lineW = (int)(halfW * t);
                if (lineW < 1) lineW = 1;

                sb.Draw(_pixel, new Rectangle(cx - lineW, cy + dy, lineW * 2, 1), color);
            }
        }

        #region Drawing Helpers

        private void DrawFilledRect(int x, int y, int w, int h, Color color)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), color);
        }

        private void DrawRectBorder(int x, int y, int w, int h, Color color)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, 1), color);           // top
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + h - 1, w, 1), color);   // bottom
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, h), color);           // left
            _spriteBatch.Draw(_pixel, new Rectangle(x + w - 1, y, 1, h), color);   // right
        }

        private void DrawDot(int cx, int cy, int size, Color color)
        {
            int half = size / 2;
            _spriteBatch.Draw(_pixel, new Rectangle(cx - half, cy - half, size, size), color);
        }

        private void DrawCenteredText(string text, int x, int y, Color color)
        {
            _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _terrainCache?.Dispose();
                _pixel?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
