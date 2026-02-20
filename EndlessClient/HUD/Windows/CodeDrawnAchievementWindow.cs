using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.HUD.Panels;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Character;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Achievement;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// A draggable floating window that displays all achievements, their progress,
    /// tier details, and per-tier leaderboard.
    /// </summary>
    public class CodeDrawnAchievementWindow : DraggableHudPanel, IZOrderedWindow
    {
        private enum AchievementTab { All, NpcKills, Quests, Maps, Equipment, Crafting, Pets, Badges }

        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IAchievementProvider _achievementProvider;
        private readonly IAchievementActions _achievementActions;
        private readonly IContentProvider _contentProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private Texture2D _badgeSheet;

        private const int BadgeIconSize = 12;

        private const int PanelWidth = 340;
        private const int PanelHeight = 460;
        private const int HeaderHeight = 24;

        private const int TabBarHeight = 22;
        private const int Padding = 8;
        private const int CardHeight = 70;
        private const int CardGap = 4;
        private const int ScrollBarWidth = 6;
        private const int BadgeFooterHeight = 40;
        private const int ContentAreaHeight = PanelHeight - HeaderHeight - TabBarHeight;

        private AchievementTab _activeTab = AchievementTab.All;

        private int _selectedAchievementId = -1;
        private int _leaderboardAchievementId = -1;
        private IReadOnlyList<LeaderboardEntry> _leaderboardEntries = Array.Empty<LeaderboardEntry>();
        private float _scrollOffset;
        private int _hoveredTabIndex = -1;
        private int _hoveredRowIndex = -1;
        private bool _dataRequested;
        private double _pollTimer;

        // Filtered/cached list
        private IReadOnlyList<AchievementDefinition> _filteredAchievements = Array.Empty<AchievementDefinition>();
        private IReadOnlyList<AchievementDefinition> _lastAchievements = Array.Empty<AchievementDefinition>();

        // Badge selection state
        private readonly HashSet<int> _selectedBadgeIds = new HashSet<int>();
        private bool _badgesDirty;

        // Theme colors
        private Color HeaderColor => new Color(_styleProvider.TitleBarBackground, 0.95f);
        private Color HeaderText => _styleProvider.TitleBarText;
        private Color TabTextActive => _styleProvider.TabText;
        private Color TabTextInactive => _styleProvider.TextSecondary;

        // Card colors - darken the panel background slightly for card fill
        private Color CardBg
        {
            get
            {
                var p = _styleProvider.PanelBackground;
                return new Color((int)(p.R * 0.82f), (int)(p.G * 0.82f), (int)(p.B * 0.82f), 230);
            }
        }
        private Color CardBorder => new Color(_styleProvider.SlotBorder, 0.59f);
        private Color CardSelectedBg
        {
            get
            {
                var p = _styleProvider.PanelBackground;
                return new Color((int)(p.R * 0.72f), (int)(p.G * 0.72f), (int)(p.B * 0.72f), 240);
            }
        }
        private Color CardSelectedBorder => new Color(_styleProvider.TextHighlight, 0.70f);

        // Tier colors - use darker variants on light backgrounds, brighter on dark
        private Color[] TierColors
        {
            get
            {
                var bg = _styleProvider.PanelBackground;
                var lum = (bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f) / 255f;
                if (lum > 0.5f) // Light theme
                {
                    return new[]
                    {
                        new Color(120, 120, 140),  // Tier 1 - Dark silver
                        new Color(180, 150, 0),    // Tier 2 - Dark gold
                        new Color(0, 120, 200),    // Tier 3 - Deep blue
                        new Color(110, 60, 160),   // Tier 4 - Deep purple
                        new Color(200, 50, 0),     // Tier 5 - Deep red-orange
                    };
                }
                return new[]
                {
                    new Color(220, 220, 240),  // Tier 1 - Bright silver
                    new Color(255, 215, 0),    // Tier 2 - Gold
                    new Color(0, 191, 255),    // Tier 3 - Diamond blue
                    new Color(148, 103, 189),  // Tier 4 - Purple
                    new Color(255, 69, 0),     // Tier 5 - Legendary red-orange
                };
            }
        }

        // Progress bar colors
        private Color AchProgressBarBg => _styleProvider.ProgressBarBackground;
        private Color AchProgressBarFill => _styleProvider.ProgressBarFill;
        private Color AchProgressBarComplete => _styleProvider.CompletionColor;

        public CodeDrawnAchievementWindow(
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IAchievementProvider achievementProvider,
            IAchievementActions achievementActions,
            IEIFFileProvider eifFileProvider)
            : base(true)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _achievementProvider = achievementProvider;
            _achievementActions = achievementActions;
            _contentProvider = contentProvider;
            _eifFileProvider = eifFileProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            DrawArea = new Rectangle(
                _clientWindowSizeProvider.Width / 2 - PanelWidth / 2,
                _clientWindowSizeProvider.Height / 2 - PanelHeight / 2,
                PanelWidth,
                PanelHeight);

            Visible = false;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            if (_contentProvider.Textures.ContainsKey(ContentProvider.IconBadges))
                _badgeSheet = _contentProvider.Textures[ContentProvider.IconBadges];

            base.Initialize();
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                _achievementActions.RequestAchievements();
                _dataRequested = true;
                _pollTimer = 0;
                _scrollOffset = 0;
                _selectedAchievementId = -1;
                _leaderboardAchievementId = -1;
            }
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MonoGame.Extended.Input.MouseButton.Left)
                return base.HandleClick(control, eventArgs);

            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var logicalX = (int)((eventArgs.Position.X - offset.X) / scale);
            var logicalY = (int)((eventArgs.Position.Y - offset.Y) / scale);

            var area = DrawAreaWithParentOffset;
            var localX = logicalX - area.X;
            var localY = logicalY - area.Y;

            // Tab bar click
            var tabTop = HeaderHeight;
            if (localY >= tabTop && localY < tabTop + TabBarHeight)
            {
                var tabNames = Enum.GetValues(typeof(AchievementTab));
                var tabWidth = PanelWidth / tabNames.Length;
                var tabIndex = localX / tabWidth;
                if (tabIndex >= 0 && tabIndex < tabNames.Length)
                {
                    _activeTab = (AchievementTab)tabIndex;
                    _scrollOffset = 0;
                    _selectedAchievementId = -1;

                    // When switching to Badges tab, populate selection from server data
                    if (_activeTab == AchievementTab.Badges)
                    {
                        _selectedBadgeIds.Clear();
                        foreach (var id in _achievementProvider.SelectedBadgeIds)
                            _selectedBadgeIds.Add(id);
                        _badgesDirty = false;
                    }

                    RebuildFilteredList();
                    return true;
                }
            }

            // Save button click — must be BEFORE row click handler (badges tab)
            if (_activeTab == AchievementTab.Badges)
            {
                var footerTop = PanelHeight - BadgeFooterHeight;
                var saveW = 80;
                var saveH = 22;
                var saveX = PanelWidth - saveW - Padding;
                var saveY = footerTop + (BadgeFooterHeight - saveH) / 2;
                if (localX >= saveX && localX <= saveX + saveW && localY >= saveY && localY <= saveY + saveH)
                {
                    _achievementActions.SendBadgeSelection(_selectedBadgeIds.ToArray());
                    _badgesDirty = false;
                    return true;
                }
            }

            // Achievement row click
            var contentTop = HeaderHeight + TabBarHeight;
            var rowStride = CardHeight + CardGap;
            if (localY >= contentTop && localY < PanelHeight)
            {
                var rowIndex = (int)((localY - contentTop + _scrollOffset) / rowStride);
                if (rowIndex >= 0 && rowIndex < _filteredAchievements.Count)
                {
                    var ach = _filteredAchievements[rowIndex];

                    if (_activeTab == AchievementTab.Badges)
                    {
                        // Toggle badge selection
                        if (_selectedBadgeIds.Contains(ach.Id))
                        {
                            _selectedBadgeIds.Remove(ach.Id);
                            _badgesDirty = true;
                        }
                        else if (_selectedBadgeIds.Count < 3)
                        {
                            _selectedBadgeIds.Add(ach.Id);
                            _badgesDirty = true;
                        }
                        return true;
                    }

                    if (_selectedAchievementId == ach.Id)
                    {
                        // Deselect
                        _selectedAchievementId = -1;
                        _leaderboardAchievementId = -1;
                        _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                    }
                    else
                    {
                        // Select and auto-request overall leaderboard
                        _selectedAchievementId = ach.Id;
                        _leaderboardAchievementId = ach.Id;
                        _leaderboardEntries = Array.Empty<LeaderboardEntry>();
                        _achievementActions.RequestLeaderboard(ach.Id);
                    }
                    return true;
                }
            }

            return base.HandleClick(control, eventArgs);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (Visible)
            {
                // Poll for fresh achievement data every 3 seconds
                _pollTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_pollTimer >= 3.0)
                {
                    _pollTimer = 0;
                    _achievementActions.RequestAchievements();
                }

                // Check if achievements data has changed
                var current = _achievementProvider.Achievements;
                if (current != _lastAchievements)
                {
                    _lastAchievements = current;
                    RebuildFilteredList();

                    // Re-request leaderboard if we have a selection (achievement data changed = possible tier unlock)
                    if (_leaderboardAchievementId > 0)
                    {
                        _achievementActions.RequestLeaderboard(_leaderboardAchievementId);
                    }
                }

                HandleScrollWheel();
                UpdateHover();

                // Check for leaderboard data update
                if (_leaderboardAchievementId > 0 &&
                    _achievementProvider.LeaderboardAchievementId == _leaderboardAchievementId)
                {
                    _leaderboardEntries = _achievementProvider.LeaderboardEntries;
                }
            }

            base.OnUpdateControl(gameTime);
        }

        private void RebuildFilteredList()
        {
            var all = _lastAchievements;
            if (all == null || all.Count == 0)
            {
                _filteredAchievements = Array.Empty<AchievementDefinition>();
                return;
            }

            IEnumerable<AchievementDefinition> filtered = all;

            // Filter by tab
            if (_activeTab != AchievementTab.All && _activeTab != AchievementTab.Badges)
            {
                filtered = _activeTab switch
                {
                    AchievementTab.NpcKills => filtered.Where(a => a.Type == "kill_npc"),
                    AchievementTab.Quests => filtered.Where(a => a.Type == "unique_quests"),
                    AchievementTab.Maps => filtered.Where(a => a.Type == "unique_maps"),
                    AchievementTab.Equipment => filtered.Where(a =>
                        a.Type == "unique_weapons" || a.Type == "unique_armors" ||
                        a.Type == "unique_shields" || a.Type == "unique_hats" ||
                        a.Type == "unique_boots"),
                    AchievementTab.Crafting => filtered.Where(a => a.Type == "unique_crafts"),
                    AchievementTab.Pets => filtered.Where(a => a.Type == "unique_pets"),
                    _ => filtered
                };
            }

            // Badges tab: only show maxed achievements
            if (_activeTab == AchievementTab.Badges)
            {
                var maxedIds = _achievementProvider.MaxedAchievementIds;
                filtered = filtered.Where(a => maxedIds.Contains(a.Id));
            }

            // Sort by progress: highest tier first, then by current progress descending
            _filteredAchievements = filtered
                .OrderByDescending(a => a.CurrentTier)
                .ThenByDescending(a => a.CurrentProgress)
                .ToList();
        }

        private int _prevScrollValue;

        private void HandleScrollWheel()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var mouseX = (int)((mouseState.X - offset.X) / scale);
            var mouseY = (int)((mouseState.Y - offset.Y) / scale);

            var area = DrawAreaWithParentOffset;
            if (mouseX < area.X || mouseX > area.X + PanelWidth ||
                mouseY < area.Y || mouseY > area.Y + PanelHeight)
            {
                _prevScrollValue = mouseState.ScrollWheelValue;
                return;
            }

            var delta = mouseState.ScrollWheelValue - _prevScrollValue;
            _prevScrollValue = mouseState.ScrollWheelValue;

            if (delta == 0) return;

            var scrollChange = -delta / 4f;
            _scrollOffset = Math.Max(0, _scrollOffset + scrollChange);

            var effectiveContentH = _activeTab == AchievementTab.Badges ? ContentAreaHeight - BadgeFooterHeight : ContentAreaHeight;
            var maxScroll = Math.Max(0, _filteredAchievements.Count * (CardHeight + CardGap) - effectiveContentH);
            _scrollOffset = Math.Min(_scrollOffset, maxScroll);
        }

        private void UpdateHover()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var mouseX = (int)((mouseState.X - offset.X) / scale);
            var mouseY = (int)((mouseState.Y - offset.Y) / scale);
            var area = DrawAreaWithParentOffset;
            var localX = mouseX - area.X;
            var localY = mouseY - area.Y;

            // Tab hover
            var tabTop = HeaderHeight;
            var tabNames = Enum.GetValues(typeof(AchievementTab));
            var tabWidth = PanelWidth / tabNames.Length;
            if (localY >= tabTop && localY < tabTop + TabBarHeight && localX >= 0 && localX < PanelWidth)
                _hoveredTabIndex = localX / tabWidth;
            else
                _hoveredTabIndex = -1;

            // Row hover
            var contentTop = HeaderHeight + TabBarHeight;
            if (localY >= contentTop && localY < PanelHeight && localX >= 0 && localX < PanelWidth)
                _hoveredRowIndex = (int)((localY - contentTop + _scrollOffset) / (CardHeight + CardGap));
            else
                _hoveredRowIndex = -1;
        }

        // IZOrderedWindow
        private int _zOrder = 12;
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }
        public int PostScaleDrawOrder => _zOrder;
        public bool SkipRenderTargetDraw => true;

        public void BringToFront()
        {
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                DrawPanelFills(DrawPositionWithParentOffset);
            }
            else
            {
                DrawPanelComplete(DrawPositionWithParentOffset, 1f);
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

            DrawPanelComplete(scaledPos, scaleFactor);
        }

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.92f));
            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos, float scale)
        {
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scale);
            var scaledW = (int)(PanelWidth * scale);
            var scaledH = (int)(PanelHeight * scale);

            _spriteBatch.Begin();

            // Panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, scaledW, scaledH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.96f));

            // Header
            var headerH = (int)(HeaderHeight * scale);
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, scaledW, headerH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);
            _spriteBatch.DrawString(font, "Achievements", new Vector2(pos.X + Padding * scale, pos.Y + 4 * scale), HeaderText);



            // Tab bar
            DrawTabBar(pos, scale, font);

            _spriteBatch.End();

            // Achievement cards — clipped to content area
            var contentTop = (int)(pos.Y + (HeaderHeight + TabBarHeight) * scale);
            var footerH = _activeTab == AchievementTab.Badges ? (int)(BadgeFooterHeight * scale) : 0;
            var contentBottom = (int)(pos.Y + scaledH) - footerH;
            var clipRect = new Rectangle((int)pos.X, contentTop, scaledW, contentBottom - contentTop);

            var gd = _graphicsDeviceProvider.GraphicsDevice;
            var prevScissor = gd.ScissorRectangle;
            var prevRasterizer = gd.RasterizerState;
            gd.ScissorRectangle = clipRect;

            var scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
            _spriteBatch.Begin(rasterizerState: scissorRasterizer);

            DrawAchievementList(pos, scale, font);

            _spriteBatch.End();

            gd.ScissorRectangle = prevScissor;
            gd.RasterizerState = prevRasterizer;

            // Overlays and border (unclipped)
            _spriteBatch.Begin();

            // Leaderboard overlay
            if (_leaderboardAchievementId > 0)
                DrawLeaderboardOverlay(pos, scale, font);

            // Badge footer bar with save button and count
            if (_activeTab == AchievementTab.Badges)
            {
                var footerTop = (int)(pos.Y + (PanelHeight - BadgeFooterHeight) * scale);
                var footerRect = new Rectangle((int)pos.X + 1, footerTop, scaledW - 2, (int)(BadgeFooterHeight * scale));

                // Footer background + separator line
                DrawingPrimitives.DrawFilledRect(_spriteBatch, footerRect, _styleProvider.SectionBackground);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X + 1, footerTop, scaledW - 2, 1), _styleProvider.PanelBorder);

                // Selection count on the left
                var countText = $"{_selectedBadgeIds.Count}/3 selected";
                var countSize = font.MeasureString(countText);
                var countY = footerTop + ((int)(BadgeFooterHeight * scale) - (int)countSize.Height) / 2;
                _spriteBatch.DrawString(font, countText,
                    new Vector2(pos.X + Padding * scale, countY),
                    _styleProvider.TextSecondary);

                // Save button on the right
                var saveW = (int)(80 * scale);
                var saveH = (int)(22 * scale);
                var saveX = (int)(pos.X + scaledW - saveW - Padding * scale);
                var saveY = footerTop + ((int)(BadgeFooterHeight * scale) - saveH) / 2;
                var saveRect = new Rectangle(saveX, saveY, saveW, saveH);
                var btnColor = _badgesDirty ? _styleProvider.CompletionColor : _styleProvider.ButtonDisabled;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, saveRect, btnColor);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, saveRect, _styleProvider.ButtonBorder, 1);
                var saveText = _badgesDirty ? "Save" : "Saved";
                var saveTextSize = font.MeasureString(saveText);
                _spriteBatch.DrawString(font, saveText,
                    new Vector2(saveX + (saveW - saveTextSize.Width) / 2, saveY + 4 * scale),
                    _styleProvider.ButtonText);
            }

            // Panel border (drawn last)
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            _spriteBatch.End();
        }

        private void DrawTabBar(Vector2 pos, float scale, BitmapFont font)
        {
            var tabNames = new[] { "All", "Kills", "Quest", "Maps", "Equip", "Craft", "Pets", "Badge" };
            var tabTop = pos.Y + HeaderHeight * scale;
            var tabBarH = (int)(TabBarHeight * scale);
            var tabWidth = (int)(PanelWidth * scale / tabNames.Length);

            // Tab bar background — match header style
            var tabBarRect = new Rectangle((int)pos.X, (int)tabTop, (int)(PanelWidth * scale), tabBarH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, tabBarRect, new Color(_styleProvider.TitleBarBackground, 0.6f));

            for (int i = 0; i < tabNames.Length; i++)
            {
                var tabRect = new Rectangle((int)(pos.X + i * tabWidth), (int)tabTop, tabWidth, tabBarH);
                Color tabText;

                if ((int)_activeTab == i)
                {
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, tabRect, new Color(0, 0, 0, 40));
                    tabText = _styleProvider.TabText;
                }
                else if (_hoveredTabIndex == i)
                {
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, tabRect, new Color(0, 0, 0, 20));
                    tabText = _styleProvider.TextPrimary;
                }
                else
                {
                    tabText = _styleProvider.TextSecondary;
                }

                var textSize = font.MeasureString(tabNames[i]);
                _spriteBatch.DrawString(font, tabNames[i],
                    new Vector2(tabRect.X + (tabRect.Width - textSize.Width) / 2, tabRect.Y + 4 * scale),
                    tabText);
            }
        }

        private void DrawAchievementList(Vector2 pos, float scale, BitmapFont font)
        {
            var contentTop = pos.Y + (HeaderHeight + TabBarHeight) * scale;
            var contentHeight = ContentAreaHeight * scale;
            var rowStride = (CardHeight + CardGap) * scale;

            if (_filteredAchievements.Count == 0)
            {
                var msg = _dataRequested ? "No achievements found" : "Loading...";
                _spriteBatch.DrawString(font, msg,
                    new Vector2(pos.X + Padding * scale, contentTop + Padding * scale),
                    _styleProvider.TextSecondary);
                return;
            }

            var startIndex = Math.Max(0, (int)(_scrollOffset / (CardHeight + CardGap)));
            var visibleRows = (int)(contentHeight / rowStride) + 2;

            for (int i = startIndex; i < Math.Min(startIndex + visibleRows, _filteredAchievements.Count); i++)
            {
                var ach = _filteredAchievements[i];
                var cardY = contentTop + (i * (CardHeight + CardGap) - _scrollOffset) * scale;

                if (cardY + CardHeight * scale < contentTop || cardY > contentTop + contentHeight)
                    continue;

                DrawAchievementCard(pos, cardY, ach, i, scale, font);
            }
        }

        private void DrawAchievementCard(Vector2 panelPos, float cardY, AchievementDefinition ach, int index, float scale, BitmapFont font)
        {
            var cardX = (int)(panelPos.X + Padding * scale);
            var cardW = (int)((PanelWidth - Padding * 2 - ScrollBarWidth) * scale);
            var cardH = (int)(CardHeight * scale);
            var isSelected = ach.Id == _selectedAchievementId;
            var isHovered = index == _hoveredRowIndex;

            var cardRect = new Rectangle(cardX, (int)cardY, cardW, cardH);

            // Card background + border
            var isBadgeSelected = _activeTab == AchievementTab.Badges && _selectedBadgeIds.Contains(ach.Id);
            var bg = (isSelected || isBadgeSelected) ? CardSelectedBg : CardBg;
            var border = (isSelected || isBadgeSelected) ? CardSelectedBorder : CardBorder;
            if (isHovered && !isSelected && !isBadgeSelected)
                bg = CardSelectedBg;

            DrawingPrimitives.DrawFilledRect(_spriteBatch, cardRect, bg);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, cardRect, border, Math.Max(1, (int)(isBadgeSelected ? 2 * scale : 1)));

            // Badge tab: draw SELECTED label
            if (_activeTab == AchievementTab.Badges && isBadgeSelected)
            {
                var labelText = "SELECTED";
                var labelSize = font.MeasureString(labelText);
                var labelX = cardX + cardW - (int)labelSize.Width - (int)(6 * scale);
                var labelY = (int)cardY + (int)(4 * scale);
                _spriteBatch.DrawString(font, labelText, new Vector2(labelX, labelY), _styleProvider.GoldColor);
            }

            var innerX = cardX + (int)(6 * scale);
            var innerW = cardW - (int)(12 * scale);

            // Row 1: Name + Type label
            var y = cardY + 3 * scale;
            _spriteBatch.DrawString(font, ach.Name, new Vector2(innerX, y), _styleProvider.TextPrimary);

            var typeLabel = GetTypeLabel(ach.Type);
            var typeLabelSize = font.MeasureString(typeLabel);
            var typeLabelX = cardX + cardW - (int)(6 * scale) - (int)typeLabelSize.Width;

            // Badge icon (to the left of the type label)
            if (_badgeSheet != null && CharacterNamePlate.BadgeIconIndex.TryGetValue(ach.Name, out var iconIdx))
            {
                var iconSize = (int)(BadgeIconSize * scale);
                var iconX = typeLabelX - iconSize - (int)(4 * scale);
                var iconY = (int)(y + 1 * scale);
                var srcRect = new Rectangle(iconIdx * BadgeIconSize, 0, BadgeIconSize, BadgeIconSize);
                var dstRect = new Rectangle(iconX, iconY, iconSize, iconSize);
                _spriteBatch.Draw(_badgeSheet, dstRect, srcRect, Color.White);
            }

            _spriteBatch.DrawString(font, typeLabel,
                new Vector2(typeLabelX, y),
                _styleProvider.TextSecondary);

            // Row 1.5: Description (subtle grey, below name)
            if (!string.IsNullOrEmpty(ach.Description))
            {
                y += 14 * scale;
                _spriteBatch.DrawString(font, ach.Description, new Vector2(innerX, y), _styleProvider.TextSecondary);
            }

            // Row 2: Progress bar (full width within card)
            y += 16 * scale;
            var barH = Math.Max(2, (int)(6 * scale));
            var barRect = new Rectangle(innerX, (int)y, innerW, barH);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, AchProgressBarBg);

            // Progress calculation
            float progressRatio = 0;
            if (ach.Tiers.Length > 0)
            {
                var idx = ach.CurrentTier;
                if (idx < ach.Tiers.Length)
                {
                    var next = ach.Tiers[idx].Threshold;
                    var prev = idx > 0 ? ach.Tiers[idx - 1].Threshold : 0;
                    var range = next - prev;
                    if (range > 0)
                        progressRatio = Math.Clamp((float)(ach.CurrentProgress - prev) / range, 0, 1);
                }
                else
                {
                    progressRatio = 1;
                }
            }

            var fillColor = progressRatio >= 1f ? AchProgressBarComplete : AchProgressBarFill;
            var fillRect = new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * progressRatio), barRect.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, fillRect, fillColor);

            // Shine on progress bar
            if (fillRect.Width > 2)
            {
                var shine = new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, Math.Max(1, fillRect.Height / 2));
                DrawingPrimitives.DrawFilledRect(_spriteBatch, shine, new Color(255, 255, 255, 30));
            }

            // Row 3: Tier info + reward hint + progress text
            y += (barH + 4) * scale;

            // Tier text with colored indicator
            var tierText = $"Tier {ach.CurrentTier}/{ach.Tiers.Length}";
            var tierColorIdx = Math.Max(0, Math.Min(ach.CurrentTier - 1, TierColors.Length - 1));
            var tierColor = ach.CurrentTier > 0 ? TierColors[tierColorIdx] : _styleProvider.TextSecondary;
            _spriteBatch.DrawString(font, tierText, new Vector2(innerX, y), tierColor);

            // Next tier reward hint (center-ish)
            if (ach.CurrentTier < ach.Tiers.Length)
            {
                var nextTier = ach.Tiers[ach.CurrentTier];
                var rewardParts = new List<string>();
                if (nextTier.ExpReward > 0)
                    rewardParts.Add($"{nextTier.ExpReward} EXP");
                if (nextTier.ItemId > 0)
                {
                    var itemName = ResolveItemName(nextTier.ItemId);
                    rewardParts.Add($"{itemName} x{nextTier.ItemAmount}");
                }
                if (rewardParts.Count > 0)
                {
                    var rewardText = "\u279C " + string.Join(", ", rewardParts);
                    var tierTextSize = font.MeasureString(tierText);
                    _spriteBatch.DrawString(font, rewardText,
                        new Vector2(innerX + tierTextSize.Width + 8 * scale, y),
                        _styleProvider.GoldColor);
                }
            }
            else
            {
                // All tiers complete
                var tierTextSize = font.MeasureString(tierText);
                _spriteBatch.DrawString(font, "\u2605 Complete!",
                    new Vector2(innerX + tierTextSize.Width + 8 * scale, y),
                    AchProgressBarComplete);
            }

            // Progress count (right-aligned)
            var progressText = $"{ach.CurrentProgress}";
            if (ach.CurrentTier < ach.Tiers.Length)
                progressText += $"/{ach.Tiers[ach.CurrentTier].Threshold}";
            var progressSize = font.MeasureString(progressText);
            _spriteBatch.DrawString(font, progressText,
                new Vector2(cardX + cardW - (int)(6 * scale) - progressSize.Width, y),
                _styleProvider.TextSecondary);
        }
        private void DrawLeaderboardOverlay(Vector2 panelPos, float scale, BitmapFont font)
        {
            // Find the selected achievement to position the overlay
            var selectedIndex = -1;
            AchievementDefinition selectedAch = null;
            for (int i = 0; i < _filteredAchievements.Count; i++)
            {
                if (_filteredAchievements[i].Id == _leaderboardAchievementId)
                {
                    selectedIndex = i;
                    selectedAch = _filteredAchievements[i];
                    break;
                }
            }
            if (selectedIndex < 0 || selectedAch == null) return;

            var contentTop = panelPos.Y + (HeaderHeight + TabBarHeight) * scale;
            var rowY = contentTop + (selectedIndex * (CardHeight + CardGap) - _scrollOffset) * scale;

            // === Build overlay sections ===
            var lineHeight = (int)(14 * scale);
            var overlayPadding = (int)(6 * scale);
            var headerLineHeight = (int)(16 * scale);
            var sectionGap = (int)(8 * scale);

            // Section 1: Tier rewards breakdown
            var rewardLines = new List<(string text, Color color)>();
            for (int t = 0; t < selectedAch.Tiers.Length; t++)
            {
                var tier = selectedAch.Tiers[t];
                var completed = t < selectedAch.CurrentTier;
                var tierColorIdx = Math.Max(0, Math.Min(t, TierColors.Length - 1));
                var col = completed ? TierColors[tierColorIdx] : _styleProvider.TextSecondary;
                var check = completed ? "\u2713 " : "  ";

                var rewardStr = $"Tier {t + 1} ({tier.Threshold}):";
                if (tier.ExpReward > 0) rewardStr += $" {tier.ExpReward} EXP";
                if (tier.ItemId > 0)
                {
                    var itemName = ResolveItemName(tier.ItemId);
                    rewardStr += $" + {itemName} x{tier.ItemAmount}";
                }
                rewardLines.Add((check + rewardStr, col));
            }

            // Section 2: Leaderboard entries
            var maxEntries = Math.Min(_leaderboardEntries.Count, 10);

            // Calculate overlay height
            var overlayWidth = (int)(200 * scale);
            var overlayHeight = overlayPadding * 2
                + headerLineHeight  // "Rewards" header
                + rewardLines.Count * lineHeight
                + sectionGap
                + headerLineHeight  // "Leaderboard" header
                + Math.Max(maxEntries, 1) * lineHeight;
            if (_leaderboardEntries.Count > 10)
                overlayHeight += lineHeight;

            // Position to the right of the panel
            var overlayX = (int)(panelPos.X + PanelWidth * scale + 4 * scale);
            var overlayY = (int)rowY;

            // Clamp to screen
            if (overlayY + overlayHeight > _clientWindowSizeProvider.Height)
                overlayY = _clientWindowSizeProvider.Height - overlayHeight - 4;
            if (overlayY < 0) overlayY = 0;

            var overlayRect = new Rectangle(overlayX, overlayY, overlayWidth, overlayHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, overlayRect, new Color(_styleProvider.PanelBackground, 0.92f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, overlayRect, _styleProvider.PanelBorder, Math.Max(1, (int)(1 * scale)));

            var drawY = overlayY + overlayPadding;

            // --- Rewards section ---
            _spriteBatch.DrawString(font, "\u2606 Tier Rewards",
                new Vector2(overlayX + overlayPadding, drawY), _styleProvider.GoldColor);
            drawY += headerLineHeight;

            foreach (var (text, color) in rewardLines)
            {
                _spriteBatch.DrawString(font, text,
                    new Vector2(overlayX + overlayPadding, drawY), color);
                drawY += lineHeight;
            }

            drawY += sectionGap;

            // --- Leaderboard section ---
            _spriteBatch.DrawString(font, "\u265F Leaderboard",
                new Vector2(overlayX + overlayPadding, drawY), _styleProvider.TextHighlight);
            drawY += headerLineHeight;

            if (maxEntries == 0)
            {
                _spriteBatch.DrawString(font, "No entries yet",
                    new Vector2(overlayX + overlayPadding, drawY), _styleProvider.TextSecondary);
            }
            else
            {
                for (int i = 0; i < maxEntries; i++)
                {
                    var entry = _leaderboardEntries[i];
                    var entryTierIdx = Math.Max(0, Math.Min(entry.TierReached - 1, TierColors.Length - 1));
                    var entryColor = entry.TierReached > 0 ? TierColors[entryTierIdx] : _styleProvider.TextSecondary;

                    var rankText = $"{i + 1}. {entry.Name}";
                    _spriteBatch.DrawString(font, rankText,
                        new Vector2(overlayX + overlayPadding, drawY), _styleProvider.TextPrimary);

                    // Tier badge (right-aligned)
                    var tierBadge = $"T{entry.TierReached}";
                    var badgeSize = font.MeasureString(tierBadge);
                    _spriteBatch.DrawString(font, tierBadge,
                        new Vector2(overlayX + overlayWidth - overlayPadding - badgeSize.Width, drawY),
                        entryColor);

                    drawY += lineHeight;
                }
            }

            if (_leaderboardEntries.Count > 10)
            {
                _spriteBatch.DrawString(font, $"...and {_leaderboardEntries.Count - 10} more",
                    new Vector2(overlayX + overlayPadding, drawY),
                    _styleProvider.TextSecondary);
            }
        }

        private static string GetTypeLabel(string type)
        {
            return type switch
            {
                "kill_npc" => "Kills",
                "unique_quests" => "Quests",
                "unique_maps" => "Explore",
                "unique_weapons" => "Weapons",
                "unique_armors" => "Armor",
                "unique_shields" => "Shields",
                "unique_hats" => "Hats",
                "unique_boots" => "Boots",
                "unique_crafts" => "Crafting",
                "unique_pets" => "Pets",
                _ => type
            };
        }

        private string ResolveItemName(int itemId)
        {
            try
            {
                var record = _eifFileProvider.EIFFile[itemId];
                return !string.IsNullOrEmpty(record.Name) ? record.Name : $"Item #{itemId}";
            }
            catch
            {
                return $"Item #{itemId}";
            }
        }
    }
}
