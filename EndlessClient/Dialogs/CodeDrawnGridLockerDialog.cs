using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
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
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Grid-based locker dialog with search filter, category tabs, hover tooltips,
    /// and an inline-drawn scrollbar with up/down buttons + draggable thumb.
    /// </summary>
    public class CodeDrawnGridLockerDialog : XNADialog, IPostScaleDrawable
    {
        private enum ItemCategory { All, Equip, Use, Misc }

        private readonly IUIStyleProvider _styleProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly ILockerActions _lockerActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataProvider _lockerDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _buttonFont;

        private HashSet<InventoryItem> _cachedItems;
        private ItemCategory _activeCategory = ItemCategory.All;
        private string _searchText = string.Empty;
        private int _hoveredTileIndex = -1;

        // Inline scroll state (replaces CodeDrawnScrollBar child which can't render in post-scale mode)
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
        private const int TileHeight = 72;
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

        public CodeDrawnGridLockerDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager graphicsManager,
            ILockerActions lockerActions,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            IStatusLabelSetter statusLabelSetter,
            IEOMessageBoxFactory messageBoxFactory,
            ICharacterProvider characterProvider,
            ILockerDataProvider lockerDataProvider,
            IEIFFileProvider eifFileProvider,
            IContentProvider contentProvider)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _graphicsManager = graphicsManager;
            _lockerActions = lockerActions;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _characterProvider = characterProvider;
            _lockerDataProvider = lockerDataProvider;
            _eifFileProvider = eifFileProvider;
            _contentProvider = contentProvider;
            _font = contentProvider.Fonts[Constants.FontSize08pt5];
            _buttonFont = contentProvider.Fonts[Constants.FontSize10];

            _cachedItems = new HashSet<InventoryItem>();

            DrawArea = new Rectangle(0, 0, DlgWidth, DlgHeight);

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

        public override void Initialize()
        {
            if (_graphicsDeviceProvider != null)
                DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            else
                DrawingPrimitives.Initialize(Game.GraphicsDevice);

            base.Initialize();
            CenterInGameView();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Check for data changes
            if (!_cachedItems.SetEquals(_lockerDataProvider.Items))
            {
                _cachedItems = _lockerDataProvider.Items.ToHashSet();
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
                else if (MaxScrollOffset > 0)
                {
                    var (_, localY) = ScreenToLocal(mouseState.X, mouseState.Y);

                    var thumbHeight = GetThumbHeight();
                    var usableTrack = ScrollTrackHeight - thumbHeight;
                    if (usableTrack > 0)
                    {
                        var dragDelta = localY - _dragStartY;
                        var offsetDelta = (int)((dragDelta / (float)usableTrack) * MaxScrollOffset);
                        _scrollOffset = Math.Clamp(_dragStartOffset + offsetDelta, 0, MaxScrollOffset);
                    }
                }
            }

            // Update hovered tile from mouse position
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
                    var filteredItems = GetFilteredItems().ToList();

                    if (tileIndex >= 0 && tileIndex < filteredItems.Count)
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

        private IEnumerable<InventoryItem> GetFilteredItems()
        {
            var items = _cachedItems.AsEnumerable();

            if (_activeCategory != ItemCategory.All)
                items = items.Where(i => GetCategory(_eifFileProvider.EIFFile[i.ItemID].Type) == _activeCategory);

            if (!string.IsNullOrWhiteSpace(_searchText))
                items = items.Where(i => _eifFileProvider.EIFFile[i.ItemID].Name
                    .Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            return items;
        }

        private int GetCategoryCount(ItemCategory category)
        {
            if (category == ItemCategory.All) return _cachedItems.Count;
            return _cachedItems.Count(i => GetCategory(_eifFileProvider.EIFFile[i.ItemID].Type) == category);
        }

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

        private void UpdateScrollDimensions()
        {
            var totalItems = GetFilteredItems().Count();
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

        /// <summary>
        /// Converts screen-space (window pixel) coordinates to dialog-local game-space coordinates.
        /// Accounts for render scale factor and letterbox/pillarbox offset so that
        /// hit-testing matches the visually rendered positions.
        /// </summary>
        private (int X, int Y) ScreenToLocal(int screenX, int screenY)
        {
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var drawPos = DrawAreaWithParentOffset;

            var localX = (int)((screenX - offset.X) / scale) - drawPos.X;
            var localY = (int)((screenY - offset.Y) / scale) - drawPos.Y;
            return (localX, localY);
        }

        private string GetDialogTitle()
        {
            var count = $" [{_lockerDataProvider.Items.Count}]";
            return _lockerDataProvider.Context switch
            {
                LockerContext.GuildStorage => "Guild Storage" + count,
                LockerContext.DeliveryInbox => "Personal Inbox" + count,
                _ => _characterProvider.MainCharacter.Name + "'s " +
                     _localizedStringFinder.GetString(EOResourceID.DIALOG_TITLE_PRIVATE_LOCKER) + count,
            };
        }

        private void TakeItem(EIFRecord itemData, InventoryItem item)
        {
            if (!_inventorySpaceValidator.ItemFits(item.ItemID))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(
                    EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT,
                    EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
                _statusLabelSetter.SetStatusLabel(
                    EOResourceID.STATUS_LABEL_TYPE_INFORMATION,
                    EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT);
            }
            else if (itemData.Weight * item.Amount +
                     _characterProvider.MainCharacter.Stats[CharacterStat.Weight] >
                     _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(
                    EOResourceID.DIALOG_ITS_TOO_HEAVY_WEIGHT,
                    EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
            }
            else
            {
                _lockerActions.TakeItemFromLocker(item.ItemID);
            }
        }

        // ---- Drawing ----

        public int PostScaleDrawOrder => 200;
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

            var title = GetDialogTitle();
            _spriteBatch.DrawString(font, title,
                new Vector2(pos.X + 12 * scale, pos.Y + 8 * scale), _styleProvider.TitleBarText);

            // === Category Tabs ===
            DrawTabs(pos, scale, font);

            // === Grid Area Background ===
            var gridBgRect = new Rectangle(
                (int)(pos.X + GridLeftMargin * scale),
                (int)(pos.Y + GridAreaTop * scale),
                (int)((ScrollBarLeft - GridLeftMargin) * scale),
                (int)(GridAreaHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, gridBgRect, new Color(0, 0, 0, 40));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, gridBgRect, _styleProvider.PanelBorder, 1);

            // === Grid Items ===
            var filteredItems = GetFilteredItems().ToList();
            var startIndex = _scrollOffset * GridColumns;

            for (int row = 0; row < VisibleRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    var idx = startIndex + row * GridColumns + col;
                    if (idx >= filteredItems.Count) break;

                    var item = filteredItems[idx];
                    var itemData = _eifFileProvider.EIFFile[item.ItemID];

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
                        var itemIcon = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * itemData.Graphic - 1, transparent: true);
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
                    var nameText = itemData.Name;
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
                    var textY = tileY + (int)(48 * scale);
                    _spriteBatch.DrawString(font, nameText, new Vector2(textX, textY), _styleProvider.TextPrimary);

                    // Quantity badge (if > 1)
                    if (item.Amount > 1)
                    {
                        var qtyText = $"x{item.Amount}";
                        var qtySize = font.MeasureString(qtyText);
                        var qtyX = tileX + tileW - (int)qtySize.Width - (int)(3 * scale);
                        var qtyY = tileY + tileH - (int)qtySize.Height - (int)(2 * scale);

                        var badgeRect = new Rectangle(qtyX - (int)(2 * scale), qtyY, (int)qtySize.Width + (int)(4 * scale), (int)qtySize.Height);
                        DrawingPrimitives.DrawFilledRect(_spriteBatch, badgeRect, new Color(0, 0, 0, 140));
                        _spriteBatch.DrawString(font, qtyText, new Vector2(qtyX, qtyY), Color.White);
                    }
                }
            }

            // === Scrollbar ===
            DrawScrollbar(pos, scale, font);

            // === Close Button ===
            DrawCloseButton(pos, scale, font);

            // === Tooltip (drawn last, on top of everything) ===
            if (_hoveredTileIndex >= 0 && _hoveredTileIndex < filteredItems.Count)
            {
                var item = filteredItems[_hoveredTileIndex];
                var itemData = _eifFileProvider.EIFFile[item.ItemID];
                DrawTooltip(pos, scale, font, itemData, item);
            }

            _spriteBatch.End();
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
            var btnColor = _styleProvider.ButtonNormal;
            var btnHover = _styleProvider.ButtonHover;
            var arrowTextColor = _styleProvider.ButtonText;

            // Track background
            var trackRect = new Rectangle(sbX, sbY, sbW, sbH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, trackRect, trackColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, trackRect, borderColor, 1);

            // Check mouse hover for button highlights
            var mouseState = Mouse.GetState();
            var mouseOverUp = mouseState.X >= sbX && mouseState.X < sbX + sbW
                           && mouseState.Y >= sbY && mouseState.Y < sbY + arrowH;
            var mouseOverDown = mouseState.X >= sbX && mouseState.X < sbX + sbW
                             && mouseState.Y >= sbY + sbH - arrowH && mouseState.Y < sbY + sbH;

            // Up arrow button
            var upRect = new Rectangle(sbX, sbY, sbW, arrowH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, upRect, mouseOverUp ? btnHover : btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, upRect, borderColor, 1);
            // Draw up arrow triangle
            var arrowSize = (int)(5 * scale);
            var upCenterX = sbX + sbW / 2;
            var upCenterY = sbY + arrowH / 2;
            DrawArrow(_spriteBatch, upCenterX, upCenterY, arrowSize, true, arrowTextColor);

            // Down arrow button
            var downRect = new Rectangle(sbX, sbY + sbH - arrowH, sbW, arrowH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, downRect, mouseOverDown ? btnHover : btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, downRect, borderColor, 1);
            // Draw down arrow triangle
            var downCenterX = sbX + sbW / 2;
            var downCenterY = sbY + sbH - arrowH / 2;
            DrawArrow(_spriteBatch, downCenterX, downCenterY, arrowSize, false, arrowTextColor);

            // Thumb
            if (_totalRows > VisibleRows)
            {
                var thumbHeight = (int)(GetThumbHeight() * scale);
                var thumbY = sbY + arrowH + (int)(GetThumbY() * scale);
                var thumbRect = new Rectangle(sbX + (int)(2 * scale), thumbY, sbW - (int)(4 * scale), thumbHeight);

                var thumbColor = _isDraggingThumb ? _styleProvider.ButtonPressed : btnHover;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, thumbColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, borderColor, 1);
            }
        }

        private static void DrawArrow(SpriteBatch sb, int cx, int cy, int size, bool up, Color color)
        {
            // Draw a simple arrow using small filled rects (triangle approximation)
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

            // Check if mouse is over button area for hover feedback
            var mouseState = Mouse.GetState();
            var isHovered = mouseState.X >= btnX && mouseState.X < btnX + btnW
                         && mouseState.Y >= btnY && mouseState.Y < btnY + btnH;

            var btnColor = isHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;

            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, btnRect, btnColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, btnRect, _styleProvider.ButtonBorder, 1);

            var btnTextSize = _buttonFont.MeasureString("Close");
            var btnTextPos = new Vector2(
                btnX + (btnW - btnTextSize.Width) / 2,
                btnY + (btnH - btnTextSize.Height) / 2);
            _spriteBatch.DrawString(_buttonFont, "Close", btnTextPos, _styleProvider.ButtonText);
        }

        private void DrawTabs(Vector2 pos, float scale, BitmapFont font)
        {
            var categories = new[] { ItemCategory.All, ItemCategory.Equip, ItemCategory.Use, ItemCategory.Misc };
            var labels = new[] { "All", "Equip", "Use", "Misc" };

            var totalTabWidth = DlgWidth - GridLeftMargin * 2;
            var tabWidth = totalTabWidth / categories.Length;

            for (int i = 0; i < categories.Length; i++)
            {
                var cat = categories[i];
                var isActive = _activeCategory == cat;
                var count = GetCategoryCount(cat);

                var tabX = (int)(pos.X + (GridLeftMargin + i * tabWidth) * scale);
                var tabY = (int)(pos.Y + TabAreaTop * scale);
                var tabW = (int)(tabWidth * scale);
                var tabH = (int)(TabHeight * scale);

                var tabRect = new Rectangle(tabX, tabY, tabW, tabH);
                var tabColor = isActive ? _styleProvider.TabActive : _styleProvider.TabInactive;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, tabRect, tabColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, tabRect, _styleProvider.PanelBorder, 1);

                var labelText = $"{labels[i]}({count})";
                var labelSize = font.MeasureString(labelText);
                var labelX = tabX + (tabW - (int)labelSize.Width) / 2;
                var labelY = tabY + (tabH - (int)labelSize.Height) / 2;

                var textColor = isActive ? _styleProvider.TabText : _styleProvider.TextSecondary;
                _spriteBatch.DrawString(font, labelText, new Vector2(labelX, labelY), textColor);
            }
        }

        private void DrawTooltip(Vector2 dialogPos, float scale, BitmapFont font, EIFRecord itemData, InventoryItem item)
        {
            var lines = new List<string>();
            lines.Add(itemData.Name);
            lines.Add($"Type: {itemData.Type}");

            if (item.Amount > 1)
                lines.Add($"Qty: {item.Amount}");

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

        // ---- Input Handling ----

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            var (localX, localY) = ScreenToLocal(eventArgs.Position.X, eventArgs.Position.Y);

            // Tab clicks
            if (localY >= TabAreaTop && localY < TabAreaTop + TabHeight)
            {
                var totalTabWidth = DlgWidth - GridLeftMargin * 2;
                var tabWidth = totalTabWidth / 4;
                var tabIndex = (localX - GridLeftMargin) / tabWidth;

                if (tabIndex >= 0 && tabIndex < 4 && localX >= GridLeftMargin)
                {
                    _activeCategory = tabIndex switch
                    {
                        0 => ItemCategory.All,
                        1 => ItemCategory.Equip,
                        2 => ItemCategory.Use,
                        3 => ItemCategory.Misc,
                        _ => ItemCategory.All
                    };
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

            return true;
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            // Handle right-click to take items
            if (eventArgs.Button == MonoGame.Extended.Input.MouseButton.Right)
            {
                if (_hoveredTileIndex >= 0)
                {
                    var filteredItems = GetFilteredItems().ToList();
                    if (_hoveredTileIndex < filteredItems.Count)
                    {
                        var item = filteredItems[_hoveredTileIndex];
                        var itemData = _eifFileProvider.EIFFile[item.ItemID];
                        TakeItem(itemData, item);
                    }
                }
                return true;
            }

            var (localX, localY) = ScreenToLocal(eventArgs.Position.X, eventArgs.Position.Y);

            // Scrollbar interactions — must be handled here (not HandleClick)
            // to prevent the base XNADialog.HandleDrag from moving the window
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
                        // Start thumb drag — sets flag so HandleDrag suppresses window movement
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
            // When dragging the scrollbar thumb, suppress the base XNADialog behavior
            // which would move the entire window
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
    }
}
