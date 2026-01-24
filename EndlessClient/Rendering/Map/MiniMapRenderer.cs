using System.Collections.Generic;
using System.Linq;
using EOLib;
using EndlessClient.Rendering.Factories;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Map;
using EOLib.IO.Repositories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XNAControls;

namespace EndlessClient.Rendering.Map
{
    public class MiniMapRenderer : XNAControl
    {
        private const int TileWidth = 42;  // Scaled up 1.5x for visibility
        private const int TileHeight = 21;

        // Texture grid is 11 columns × 5 rows, each cell is 27×16 pixels
        private const int GridColumns = 11;
        private const int GridRows = 5;
        private const int CellWidth = 27;
        private const int CellHeight = 16;

        // Grid positions for minimap graphics (column, row)
        // Row 0: Borders and colored diamonds
        private static readonly (int Col, int Row) Border1 = (0, 0);
        private static readonly (int Col, int Row) Border2 = (1, 0);
        private static readonly (int Col, int Row) Border3 = (2, 0);
        private static readonly (int Col, int Row) WallTile = (3, 0);
        private static readonly (int Col, int Row) PlayerBase = (9, 0);  // X10 - base under current player

        // Row 1: Interactive tiles
        private static readonly (int Col, int Row) Warp = (2, 1);     // Y3 - zone transition warps
        private static readonly (int Col, int Row) Chest = (4, 1);
        private static readonly (int Col, int Row) Door = (5, 1);
        private static readonly (int Col, int Row) ChairLeft = (6, 1);
        private static readonly (int Col, int Row) ChairRight = (7, 1);

        // Row 2: Entity markers
        private static readonly (int Col, int Row) ThisPlayer = (4, 2);
        private static readonly (int Col, int Row) Enemy = (5, 2);
        private static readonly (int Col, int Row) NPC = (6, 2);
        private static readonly (int Col, int Row) Boss = (8, 2);

        // Row 3: NPC/Player directional indicators (green boxes)
        private static readonly (int Col, int Row) NPCDown = (4, 3);
        private static readonly (int Col, int Row) NPCLeft = (5, 3);
        private static readonly (int Col, int Row) NPCUp = (6, 3);
        private static readonly (int Col, int Row) NPCRight = (7, 3);

        // Row 4: Enemy directional indicators (red boxes)
        private static readonly (int Col, int Row) EnemyDown = (0, 4);
        private static readonly (int Col, int Row) EnemyLeft = (1, 4);
        private static readonly (int Col, int Row) EnemyUp = (2, 4);
        private static readonly (int Col, int Row) EnemyRight = (3, 4);

        // Legacy enum for edge graphics (still used by GetEdge)
        private enum MiniMapGfx
        {
            UpLine = 0,       // Border1 - thin edge line (up-right diagonal)
            LeftLine = 1,     // Border2 - thin edge line (up-left diagonal)
        }

        private readonly object _rt_locker_ = new object();
        private readonly IRenderTargetFactory _renderTargetFactory;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly ICurrentMapStateProvider _currentMapStateProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IGridDrawCoordinateCalculator _gridDrawCoordinateCalculator;
        private readonly Texture2D _miniMapTexture;

        private RenderTarget2D _miniMapTarget;
        private IReadOnlyList<int> _lastMapChecksum;

        public MiniMapRenderer(INativeGraphicsManager nativeGraphicsManager,
                               IRenderTargetFactory renderTargetFactory,
                               IClientWindowSizeProvider clientWindowSizeProvider,
                               ICurrentMapProvider currentMapProvider,
                               ICurrentMapStateProvider currentMapStateProvider,
                               ICharacterProvider characterProvider,
                               IENFFileProvider enfFileProvider,
                               IGridDrawCoordinateCalculator gridDrawCoordinateCalculator)
        {
            _renderTargetFactory = renderTargetFactory;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _currentMapProvider = currentMapProvider;
            _currentMapStateProvider = currentMapStateProvider;
            _characterProvider = characterProvider;
            _enfFileProvider = enfFileProvider;
            _gridDrawCoordinateCalculator = gridDrawCoordinateCalculator;
            _miniMapTexture = nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 45, true);
        }

