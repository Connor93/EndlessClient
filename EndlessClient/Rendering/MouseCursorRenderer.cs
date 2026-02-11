using System;
using System.Diagnostics;
using System.Linq;
using EndlessClient.Dialogs;
using EndlessClient.HUD;
using EndlessClient.Input;
using EOLib.Config;
using EOLib.Domain.Item;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO.Map;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Optional;
using XNAControls;

namespace EndlessClient.Rendering
{
    public class MouseCursorRenderer : XNAControl, IMouseCursorRenderer
    {
        private enum CursorState
        {
            Hidden,
            Standard,
            HoverInteractive,
            HoverItem
        }

        private const int TileWidth = IGridDrawCoordinateCalculator.DefaultGridWidth;
        private const int TileHeight = IGridDrawCoordinateCalculator.DefaultGridHeight;

        private readonly IGridDrawCoordinateCalculator _gridDrawCoordinateCalculator;
        private readonly IMapCellStateProvider _mapCellStateProvider;
        private readonly IItemStringService _itemStringService;
        private readonly IItemNameColorService _itemNameColorService;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly IActiveDialogProvider _activeDialogProvider;
        private readonly IContextMenuProvider _contextMenuProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        private readonly XNALabel _mapItemText;

        private Texture2D _whitePixel;
        private int _gridX, _gridY;
        private CursorState _cursorState;

        private Option<Stopwatch> _startClickTime;
        private int _clickAlpha;
        private Option<MapCoordinate> _clickCoordinate;

        public MapCoordinate GridCoordinates => new MapCoordinate(_gridX, _gridY);

        private static readonly Color StandardOutline = Color.FromNonPremultiplied(255, 255, 255, 210);
        private static readonly Color StandardFill = Color.FromNonPremultiplied(255, 255, 255, 20);
        private static readonly Color InteractiveOutline = Color.FromNonPremultiplied(200, 255, 200, 160);
        private static readonly Color InteractiveFill = Color.FromNonPremultiplied(200, 255, 200, 30);
        private static readonly Color ItemOutline = Color.FromNonPremultiplied(255, 215, 0, 180);
        private static readonly Color ItemFill = Color.FromNonPremultiplied(255, 215, 0, 50);

        public MouseCursorRenderer(IGridDrawCoordinateCalculator gridDrawCoordinateCalculator,
                                   IMapCellStateProvider mapCellStateProvider,
                                   IItemStringService itemStringService,
                                   IItemNameColorService itemNameColorService,
                                   IEIFFileProvider eifFileProvider,
                                   ICurrentMapProvider currentMapProvider,
                                   IUserInputProvider userInputProvider,
                                   IActiveDialogProvider activeDialogProvider,
                                   IContextMenuProvider contextMenuProvider,
                                   IConfigurationProvider configurationProvider,
                                   IClientWindowSizeProvider clientWindowSizeProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _gridDrawCoordinateCalculator = gridDrawCoordinateCalculator;
            _mapCellStateProvider = mapCellStateProvider;
            _itemStringService = itemStringService;
            _itemNameColorService = itemNameColorService;
            _eifFileProvider = eifFileProvider;
            _currentMapProvider = currentMapProvider;
            _userInputProvider = userInputProvider;
            _activeDialogProvider = activeDialogProvider;
            _contextMenuProvider = contextMenuProvider;
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;

            DrawArea = new Rectangle(0, 0, TileWidth, TileHeight);

            _mapItemText = new XNALabel(Constants.FontSize09)
            {
                Visible = false,
                Text = string.Empty,
                ForeColor = Color.White,
                AutoSize = false,
                DrawOrder = 10
            };

            _clickCoordinate = Option.None<MapCoordinate>();
        }

