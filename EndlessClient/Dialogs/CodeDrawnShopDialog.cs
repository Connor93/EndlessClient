using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Shop;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Grid-based shop dialog with Buy/Sell/Craft tabs, item tiles with price badges,
    /// hover tooltips, and an inline-drawn scrollbar with up/down buttons + draggable thumb.
    /// Modeled on CodeDrawnGridLockerDialog.
    /// </summary>
    public class CodeDrawnShopDialog : XNADialog, IPostScaleDrawable
    {
        private enum ShopTab { Buy, Sell, Craft }

        private readonly IUIStyleProvider _styleProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly IShopActions _shopActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IShopDataProvider _shopDataProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _buttonFont;

        private IReadOnlyList<IShopItem> _buyItems, _sellItems;
        private IReadOnlyList<IShopCraftItem> _craftItems;
        private ShopTab _activeTab = ShopTab.Buy;
        private int _hoveredTileIndex = -1;
        private Option<int> _cachedShopId;
        private HashSet<InventoryItem> _cachedInventory;
        private ulong _tick;

        // Inline scroll state
        private int _scrollOffset;
        private int _totalRows;
        private bool _isDraggingThumb;
        private int _dragStartY;
        private int _dragStartOffset;

        // Layout constants
        private const int DlgWidth = 380;
        private const int DlgHeight = 420;
        private const int TitleBarHeight = 32;
        private const int TabAreaTop = 36;
        private const int TabHeight = 22;
        private const int GridAreaTop = 62;
        private const int GridAreaHeight = 310;
        private const int TileWidth = 64;
        private const int TileHeight = 80; // Taller than locker (72) to fit price badge
        private const int TilePadding = 4;
        private const int GridColumns = 5;
        private const int GridLeftMargin = 8;
        private const int ScrollBarWidth = 16;
        private const int ScrollArrowHeight = 16;

        private int VisibleRows => GridAreaHeight / (TileHeight + TilePadding);

        // Scrollbar geometry (in dialog-local unscaled coords)
        private int ScrollBarLeft => DlgWidth - ScrollBarWidth - 8;
        private int ScrollBarTop => GridAreaTop;
        private int ScrollTrackHeight => GridAreaHeight - ScrollArrowHeight * 2;
        private int MaxScrollOffset => Math.Max(0, _totalRows - VisibleRows);

        public CodeDrawnShopDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager graphicsManager,
            IShopActions shopActions,
            IEOMessageBoxFactory messageBoxFactory,
            IItemTransferDialogFactory itemTransferDialogFactory,
            ILocalizedStringFinder localizedStringFinder,
            IShopDataProvider shopDataProvider,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider,
            ICharacterProvider characterProvider,
            IInventorySpaceValidator inventorySpaceValidator,
            IContentProvider contentProvider)
            : base()
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _graphicsManager = graphicsManager;
            _shopActions = shopActions;
            _messageBoxFactory = messageBoxFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _localizedStringFinder = localizedStringFinder;
            _shopDataProvider = shopDataProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _characterProvider = characterProvider;
            _inventorySpaceValidator = inventorySpaceValidator;
            _contentProvider = contentProvider;

            _font = contentProvider.Fonts[Constants.FontSize08pt5];
            _buttonFont = contentProvider.Fonts[Constants.FontSize10];

            _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);
            _buyItems = new List<IShopItem>();
            _sellItems = new List<IShopItem>();
            _craftItems = new List<IShopCraftItem>();

            DrawArea = new Rectangle(0, 0, DlgWidth, DlgHeight);
            CenterInGameView();
        }

        public override void Initialize()
        {
            if (_graphicsDeviceProvider != null)
                DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            else
                DrawingPrimitives.Initialize(Game.GraphicsDevice);

            base.Initialize();
            CenterInGameView();
        }

        public override void CenterInGameView()
        {
            int centerWidth, centerHeight;
            if (GameViewportProvider != null)
            {
                centerWidth = GameViewportProvider.GameWidth;
                centerHeight = GameViewportProvider.GameHeight;
            }
            else if (Game?.GraphicsDevice != null)
            {
                var viewport = Game.GraphicsDevice.Viewport;
                centerWidth = viewport.Width;
                centerHeight = viewport.Height;
            }
            else return;

            DrawPosition = new Vector2(centerWidth / 2 - DlgWidth / 2,
                                       centerHeight / 2 - DlgHeight / 2);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _cachedShopId.MatchNone(() =>
            {
                _shopDataProvider.SessionID.SomeWhen(x => x > 0)
                    .MatchSome(x =>
                    {
                        _cachedShopId = Option.Some(_shopDataProvider.SessionID);

                        _buyItems = _shopDataProvider.TradeItems.Where(x => x.Buy > 0).ToList();
                        _sellItems = _shopDataProvider.TradeItems
                            .Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0))
                            .ToList();
                        _craftItems = _shopDataProvider.CraftItems;

                        // Default to Buy tab; if no buy items, try Sell
                        _activeTab = _buyItems.Count > 0 ? ShopTab.Buy : ShopTab.Sell;
                        UpdateScrollDimensions();
                    });
            });

            // Periodically refresh sell items when inventory changes
            if (++_tick % 8 == 0 && !_cachedInventory.SetEquals(_characterInventoryProvider.ItemInventory))
            {
                _sellItems = _shopDataProvider.TradeItems
                    .Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0))
                    .ToList();
                _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);

                if (_activeTab == ShopTab.Sell)
                    UpdateScrollDimensions();
            }

            // Handle thumb dragging
            if (_isDraggingThumb)
            {
                var mouseState = Mouse.GetState();
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    _isDraggingThumb = false;
                }
                else
                {
                    var (_, currentLocalY) = ScreenToLocal(mouseState.X, mouseState.Y);
                    var dragDelta = currentLocalY - _dragStartY;

                    if (MaxScrollOffset > 0)
                    {
                        var thumbHeight = GetThumbHeight();
                        var usableTrack = ScrollTrackHeight - thumbHeight;
                        if (usableTrack > 0)
                        {
                            var offsetDelta = (int)((dragDelta / (float)usableTrack) * MaxScrollOffset);
                            _scrollOffset = Math.Clamp(_dragStartOffset + offsetDelta, 0, MaxScrollOffset);
                        }
                    }
                }
            }

            // Hover tracking
            UpdateHoveredTile();

            base.OnUpdateControl(gameTime);
        }

        private void UpdateHoveredTile()
        {
            var mouseState = Mouse.GetState();
            var (localX, localY) = ScreenToLocal(mouseState.X, mouseState.Y);

            _hoveredTileIndex = -1;

            if (localX >= GridLeftMargin && localX < ScrollBarLeft
                && localY >= GridAreaTop && localY < GridAreaTop + GridAreaHeight)
            {
                var gridX = localX - GridLeftMargin;
                var gridY = localY - GridAreaTop;

                var col = gridX / (TileWidth + TilePadding);
                var row = gridY / (TileHeight + TilePadding);

                if (col >= 0 && col < GridColumns)
                {
                    var tileIndex = (_scrollOffset + row) * GridColumns + col;
                    var items = GetActiveItems();

                    if (tileIndex >= 0 && tileIndex < items.Count)
                    {
                        var tileLocalX = gridX - col * (TileWidth + TilePadding);
                        var tileLocalY = gridY - row * (TileHeight + TilePadding);
                        if (tileLocalX < TileWidth && tileLocalY < TileHeight)
                        {
                            _hoveredTileIndex = tileIndex;
                        }
                    }
                }
            }
        }

        // ---- Data Helpers ----

        private List<(int ID, string Name, int Price, EIFRecord Data, object Source)> GetActiveItems()
        {
            return _activeTab switch
            {
                ShopTab.Buy => _buyItems.Select(i =>
                {
                    var data = _eifFileProvider.EIFFile[i.ID];
                    return (i.ID, data.Name, i.Buy, data, (object)i);
                }).ToList(),

                ShopTab.Sell => _sellItems.Select(i =>
                {
                    var data = _eifFileProvider.EIFFile[i.ID];
                    return (i.ID, data.Name, i.Sell, data, (object)i);
                }).ToList(),

                ShopTab.Craft => _craftItems.Select(i =>
                {
                    var data = _eifFileProvider.EIFFile[i.ID];
                    return (i.ID, data.Name, 0, data, (object)i);
                }).ToList(),

                _ => new List<(int, string, int, EIFRecord, object)>()
            };
        }

        private int GetTabItemCount(ShopTab tab) => tab switch
        {
            ShopTab.Buy => _buyItems.Count,
            ShopTab.Sell => _sellItems.Count,
            ShopTab.Craft => _craftItems.Count,
            _ => 0
        };

        private void UpdateScrollDimensions()
        {
            var totalItems = GetActiveItems().Count;
            _totalRows = (int)Math.Ceiling((double)totalItems / GridColumns);
        }

        private int GetThumbHeight()
        {
            if (_totalRows <= VisibleRows)
                return ScrollTrackHeight;
            return Math.Max(20, (int)(ScrollTrackHeight * ((float)VisibleRows / _totalRows)));
        }

        private int GetThumbY()
        {
            if (MaxScrollOffset <= 0) return 0;
            var thumbHeight = GetThumbHeight();
            var usableTrack = ScrollTrackHeight - thumbHeight;
            return (int)(usableTrack * ((float)_scrollOffset / MaxScrollOffset));
        }

        private void ScrollUp(int lines = 1)
        {
            if (_totalRows <= VisibleRows) return;
            _scrollOffset = Math.Max(0, _scrollOffset - lines);
        }

        private void ScrollDown(int lines = 1)
        {
            if (_totalRows <= VisibleRows) return;
            _scrollOffset = Math.Min(MaxScrollOffset, _scrollOffset + lines);
        }

        private (int X, int Y) ScreenToLocal(int screenX, int screenY)
        {
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var drawPos = DrawAreaWithParentOffset;

            var localX = (int)((screenX - offset.X) / scale) - drawPos.X;
            var localY = (int)((screenY - offset.Y) / scale) - drawPos.Y;
            return (localX, localY);
        }



        // ---- Drawing ----

        public int PostScaleDrawOrder => 210;
        public bool SkipRenderTargetDraw => true;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                base.OnDrawControl(gameTime);
                return;
            }

            DrawComplete(DrawAreaWithParentOffset);
            base.OnDrawControl(gameTime);
        }

        public virtual void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            var font = _contentProvider != null
                ? FontScaleHelper.GetScaledFont(_contentProvider, scaleFactor)
                : _font;

            DrawDialog(scaledPos, scaleFactor, font);
        }

        private void DrawComplete(Rectangle drawPos)
        {
            DrawDialog(new Vector2(drawPos.X, drawPos.Y), 1f, _font);
        }

        private void DrawDialog(Vector2 pos, float scale, BitmapFont font)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;

            var panelW = (int)(DlgWidth * scale);
            var panelH = (int)(DlgHeight * scale);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // === Panel Background ===
            var panelRect = new Rectangle((int)pos.X, (int)pos.Y, panelW, panelH);
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, panelRect, _styleProvider.PanelBackground, cornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, panelRect, _styleProvider.PanelBorder, cornerRadius,
                Math.Max(1, (int)(borderThickness * scale)));

            // === Title Bar ===
            var titleRect = new Rectangle(
                (int)(pos.X + borderThickness * scale),
                (int)(pos.Y + borderThickness * scale),
                panelW - (int)(borderThickness * 2 * scale),
                (int)((TitleBarHeight - borderThickness) * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, titleRect, _styleProvider.TitleBarBackground);

            var title = _shopDataProvider.ShopName;
            if (string.IsNullOrEmpty(title)) title = "Shop";
            _spriteBatch.DrawString(font, title,
                new Vector2(pos.X + 12 * scale, pos.Y + 8 * scale), _styleProvider.TitleBarText);

            // === Tabs ===
            DrawTabs(pos, scale, font);

            // === Grid Area Background ===
            var gridBgRect = new Rectangle(
                (int)(pos.X + GridLeftMargin * scale),
                (int)(pos.Y + GridAreaTop * scale),
                (int)((ScrollBarLeft - GridLeftMargin) * scale),
                (int)(GridAreaHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, gridBgRect, _styleProvider.SectionBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, gridBgRect, _styleProvider.PanelBorder, 1);

            // === Grid Items ===
            var items = GetActiveItems();
            var startIndex = _scrollOffset * GridColumns;

            for (int row = 0; row < VisibleRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    var idx = startIndex + row * GridColumns + col;
                    if (idx >= items.Count) break;

                    var item = items[idx];

                    var tileX = (int)(pos.X + (GridLeftMargin + col * (TileWidth + TilePadding)) * scale);
                    var tileY = (int)(pos.Y + (GridAreaTop + row * (TileHeight + TilePadding)) * scale);
                    var tileW = (int)(TileWidth * scale);
                    var tileH = (int)(TileHeight * scale);

                    var isHovered = idx == _hoveredTileIndex;
                    var tileBg = isHovered ? _styleProvider.GridTileHover : _styleProvider.GridTileBackground;

                    // Tile background
                    var tileRect = new Rectangle(tileX, tileY, tileW, tileH);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, tileRect, tileBg);
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, tileRect, _styleProvider.GridTileBorder, 1);

                    // Item icon (centered in top portion of tile)
                    try
                    {
                        var itemIcon = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Data.Graphic - 1, transparent: true);
                        if (itemIcon != null)
                        {
                            var maxIconSize = (int)(40 * scale);
                            var iconW = Math.Min(itemIcon.Width, maxIconSize);
                            var iconH = Math.Min(itemIcon.Height, maxIconSize);

                            if (itemIcon.Width > maxIconSize || itemIcon.Height > maxIconSize)
                            {
                                var ratio = Math.Min((float)maxIconSize / itemIcon.Width, (float)maxIconSize / itemIcon.Height);
                                iconW = (int)(itemIcon.Width * ratio);
                                iconH = (int)(itemIcon.Height * ratio);
                            }

                            var iconX = tileX + (tileW - iconW) / 2;
                            var iconY = tileY + (int)(4 * scale);
                            _spriteBatch.Draw(itemIcon, new Rectangle(iconX, iconY, iconW, iconH), Color.White);
                        }
                    }
                    catch { /* gracefully handle missing graphics */ }

                    // Item name (truncated to fit tile width)
                    var nameText = item.Name;
                    var nameSize = font.MeasureString(nameText);
                    if (nameSize.Width > tileW - 4 * scale)
                    {
                        while (nameText.Length > 1)
                        {
                            nameText = nameText[..^1];
                            if (font.MeasureString(nameText + "..").Width <= tileW - 4 * scale)
                            {
                                nameText += "..";
                                break;
                            }
                        }
                    }
                    var textW = font.MeasureString(nameText).Width;
                    var textX = tileX + (tileW - (int)textW) / 2;
                    var textY = tileY + (int)(46 * scale);
                    _spriteBatch.DrawString(font, nameText, new Vector2(textX, textY), _styleProvider.TextPrimary);

                    // Price badge (or ingredient count for craft tab)
                    string badgeText;
                    if (_activeTab == ShopTab.Craft)
                    {
                        var craftItem = (IShopCraftItem)item.Source;
                        badgeText = $"{craftItem.Ingredients.Count} ingr.";
                    }
                    else
                    {
                        badgeText = FormatGold(item.Price);
                    }

                    var badgeSize = font.MeasureString(badgeText);
                    var badgeX = tileX + (tileW - (int)badgeSize.Width) / 2;
                    var badgeY = tileY + tileH - (int)badgeSize.Height - (int)(2 * scale);

                    var badgeBgRect = new Rectangle(
                        badgeX - (int)(2 * scale), badgeY,
                        (int)badgeSize.Width + (int)(4 * scale), (int)badgeSize.Height);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, badgeBgRect, _styleProvider.OverlayDim);

                    var badgeColor = _activeTab == ShopTab.Craft
                        ? new Color(_styleProvider.GoldColor, 0.70f)  // Muted gold for craft
                        : _styleProvider.GoldColor;   // Gold for prices
                    _spriteBatch.DrawString(font, badgeText, new Vector2(badgeX, badgeY), badgeColor);
                }
            }

            // === Scrollbar ===
            DrawScrollbar(pos, scale, font);

            // === Close Button ===
            DrawCloseButton(pos, scale, font);

            // === Tooltip ===
            if (_hoveredTileIndex >= 0 && _hoveredTileIndex < items.Count)
            {
                var item = items[_hoveredTileIndex];
                DrawTooltip(pos, scale, font, item);
            }

            _spriteBatch.End();
        }

        private static string FormatGold(int amount)
        {
            if (amount >= 1_000_000)
                return $"{amount / 1_000_000f:0.#}M";
            if (amount >= 10_000)
                return $"{amount / 1_000f:0.#}K";
            return amount.ToString("N0");
        }

        private void DrawTabs(Vector2 pos, float scale, BitmapFont font)
        {
            var tabs = new List<(ShopTab Tab, string Label)>
            {
                (ShopTab.Buy, "Buy"),
                (ShopTab.Sell, "Sell")
            };

            if (_craftItems.Count > 0)
                tabs.Add((ShopTab.Craft, "Craft"));

            var totalTabWidth = DlgWidth - GridLeftMargin * 2;
            var tabWidth = totalTabWidth / tabs.Count;

            for (int i = 0; i < tabs.Count; i++)
            {
                var (tab, label) = tabs[i];
                var isActive = _activeTab == tab;
                var count = GetTabItemCount(tab);

                var tabX = (int)(pos.X + (GridLeftMargin + i * tabWidth) * scale);
                var tabY = (int)(pos.Y + TabAreaTop * scale);
                var tabW = (int)(tabWidth * scale);
                var tabH = (int)(TabHeight * scale);

                var tabRect = new Rectangle(tabX, tabY, tabW, tabH);
                var tabColor = isActive ? _styleProvider.TabActive : _styleProvider.TabInactive;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, tabRect, tabColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, tabRect, _styleProvider.PanelBorder, 1);

                var labelText = $"{label}({count})";
                var labelSize = font.MeasureString(labelText);
                var labelX = tabX + (tabW - (int)labelSize.Width) / 2;
                var labelY = tabY + (tabH - (int)labelSize.Height) / 2;

                var textColor = isActive ? _styleProvider.TabText : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(font, labelText, new Vector2(labelX, labelY), textColor);
            }
        }

        private void DrawScrollbar(Vector2 pos, float scale, BitmapFont font)
        {
            var sbX = (int)(pos.X + ScrollBarLeft * scale);
            var sbY = (int)(pos.Y + ScrollBarTop * scale);
            var sbW = (int)(ScrollBarWidth * scale);
            var sbH = (int)(GridAreaHeight * scale);
            var arrowH = (int)(ScrollArrowHeight * scale);

            var trackColor = _styleProvider.PanelBackground;
            var borderColor = _styleProvider.PanelBorder;

            // Track background
            var trackRect = new Rectangle(sbX, sbY, sbW, sbH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, trackRect, trackColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, trackRect, borderColor, 1);

            // Hover detection for arrows
            var mouseState = Mouse.GetState();
            var (localMX, localMY) = ScreenToLocal(mouseState.X, mouseState.Y);
            var isInScrollbar = localMX >= ScrollBarLeft && localMX < ScrollBarLeft + ScrollBarWidth
                             && localMY >= ScrollBarTop && localMY < ScrollBarTop + GridAreaHeight;

            // Up arrow
            var upRect = new Rectangle(sbX, sbY, sbW, arrowH);
            var upHovered = isInScrollbar && localMY < ScrollBarTop + ScrollArrowHeight;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, upRect,
                upHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, upRect, borderColor, 1);
            DrawArrow(_spriteBatch, sbX + sbW / 2, sbY + arrowH / 2, (int)(5 * scale), true,
                upHovered ? _styleProvider.TextHighlight : _styleProvider.TextPrimary);

            // Down arrow
            var downY = sbY + sbH - arrowH;
            var downRect = new Rectangle(sbX, downY, sbW, arrowH);
            var downHovered = isInScrollbar && localMY >= GridAreaTop + GridAreaHeight - ScrollArrowHeight;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, downRect,
                downHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, downRect, borderColor, 1);
            DrawArrow(_spriteBatch, sbX + sbW / 2, downY + arrowH / 2, (int)(5 * scale), false,
                downHovered ? _styleProvider.TextHighlight : _styleProvider.TextPrimary);

            // Thumb
            if (_totalRows > VisibleRows)
            {
                var thumbH = (int)(GetThumbHeight() * scale);
                var thumbY = sbY + arrowH + (int)(GetThumbY() * scale);
                var thumbRect = new Rectangle(sbX + 2, thumbY, sbW - 4, thumbH);
                var thumbColor = _isDraggingThumb ? _styleProvider.ButtonPressed : _styleProvider.ButtonNormal;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, thumbColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, borderColor, 1);
            }
        }

        private static void DrawArrow(SpriteBatch sb, int cx, int cy, int size, bool up, Color color)
        {
            var dir = up ? -1 : 1;
            for (int row = 0; row < size; row++)
            {
                var width = (row * 2) + 1;
                var x = cx - row;
                var y = cy + dir * (size / 2 - row);
                DrawingPrimitives.DrawFilledRect(sb, new Rectangle(x, y, width, 1), color);
            }
        }

        private void DrawCloseButton(Vector2 pos, float scale, BitmapFont font)
        {
            var btnW = (int)(72 * scale);
            var btnH = (int)(26 * scale);
            var btnX = (int)(pos.X + ((DlgWidth - 72) / 2) * scale);
            var btnY = (int)(pos.Y + (DlgHeight - 34) * scale);

            var mouseState = Mouse.GetState();
            var isHovered = mouseState.X >= btnX && mouseState.X < btnX + btnW
                         && mouseState.Y >= btnY && mouseState.Y < btnY + btnH;

            var btnColor = isHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;

            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, btnRect, btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, btnRect, _styleProvider.ButtonBorder, 1);

            var btnText = "Close";
            var btnTextSize = _buttonFont.MeasureString(btnText);
            var btnTextPos = new Vector2(
                btnX + (btnW - btnTextSize.Width) / 2,
                btnY + (btnH - btnTextSize.Height) / 2);
            _spriteBatch.DrawString(_buttonFont, btnText, btnTextPos, _styleProvider.ButtonText);
        }

        private void DrawTooltip(Vector2 dialogPos, float scale, BitmapFont font,
            (int ID, string Name, int Price, EIFRecord Data, object Source) item)
        {
            var lines = new List<string>();
            var itemData = item.Data;
            lines.Add(itemData.Name);
            lines.Add($"Type: {itemData.Type}");

            if (_activeTab == ShopTab.Buy)
                lines.Add($"Buy Price: {item.Price:N0} gold");
            else if (_activeTab == ShopTab.Sell)
                lines.Add($"Sell Price: {item.Price:N0} gold");
            else if (_activeTab == ShopTab.Craft && item.Source is IShopCraftItem craftItem)
            {
                lines.Add($"Ingredients: {craftItem.Ingredients.Count}");
                foreach (var ingred in craftItem.Ingredients)
                {
                    var ingredData = _eifFileProvider.EIFFile[ingred.ID];
                    var hasEnough = _characterInventoryProvider.ItemInventory.Any(x => x.ItemID == ingred.ID && x.Amount >= ingred.Amount);
                    var marker = hasEnough ? "✓" : "✗";
                    lines.Add($"  {marker} {ingred.Amount}x {ingredData.Name}");
                }
            }

            if (itemData.Weight > 0)
                lines.Add($"Weight: {itemData.Weight}");

            if (GetCategory(itemData.Type) == ItemCategory.Equip)
            {
                if (itemData.MinDam > 0 || itemData.MaxDam > 0)
                    lines.Add($"Damage: {itemData.MinDam}-{itemData.MaxDam}");
                if (itemData.Accuracy > 0)
                    lines.Add($"Accuracy: {itemData.Accuracy}");
                if (itemData.Evade > 0)
                    lines.Add($"Evade: {itemData.Evade}");
                if (itemData.Armor > 0)
                    lines.Add($"Armor: {itemData.Armor}");
                if (itemData.HP > 0)
                    lines.Add($"HP: +{itemData.HP}");
                if (itemData.TP > 0)
                    lines.Add($"TP: +{itemData.TP}");

                var stats = new List<string>();
                if (itemData.Str > 0) stats.Add($"Str+{itemData.Str}");
                if (itemData.Int > 0) stats.Add($"Int+{itemData.Int}");
                if (itemData.Wis > 0) stats.Add($"Wis+{itemData.Wis}");
                if (itemData.Agi > 0) stats.Add($"Agi+{itemData.Agi}");
                if (itemData.Con > 0) stats.Add($"Con+{itemData.Con}");
                if (itemData.Cha > 0) stats.Add($"Cha+{itemData.Cha}");
                if (stats.Count > 0)
                    lines.Add(string.Join(" ", stats));

                if (itemData.LevelReq > 0)
                    lines.Add($"Lvl Req: {itemData.LevelReq}");
            }

            if (itemData.Type == ItemType.Heal && itemData.HP > 0)
                lines.Add($"Heals: {itemData.HP} HP");
            if (itemData.Type == ItemType.Heal && itemData.TP > 0)
                lines.Add($"Restores: {itemData.TP} TP");

            if (itemData.Type == ItemType.Armor)
                lines.Add($"Gender: {_localizedStringFinder.GetString(EOResourceID.FEMALE - itemData.Gender)}");

            var lineHeight = (int)(font.LineHeight + 2 * scale);
            var maxWidth = 0f;
            foreach (var line in lines)
            {
                var w = font.MeasureString(line).Width;
                if (w > maxWidth) maxWidth = w;
            }

            var tooltipW = (int)(maxWidth + 16 * scale);
            var tooltipH = lines.Count * lineHeight + (int)(8 * scale);

            var mouseState = Mouse.GetState();
            var tooltipX = mouseState.X + 16;
            var tooltipY = mouseState.Y + 16;

            // Keep tooltip on screen
            if (_clientWindowSizeProvider != null)
            {
                var screenW = (int)(_clientWindowSizeProvider.GameWidth * _clientWindowSizeProvider.ScaleFactor) + _clientWindowSizeProvider.RenderOffset.X;
                var screenH = (int)(_clientWindowSizeProvider.GameHeight * _clientWindowSizeProvider.ScaleFactor) + _clientWindowSizeProvider.RenderOffset.Y;
                if (tooltipX + tooltipW > screenW)
                    tooltipX = mouseState.X - tooltipW - 8;
                if (tooltipY + tooltipH > screenH)
                    tooltipY = mouseState.Y - tooltipH - 8;
            }

            var tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipW, tooltipH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, tooltipRect, _styleProvider.TooltipBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, tooltipRect, _styleProvider.TooltipBorder, 1);

            for (int i = 0; i < lines.Count; i++)
            {
                var lineColor = i == 0 ? _styleProvider.TextHighlight : _styleProvider.TooltipText;
                _spriteBatch.DrawString(font, lines[i],
                    new Vector2(tooltipX + 8 * scale, tooltipY + 4 * scale + i * lineHeight), lineColor);
            }
        }

        private enum ItemCategory { Equip, Use, Misc }

        private static ItemCategory GetCategory(ItemType type) => type switch
        {
            ItemType.Weapon or ItemType.Shield or ItemType.Armor or
            ItemType.Hat or ItemType.Boots or ItemType.Gloves or
            ItemType.Accessory or ItemType.Belt or ItemType.Necklace or
            ItemType.Ring or ItemType.Armlet or ItemType.Bracer => ItemCategory.Equip,

            ItemType.Heal or ItemType.Teleport or ItemType.Spell or
            ItemType.EXPReward or ItemType.StatReward or ItemType.SkillReward or
            ItemType.Beer or ItemType.EffectPotion or ItemType.HairDye or
            ItemType.CureCurse => ItemCategory.Use,

            _ => ItemCategory.Misc
        };

        // ---- Input Handling ----

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            var (localX, localY) = ScreenToLocal(eventArgs.Position.X, eventArgs.Position.Y);

            // Tab clicks
            if (localY >= TabAreaTop && localY < TabAreaTop + TabHeight)
            {
                var tabs = new List<ShopTab> { ShopTab.Buy, ShopTab.Sell };
                if (_craftItems.Count > 0)
                    tabs.Add(ShopTab.Craft);

                var totalTabWidth = DlgWidth - GridLeftMargin * 2;
                var tabWidth = totalTabWidth / tabs.Count;
                var tabIndex = (localX - GridLeftMargin) / tabWidth;

                if (tabIndex >= 0 && tabIndex < tabs.Count && localX >= GridLeftMargin)
                {
                    _activeTab = tabs[tabIndex];
                    _scrollOffset = 0;
                    UpdateScrollDimensions();
                    return true;
                }
            }

            // Close button click
            var btnX = (DlgWidth - 72) / 2;
            var btnY = DlgHeight - 34;
            if (localX >= btnX && localX < btnX + 72 && localY >= btnY && localY < btnY + 26)
            {
                Close(XNADialogResult.Cancel);
                return true;
            }

            // Grid tile click — buy/sell/craft item
            if (localX >= GridLeftMargin && localX < ScrollBarLeft
                && localY >= GridAreaTop && localY < GridAreaTop + GridAreaHeight)
            {
                if (_hoveredTileIndex >= 0)
                {
                    var items = GetActiveItems();
                    if (_hoveredTileIndex < items.Count)
                    {
                        var item = items[_hoveredTileIndex];
                        if (_activeTab == ShopTab.Buy && item.Source is IShopItem buyItem)
                            TradeItem(buyItem, buying: true);
                        else if (_activeTab == ShopTab.Sell && item.Source is IShopItem sellItem)
                            TradeItem(sellItem, buying: false);
                        else if (_activeTab == ShopTab.Craft && item.Source is IShopCraftItem craftItem)
                            CraftItem(craftItem);

                        _hoveredTileIndex = -1;
                    }
                }
                return true;
            }

            return true;
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            var (localX, localY) = ScreenToLocal(eventArgs.Position.X, eventArgs.Position.Y);

            // Scrollbar interactions
            if (localX >= ScrollBarLeft && localX < ScrollBarLeft + ScrollBarWidth
                && localY >= ScrollBarTop && localY < ScrollBarTop + GridAreaHeight)
            {
                var sbLocalY = localY - ScrollBarTop;

                // Up arrow
                if (sbLocalY < ScrollArrowHeight)
                {
                    ScrollUp();
                    return true;
                }

                // Down arrow
                if (sbLocalY >= GridAreaHeight - ScrollArrowHeight)
                {
                    ScrollDown();
                    return true;
                }

                // Track area — page scroll or start thumb drag
                if (_totalRows > VisibleRows)
                {
                    var trackLocalY = sbLocalY - ScrollArrowHeight;
                    var thumbY = GetThumbY();
                    var thumbHeight = GetThumbHeight();

                    if (trackLocalY >= thumbY && trackLocalY < thumbY + thumbHeight)
                    {
                        _isDraggingThumb = true;
                        _dragStartY = localY;
                        _dragStartOffset = _scrollOffset;
                    }
                    else if (trackLocalY < thumbY)
                    {
                        _scrollOffset = Math.Max(0, _scrollOffset - VisibleRows);
                    }
                    else
                    {
                        _scrollOffset = Math.Min(MaxScrollOffset, _scrollOffset + VisibleRows);
                    }
                }

                return true;
            }

            return true;
        }

        protected override bool HandleDrag(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (_isDraggingThumb)
                return true;

            return base.HandleDrag(control, eventArgs);
        }

        protected override bool HandleMouseWheelMoved(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.ScrollWheelDelta > 0)
                ScrollUp(1);
            else if (eventArgs.ScrollWheelDelta < 0)
                ScrollDown(1);
            return true;
        }

        // ---- Trade & Craft Logic (preserved from original) ----

        private void TradeItem(IShopItem shopItem, bool buying)
        {
            var data = _eifFileProvider.EIFFile[shopItem.ID];
            var inventoryItem = _characterInventoryProvider.ItemInventory
                .SingleOrNone(x => buying ? x.ItemID == 1 : x.ItemID == shopItem.ID);

            // Validation
            if (buying)
            {
                if (!_inventorySpaceValidator.ItemFits(data.ID))
                {
                    var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_SPACE, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                    msg.ShowDialog();
                    return;
                }

                var stats = _characterProvider.MainCharacter.Stats;
                if (data.Weight + stats[CharacterStat.Weight] > stats[CharacterStat.MaxWeight])
                {
                    var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                    msg.ShowDialog();
                    return;
                }

                var hasEnoughGold = inventoryItem.Match(some: x => x.Amount >= shopItem.Buy, none: () => false);
                if (!hasEnoughGold)
                {
                    var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH, " gold.");
                    msg.ShowDialog();
                    return;
                }
            }
            else
            {
                var hasEnoughItem = inventoryItem.Match(some: x => x.Amount > 0, none: () => false);
                if (!hasEnoughItem)
                {
                    var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SHOP_NOT_BUYING_YOUR_ITEMS);
                    msg.ShowDialog();
                    return;
                }
            }

            var needItemTransferDialog = (buying && shopItem.MaxBuy != 1) || (!buying && inventoryItem.Match(x => x.Amount != 1, () => false));

            if (needItemTransferDialog)
            {
                var itemTransferDialog = _itemTransferDialogFactory.CreateItemTransferDialog(data.Name,
                    ItemTransferType.ShopTransfer,
                    buying ? shopItem.MaxBuy : inventoryItem.Match(x => x.Amount, () => 0),
                    buying ? EOResourceID.DIALOG_TRANSFER_BUY : EOResourceID.DIALOG_TRANSFER_SELL);
                itemTransferDialog.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        ConfirmAndExecuteTrade(itemTransferDialog.SelectedAmount);
                };

                itemTransferDialog.ShowDialog();
            }
            else
            {
                ConfirmAndExecuteTrade(amount: 1);
            }

            void ConfirmAndExecuteTrade(int amount)
            {
                var message = $"{_localizedStringFinder.GetString(buying ? EOResourceID.DIALOG_WORD_BUY : EOResourceID.DIALOG_WORD_SELL)} {amount} {data.Name} " +
                    $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_WORD_FOR)} {(buying ? shopItem.Buy : shopItem.Sell) * amount} gold?";
                var dlg = _messageBoxFactory.CreateMessageBox(message, _localizedStringFinder.GetString(buying ? EOResourceID.DIALOG_SHOP_BUY_ITEMS : EOResourceID.DIALOG_SHOP_SELL_ITEMS), EODialogButtons.OkCancel);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.Cancel)
                        return;

                    if (buying)
                        _shopActions.BuyItem(shopItem.ID, amount);
                    else
                        _shopActions.SellItem(shopItem.ID, amount);
                };
                dlg.ShowDialog();
            }
        }

        private void CraftItem(IShopCraftItem craftItem)
        {
            var data = _eifFileProvider.EIFFile[craftItem.ID];

            // Check ingredients
            foreach (var ingredient in craftItem.Ingredients)
            {
                if (!_characterInventoryProvider.ItemInventory.Any(x => x.ItemID == ingredient.ID && x.Amount >= ingredient.Amount))
                {
                    var message = BuildMessage(EOResourceID.DIALOG_SHOP_CRAFT_MISSING_INGREDIENTS);
                    var caption = BuildCaption(EOResourceID.DIALOG_SHOP_CRAFT_INGREDIENTS);

                    var dlg = _messageBoxFactory.CreateMessageBox(message, caption, EODialogButtons.Cancel, EOMessageBoxStyle.LargeDialogSmallHeader);
                    dlg.ShowDialog();
                    return;
                }
            }

            if (!_inventorySpaceValidator.ItemFits(data.ID))
            {
                var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_SPACE, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                msg.ShowDialog();
                return;
            }

            var message2 = BuildMessage(EOResourceID.DIALOG_SHOP_CRAFT_PUT_INGREDIENTS_TOGETHER);
            var caption2 = BuildCaption(EOResourceID.DIALOG_SHOP_CRAFT_INGREDIENTS);

            var dlg2 = _messageBoxFactory.CreateMessageBox(message2, caption2, EODialogButtons.OkCancel, EOMessageBoxStyle.LargeDialogSmallHeader);
            dlg2.DialogClosing += (o, e) =>
            {
                if (e.Result == XNADialogResult.Cancel)
                    return;

                _shopActions.CraftItem(craftItem.ID);
            };
            dlg2.ShowDialog();

            string BuildMessage(EOResourceID resource)
            {
                var message = _localizedStringFinder.GetString(resource) + "\n\n";

                foreach (var ingred in craftItem.Ingredients)
                {
                    var ingredData = _eifFileProvider.EIFFile[ingred.ID];
                    message += $"+  {ingred.Amount}  {ingredData.Name}\n";
                }

                return message;
            }

            string BuildCaption(EOResourceID resource)
            {
                return $"{_localizedStringFinder.GetString(resource)} {_localizedStringFinder.GetString(EOResourceID.DIALOG_WORD_FOR)} {data.Name}";
            }
        }
    }
}