        public override void Initialize()
        {
            Visible = true;
            DrawOrder = 0;

            base.Initialize();
        }

        protected override bool ShouldDraw()
        {
            return _currentMapStateProvider.ShowMiniMap && base.ShouldDraw();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (_lastMapChecksum == null || !_lastMapChecksum.SequenceEqual(_currentMapProvider.CurrentMap.Properties.Checksum))
            {
                // The dimensions of the map are 0-based in the properties. Adjust to 1-based for RT creation
                var widthPlus1 = _currentMapProvider.CurrentMap.Properties.Width + 1;
                var heightPlus1 = _currentMapProvider.CurrentMap.Properties.Height + 1;

                lock (_rt_locker_)
                {
                    _miniMapTarget?.Dispose();
                    _miniMapTarget = _renderTargetFactory.CreateRenderTarget(
                        (widthPlus1 + heightPlus1) * TileWidth,
                        (widthPlus1 + heightPlus1) * TileHeight);
                }

                DrawFixedMapElementsToRenderTarget();
            }

            _lastMapChecksum = _currentMapProvider.CurrentMap.Properties.Checksum;

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            lock (_rt_locker_)
            {
                _spriteBatch.Begin();

                var baseTargetDrawLoc = _gridDrawCoordinateCalculator.CalculateGroundLayerRenderTargetDrawCoordinates(isMiniMap: true, TileWidth, TileHeight);
                _spriteBatch.Draw(_miniMapTarget, baseTargetDrawLoc, Color.White);

                var entities = new IMapEntity[] { _characterProvider.MainCharacter }
                    .Concat(_currentMapStateProvider.Characters)
                    .Concat(_currentMapStateProvider.NPCs);

                foreach (var entity in entities)
                {
                    var loc = GetMiniMapDrawCoordinates(entity.X, entity.Y);
                    var (baseMarker, directionIndicator) = GetSourceRectangleForEntity(entity);

                    // Draw direction indicator FIRST (underneath)
                    if (!directionIndicator.IsEmpty)
                    {
                        DrawGridBox(loc, null, directionIndicator);
                    }

                    // Draw entity marker ON TOP, offset up by 4 pixels so it "stands" on the tile
                    var markerOffset = new Vector2(0, -4);
                    DrawGridBox(loc + markerOffset, null, baseMarker);
                }

                _spriteBatch.End();
            }

            base.OnDrawControl(gameTime);
        }

        private (MiniMapGfx? EdgeGfx, Rectangle SourceRect) GetSourceRectangleForGridSpace(int col, int row)
        {
            var tileSpec = _currentMapProvider.CurrentMap.Tiles[row, col];

            switch (tileSpec)
            {
                case TileSpec.Wall:
                case TileSpec.FakeWall:
                    return (GetEdge(), GetSourceRect(WallTile));
                case TileSpec.BankVault:
                case TileSpec.Chest:
                case (TileSpec)8:
                    return (GetEdge(), GetSourceRect(Chest));
                case TileSpec.ChairAll:
                case TileSpec.ChairDown:
                case TileSpec.ChairUp:
                case TileSpec.ChairDownRight:
                case TileSpec.ChairUpLeft:
                    return (GetEdge(), GetSourceRect(ChairLeft)); // Use left chair for generic
                case TileSpec.ChairLeft:
                    return (GetEdge(), GetSourceRect(ChairLeft));
                case TileSpec.ChairRight:
                    return (GetEdge(), GetSourceRect(ChairRight));
                // Unknown TileSpecs 10-15 have been confirmed in the vanilla client to show on the minimap
                case (TileSpec)10:
                case (TileSpec)11:
                case (TileSpec)12:
                case (TileSpec)13:
                case (TileSpec)14:
                case (TileSpec)15:
                    return (GetEdge(), GetSourceRect(Chest)); // Use chest graphic for unknown interactives
                case TileSpec.MapEdge:
                    return (null, Rectangle.Empty);
            }

            if (_currentMapProvider.CurrentMap.Warps[row, col] != null)
            {
                var doorType = _currentMapProvider.CurrentMap.Warps[row, col].DoorType;
                // Door warps use Door graphic, regular zone transitions use Warp graphic
                if (doorType != DoorSpec.NoDoor)
                    return (GetEdge(), GetSourceRect(Door));
                else
                    return (GetEdge(), GetSourceRect(Warp));
            }

            return (GetEdge(), Rectangle.Empty);

            MiniMapGfx? GetEdge()
            {
                if (tileSpec == TileSpec.MapEdge)
                    return null;

                var tiles = _currentMapProvider.CurrentMap.Tiles;

                // Check if this tile is adjacent to a map edge on either side
                var adjacentToLeftEdge = col == 0 || (col - 1 >= 0 && tiles[row, col - 1] == TileSpec.MapEdge);
                var adjacentToTopEdge = row == 0 || (row - 1 >= 0 && tiles[row - 1, col] == TileSpec.MapEdge);

                // Only draw edge graphics for tiles actually at the boundary of the playable area
                if (adjacentToLeftEdge && adjacentToTopEdge)
                    return null; // Corner of map - don't draw anything (would be outside playable area)
                else if (adjacentToLeftEdge)
                    return MiniMapGfx.UpLine;
                else if (adjacentToTopEdge)
                    return MiniMapGfx.LeftLine;
                else
                    return null; // Interior tile - no edge needed
            }
        }