        public override void Initialize()
        {
            _whitePixel = new Texture2D(_graphicsDeviceProvider.GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            _mapItemText.AddControlToDefaultGame();
        }

        #region Update and Helpers

        public override void Update(GameTime gameTime)
        {
            if (!ShouldUpdate() || _activeDialogProvider.ActiveDialogs.Any(x => x.HasValue) ||
                _contextMenuProvider.ContextMenu.HasValue)
                return;

            var mousePos = _userInputProvider.CurrentMouseState.Position.ToVector2();
            var zoom = _configurationProvider.MapZoom;
            if (zoom != 1.0f)
            {
                var centerX = _clientWindowSizeProvider.Width / 2f;
                var centerY = _clientWindowSizeProvider.Height / 2f;
                mousePos = new Vector2(
                    (mousePos.X - centerX) / zoom + centerX,
                    (mousePos.Y - centerY) / zoom + centerY);
            }

            var gridPosition = _gridDrawCoordinateCalculator.CalculateGridCoordinatesFromDrawLocation(mousePos);
            _gridX = gridPosition.X;
            _gridY = gridPosition.Y;

            UpdateDrawPositionBasedOnGridPosition();

            var cellState = _mapCellStateProvider.GetCellStateAt(_gridX, _gridY);
            UpdateCursorState(cellState);
        }

        private void UpdateDrawPositionBasedOnGridPosition()
        {
            var drawPosition = _gridDrawCoordinateCalculator.CalculateBaseLayerDrawCoordinatesFromGridUnits(_gridX, _gridY);
            DrawArea = new Rectangle((int)drawPosition.X,
                                      (int)drawPosition.Y,
                                      DrawArea.Width,
                                      DrawArea.Height);
        }

        private void UpdateCursorState(IMapCellState cellState)
        {
            _cursorState = CursorState.Standard;

            if (cellState.Character.HasValue || cellState.NPC.HasValue)
                _cursorState = CursorState.HoverInteractive;
            else if (cellState.Sign.HasValue)
                _cursorState = CursorState.Hidden;
            else if (cellState.Items.Any())
            {
                _cursorState = CursorState.HoverItem;
                UpdateMapItemLabel(Option.Some(cellState.Items.First()));
            }
            else if (cellState.TileSpec != TileSpec.None)
                UpdateCursorStateForTileSpec(cellState.TileSpec);

            if (!cellState.Items.Any())
                UpdateMapItemLabel(Option.None<MapItem>());

            if (_mapItemText.Visible)
            {
                var labelX = DrawArea.X + 32 - _mapItemText.ActualWidth / 2f;
                var labelY = DrawArea.Y - _mapItemText.ActualHeight - 4;

                var zoom = _configurationProvider.MapZoom;
                if (zoom != 1.0f)
                {
                    var centerX = _clientWindowSizeProvider.Width / 2f;
                    var centerY = _clientWindowSizeProvider.Height / 2f;
                    labelX = (DrawArea.X + 32 - centerX) * zoom + centerX - _mapItemText.ActualWidth / 2f;
                    labelY = (DrawArea.Y - _mapItemText.ActualHeight - 4 - centerY) * zoom + centerY;
                }

                _mapItemText.DrawPosition = new Vector2(labelX, labelY);
            }

            _startClickTime.MatchSome(st =>
                {
                    _clickAlpha -= 5;

                    if (_clickAlpha <= 0 || st.ElapsedMilliseconds > 600)
                    {
                        _startClickTime = Option.None<Stopwatch>();
                        _clickCoordinate = Option.None<MapCoordinate>();
                        _clickAlpha = 0;
                    }
                });
        }

        private void UpdateMapItemLabel(Option<MapItem> item)
        {
            item.Match(
                some: i =>
                {
                    var data = _eifFileProvider.EIFFile[i.ItemID];
                    var text = _itemStringService.GetStringForMapDisplay(data, i.Amount);

                    if (!_mapItemText.Visible || _mapItemText.Text != text)
                    {
                        _mapItemText.Visible = true;
                        _mapItemText.Text = text;
                        _mapItemText.ResizeBasedOnText();
                        _mapItemText.ForeColor = _itemNameColorService.GetColorForMapDisplay(data);
                    }
                },
                none: () =>
                {
                    _mapItemText.Visible = false;
                    _mapItemText.Text = string.Empty;
                });
        }

        private void UpdateCursorStateForTileSpec(TileSpec tileSpec)
        {
            switch (tileSpec)
            {
                case TileSpec.Wall:
                case TileSpec.MapEdge:
                case TileSpec.FakeWall:
                case TileSpec.VultTypo:
                    _cursorState = CursorState.Hidden;
                    break;
                case TileSpec.Chest:
                case TileSpec.BankVault:
                case TileSpec.ChairDown:
                case TileSpec.ChairLeft:
                case TileSpec.ChairRight:
                case TileSpec.ChairUp:
                case TileSpec.ChairDownRight:
                case TileSpec.ChairUpLeft:
                case TileSpec.ChairAll:
                case TileSpec.Board1:
                case TileSpec.Board2:
                case TileSpec.Board3:
                case TileSpec.Board4:
                case TileSpec.Board5:
                case TileSpec.Board6:
                case TileSpec.Board7:
                case TileSpec.Board8:
                case TileSpec.Jukebox:
                    _cursorState = CursorState.HoverInteractive;
                    break;
                case TileSpec.NPCBoundary:
                case TileSpec.Jump:
                case TileSpec.Water:
                case TileSpec.Arena:
                case TileSpec.AmbientSource:
                case TileSpec.SpikesStatic:
                case TileSpec.SpikesTrap:
                case TileSpec.SpikesTimed:
                case TileSpec.None:
                    _cursorState = CursorState.Standard;
                    break;
                default:
                    _cursorState = CursorState.HoverInteractive;
                    break;
            }
        }

        #endregion

        public void Draw(SpriteBatch spriteBatch, Vector2 additionalOffset)
        {
            if (_contextMenuProvider.ContextMenu.HasValue || _whitePixel == null)
                return;

            if (_cursorState != CursorState.Hidden && _gridX >= 0 && _gridY >= 0 &&
                _gridX <= _currentMapProvider.CurrentMap.Properties.Width &&
                _gridY <= _currentMapProvider.CurrentMap.Properties.Height)
            {
                var origin = DrawPosition + additionalOffset;

                switch (_cursorState)
                {
                    case CursorState.Standard:
                        DrawDiamond(spriteBatch, origin, StandardFill, StandardOutline);
                        break;
                    case CursorState.HoverInteractive:
                        DrawDiamond(spriteBatch, origin, InteractiveFill, InteractiveOutline);
                        break;
                    case CursorState.HoverItem:
                        DrawDiamond(spriteBatch, origin, ItemFill, ItemOutline);
                        break;
                }
            }

            if (_startClickTime.HasValue && _clickAlpha > 0)
            {
                _clickCoordinate.MatchSome(c =>
                {
                    var position = _gridDrawCoordinateCalculator.CalculateBaseLayerDrawCoordinatesFromGridUnits(c);
                    var clickFill = Color.FromNonPremultiplied(255, 255, 255, Math.Min(_clickAlpha / 3, 60));
                    var clickOutline = Color.FromNonPremultiplied(255, 255, 255, _clickAlpha);
                    DrawDiamond(spriteBatch, position + additionalOffset, clickFill, clickOutline);
                });
            }
        }

        private void DrawDiamond(SpriteBatch spriteBatch, Vector2 origin, Color fillColor, Color outlineColor)
        {
            // Diamond vertices relative to origin (top-left of tile):
            //   Top:    (32, 0)
            //   Right:  (64, 16)
            //   Bottom: (32, 32)
            //   Left:   (0, 16)
            //
            // Draw scanline-by-scanline for smooth edges
            var hasFill = fillColor.A > 0;

            for (var y = 0; y < TileHeight; y++)
            {
                int halfWidth;
                if (y < TileHeight / 2)
                    halfWidth = y * 2; // top half: expands by 2px per row
                else
                    halfWidth = (TileHeight - 1 - y) * 2; // bottom half: contracts

                if (halfWidth <= 0)
                {
                    // Tip pixel (top and bottom)
                    spriteBatch.Draw(_whitePixel,
                        new Rectangle((int)origin.X + TileWidth / 2, (int)origin.Y + y, 1, 1),
                        outlineColor);
                    continue;
                }

                var xLeft = TileWidth / 2 - halfWidth;
                var xRight = TileWidth / 2 + halfWidth;
                var drawY = (int)origin.Y + y;

                // Left edge pixel
                spriteBatch.Draw(_whitePixel,
                    new Rectangle((int)origin.X + xLeft, drawY, 1, 1),
                    outlineColor);

                // Right edge pixel
                spriteBatch.Draw(_whitePixel,
                    new Rectangle((int)origin.X + xRight, drawY, 1, 1),
                    outlineColor);

                // Fill between edges (if any)
                if (hasFill && xRight - xLeft > 1)
                {
                    spriteBatch.Draw(_whitePixel,
                        new Rectangle((int)origin.X + xLeft + 1, drawY, xRight - xLeft - 1, 1),
                        fillColor);
                }
            }
        }

        public void AnimateClick()
        {
            if (_startClickTime.HasValue)
                return;

            _startClickTime = Option.Some(Stopwatch.StartNew());
            _clickAlpha = 200;
            _clickCoordinate = Option.Some(new MapCoordinate(_gridX, _gridY));
        }

        public void ClearTransientRenderables()
        {
            _mapItemText.Visible = false;
            _startClickTime = Option.None<Stopwatch>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spriteBatch.Dispose();
                _mapItemText.Dispose();
                _whitePixel?.Dispose();
            }
        }
    }

    public interface IMouseCursorRenderer : IXNAControl, IDisposable
    {
        MapCoordinate GridCoordinates { get; }

        void Draw(SpriteBatch spriteBatch, Vector2 additionalOffset);

        void AnimateClick();

        void ClearTransientRenderables();
    }
}