        private (Rectangle BaseMarker, Rectangle DirectionIndicator) GetSourceRectangleForEntity(IMapEntity mapEntity)
        {
            if (_characterProvider.MainCharacter == mapEntity)
            {
                // Main player uses ThisPlayer marker with PlayerBase (X10) underneath
                return (GetSourceRect(ThisPlayer), GetSourceRect(PlayerBase));
            }

            return mapEntity switch
            {
                EOLib.Domain.NPC.NPC n => GetNPCSourceRectangles(n),
                EOLib.Domain.Character.Character c => GetCharacterSourceRectangles(c),
                _ => (Rectangle.Empty, Rectangle.Empty)
            };

            (Rectangle, Rectangle) GetCharacterSourceRectangles(EOLib.Domain.Character.Character character)
            {
                // Other players use NPC graphic with green directional indicator
                var directionRect = character.RenderProperties.Direction switch
                {
                    EODirection.Down => GetSourceRect(NPCDown),
                    EODirection.Left => GetSourceRect(NPCLeft),
                    EODirection.Up => GetSourceRect(NPCUp),
                    EODirection.Right => GetSourceRect(NPCRight),
                    _ => Rectangle.Empty
                };
                return (GetSourceRect(NPC), directionRect);
            }

            (Rectangle, Rectangle) GetNPCSourceRectangles(EOLib.Domain.NPC.NPC npc)
            {
                var npcType = _enfFileProvider.ENFFile[npc.ID].Type;
                var isEnemy = npcType == NPCType.Aggressive || npcType == NPCType.Passive;

                // Get directional indicator based on NPC facing direction
                Rectangle directionRect;
                if (isEnemy)
                {
                    // Enemies use red directional indicators (row 4)
                    directionRect = npc.Direction switch
                    {
                        EODirection.Down => GetSourceRect(EnemyDown),
                        EODirection.Left => GetSourceRect(EnemyLeft),
                        EODirection.Up => GetSourceRect(EnemyUp),
                        EODirection.Right => GetSourceRect(EnemyRight),
                        _ => Rectangle.Empty
                    };
                    return (GetSourceRect(Enemy), directionRect);
                }
                else
                {
                    // NPCs use green directional indicators (row 3)
                    directionRect = npc.Direction switch
                    {
                        EODirection.Down => GetSourceRect(NPCDown),
                        EODirection.Left => GetSourceRect(NPCLeft),
                        EODirection.Up => GetSourceRect(NPCUp),
                        EODirection.Right => GetSourceRect(NPCRight),
                        _ => Rectangle.Empty
                    };
                    return (GetSourceRect(NPC), directionRect);
                }
            }
        }

        private void DrawFixedMapElementsToRenderTarget()
        {
            if (_lastMapChecksum != null && _lastMapChecksum.SequenceEqual(_currentMapProvider.CurrentMap.Properties.Checksum))
                return;

            GraphicsDevice.SetRenderTarget(_miniMapTarget);
            GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 0, 0);
            _spriteBatch.Begin();

            // the height is used to offset the 0 point of the grid, which is TileHeight units per tile in the height of the map
            var height = _currentMapProvider.CurrentMap.Properties.Height + 1;

            for (int row = 0; row <= _currentMapProvider.CurrentMap.Properties.Height; ++row)
            {
                for (int col = 0; col <= _currentMapProvider.CurrentMap.Properties.Width; ++col)
                {
                    var drawLoc = _gridDrawCoordinateCalculator.CalculateRawRenderCoordinatesFromGridUnits(col, row, TileWidth, TileHeight) + new Vector2(TileHeight * height, 0);

                    // Draw grid lines FIRST (underneath tile content)
                    // Border1 = up-right edge, Border2 = up-left edge
                    var tileSpec = _currentMapProvider.CurrentMap.Tiles[row, col];
                    if (tileSpec != TileSpec.MapEdge)
                    {
                        _spriteBatch.Draw(_miniMapTexture, drawLoc, GetSourceRect(Border1), Color.FromNonPremultiplied(255, 255, 255, 128));
                        _spriteBatch.Draw(_miniMapTexture, drawLoc, GetSourceRect(Border2), Color.FromNonPremultiplied(255, 255, 255, 128));
                    }

                    // Draw tile content on top of grid lines
                    var (edgeGfx, miniMapRectSrc) = GetSourceRectangleForGridSpace(col, row);
                    DrawGridBox(drawLoc, edgeGfx, miniMapRectSrc);
                }
            }

            _spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);
        }

        private void DrawGridBox(Vector2 loc, MiniMapGfx? edgeGfx, Rectangle gridSpaceSourceRect)
        {
            // Draw edge graphics (UpLine, LeftLine) for map boundaries
            if (edgeGfx != null)
            {
                var edgeRect = GetSourceRect(edgeGfx.Value);
                _spriteBatch.Draw(_miniMapTexture, loc, edgeRect, Color.FromNonPremultiplied(255, 255, 255, 128));
            }

            // Draw content (walls, interactive tiles, entities) on top
            if (!gridSpaceSourceRect.IsEmpty)
            {
                _spriteBatch.Draw(_miniMapTexture, loc, gridSpaceSourceRect, Color.FromNonPremultiplied(255, 255, 255, 128));
            }
        }

        private Vector2 GetMiniMapDrawCoordinates(int x, int y)
        {
            var widthFactor = _clientWindowSizeProvider.Width / 2;
            var heightFactor = _clientWindowSizeProvider.Resizable
                ? _clientWindowSizeProvider.Height / 2 // 144 = 480 * .45, viewport height factor
                : _clientWindowSizeProvider.Height * 3 / 10 - 2;

            var tileWidthFactor = TileWidth / 2;
            var tileHeightFactor = TileHeight / 2;

            return new Vector2(x * tileWidthFactor - y * tileWidthFactor + widthFactor,
                               y * tileHeightFactor + x * tileHeightFactor + heightFactor) - GetCharacterOffset();
        }

        private Rectangle GetSourceRect(MiniMapGfx gfx)
        {
            // Edge graphics are in row 0
            return GetSourceRect((int)gfx, 0);
        }

        private Rectangle GetSourceRect((int Col, int Row) gridPos)
        {
            return GetSourceRect(gridPos.Col, gridPos.Row);
        }

        private Rectangle GetSourceRect(int col, int row)
        {
            // Calculate source rectangle from 2D grid coordinates
            // Add +2 to Y to account for offset between texture rows
            return new Rectangle(col * CellWidth, row * CellHeight + 2, CellWidth, CellHeight);
        }

        private Vector2 GetCharacterOffset()
        {
            var tileWidthFactor = TileWidth / 2;
            var tileHeightFactor = TileHeight / 2;

            var (cx, cy) = (_characterProvider.MainCharacter.X, _characterProvider.MainCharacter.Y);
            return new Vector2(cx * tileWidthFactor - cy * tileWidthFactor, cx * tileHeightFactor + cy * tileHeightFactor);
        }
    }
}
