using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Controls;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Online;
using EOLib.Domain.Party;
using EOLib.Extensions;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Code-drawn Online Players panel with styled table layout.
    /// Shows online players with Name, Title, Guild, Class columns.
    /// </summary>
    public class CodeDrawnOnlineListPanel : DraggableHudPanel
    {
        private enum Filter { All, Friends, Admins, Party, Max }

        private readonly IHudControlProvider _hudControlProvider;
        private readonly IOnlinePlayerProvider _onlinePlayerProvider;
        private readonly IOnlinePlayerActions _onlinePlayerActions;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly IFriendIgnoreListService _friendIgnoreListService;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _headerFont;

        private const int PanelWidth = 480;
        private const int PanelHeight = 118;
        private const int HeaderHeight = 20;
        private const int RowHeight = 14;
        private const int VisibleRows = 7;
        private const int Padding = 4;

        // Column positions (Name starts after filter button)
        private const int ColName = 60;
        private const int ColTitle = 160;
        private const int ColGuild = 270;
        private const int ColClass = 380;

        private readonly List<OnlinePlayerInfo> _onlineList;
        private HashSet<OnlinePlayerInfo> _cachedList;
        private Filter _filter;
        private List<OnlinePlayerInfo> _filteredList;
        private IReadOnlyList<string> _friendList;
        private int _scrollOffset;
        private Rectangle _filterButtonRect;
        private Rectangle _scrollUpRect;
        private Rectangle _scrollDownRect;
        private Rectangle _scrollTrackRect;
        private bool _wasMouseDown;
        private int _previousScrollWheelValue;

        public CodeDrawnOnlineListPanel(IHudControlProvider hudControlProvider,
                                        IOnlinePlayerProvider onlinePlayerProvider,
                                        IOnlinePlayerActions onlinePlayerActions,
                                        IPartyDataProvider partyDataProvider,
                                        IFriendIgnoreListService friendIgnoreListService,
                                        ISfxPlayer sfxPlayer,
                                        IUIStyleProvider styleProvider,
                                        IGraphicsDeviceProvider graphicsDeviceProvider,
                                        IContentProvider contentProvider,
                                        IClientWindowSizeProvider clientWindowSizeProvider)
            : base(clientWindowSizeProvider.Resizable)
        {
            _hudControlProvider = hudControlProvider;
            _onlinePlayerProvider = onlinePlayerProvider;
            _onlinePlayerActions = onlinePlayerActions;
            _partyDataProvider = partyDataProvider;
            _friendIgnoreListService = friendIgnoreListService;
            _sfxPlayer = sfxPlayer;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _headerFont = contentProvider.Fonts[Constants.FontSize09];

            _onlineList = new List<OnlinePlayerInfo>();
            _cachedList = new HashSet<OnlinePlayerInfo>();
            _filter = Filter.All;
            _filteredList = new List<OnlinePlayerInfo>();
            _friendList = new List<string>();

            DrawArea = new Rectangle(102, 330, PanelWidth, PanelHeight);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        protected override void OnVisibleChanged(object sender, System.EventArgs args)
        {
            if (Visible)
            {
                // Request online players when panel becomes visible
                _onlinePlayerActions.RequestOnlinePlayers(fullList: true);
            }
            base.OnVisibleChanged(sender, args);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (!_cachedList.SetEquals(_onlinePlayerProvider.OnlinePlayers))
            {
                _cachedList = _onlinePlayerProvider.OnlinePlayers.ToHashSet();

                // Keep friends list data from overriding displayed data
                if (!_cachedList.All(x => x.Title == string.Empty))
                {
                    _onlineList.Clear();
                    _onlineList.AddRange(_cachedList);
                    _onlineList.Sort((a, b) => a.Name.CompareTo(b.Name));

                    _friendList = _friendIgnoreListService.LoadList(Constants.FriendListFile);
                    ApplyFilter();
                }
            }

            // Handle clicks - transform mouse position for scaled mode
            var mouseState = Mouse.GetState();
            var rawMousePos = new Point(mouseState.X, mouseState.Y);
            var mousePos = TransformMousePosition(rawMousePos);
            var isMouseDown = mouseState.LeftButton == ButtonState.Pressed;

            // Handle mousewheel scrolling when mouse is over panel
            var panelRect = new Rectangle(DrawArea.X, DrawArea.Y, PanelWidth, PanelHeight);
            if (panelRect.Contains(mousePos))
            {
                var scrollDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
                if (scrollDelta > 0 && _scrollOffset > 0)
                {
                    _scrollOffset--;
                }
                else if (scrollDelta < 0 && _scrollOffset < Math.Max(0, _filteredList.Count - VisibleRows))
                {
                    _scrollOffset++;
                }
            }
            _previousScrollWheelValue = mouseState.ScrollWheelValue;

            if (_wasMouseDown && !isMouseDown)
            {
                // Filter button click
                if (_filterButtonRect.Contains(mousePos))
                {
                    _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);
                    _filter = (Filter)(((int)_filter + 1) % (int)Filter.Max);
                    _scrollOffset = 0;
                    ApplyFilter();
                }

                // Scroll buttons
                if (_scrollUpRect.Contains(mousePos) && _scrollOffset > 0)
                {
                    _scrollOffset--;
                }
                if (_scrollDownRect.Contains(mousePos) && _scrollOffset < _filteredList.Count - VisibleRows)
                {
                    _scrollOffset++;
                }
            }

            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw header bar
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));

            // Filter button
            _filterButtonRect = new Rectangle((int)pos.X + 4, (int)pos.Y + 3, 50, 14);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _filterButtonRect, _styleProvider.ButtonNormal);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _filterButtonRect, Color.Black, 1);
            var filterText = _filter.ToString();
            _spriteBatch.DrawString(_font, filterText, new Vector2(_filterButtonRect.X + 4, _filterButtonRect.Y + 1), Color.White);

            // Column headers
            _spriteBatch.DrawString(_headerFont, "Name", new Vector2(pos.X + ColName, pos.Y + 3), Color.White);
            _spriteBatch.DrawString(_headerFont, "Title", new Vector2(pos.X + ColTitle, pos.Y + 3), Color.White);
            _spriteBatch.DrawString(_headerFont, "Guild", new Vector2(pos.X + ColGuild, pos.Y + 3), Color.White);
            _spriteBatch.DrawString(_headerFont, "Class", new Vector2(pos.X + ColClass, pos.Y + 3), Color.White);

            // Player count
            var countText = $"{_filteredList.Count}";
            var countSize = _headerFont.MeasureString(countText);
            _spriteBatch.DrawString(_headerFont, countText, new Vector2(pos.X + PanelWidth - countSize.Width - 8, pos.Y + 3), Color.White);

            // Draw player rows
            var listAreaY = (int)pos.Y + HeaderHeight + 2;
            for (int i = 0; i < VisibleRows && _scrollOffset + i < _filteredList.Count; i++)
            {
                var player = _filteredList[_scrollOffset + i];
                var rowY = listAreaY + (i * RowHeight);

                // Alternating row colors
                var rowRect = new Rectangle((int)pos.X + 2, rowY, PanelWidth - 4, RowHeight);
                var rowColor = i % 2 == 0 ? new Color(70, 60, 50, 150) : new Color(60, 50, 40, 150);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowColor);

                // Player info
                var textColor = IsAdminIcon(player) ? new Color(255, 215, 0) : Color.White;
                _spriteBatch.DrawString(_font, player.Name, new Vector2(pos.X + ColName, rowY), textColor);
                _spriteBatch.DrawString(_font, player.Title, new Vector2(pos.X + ColTitle, rowY), _styleProvider.TextSecondary);
                _spriteBatch.DrawString(_font, player.Guild, new Vector2(pos.X + ColGuild, rowY), _styleProvider.TextSecondary);
                _spriteBatch.DrawString(_font, player.Class, new Vector2(pos.X + ColClass, rowY), _styleProvider.TextSecondary);
            }

            // Draw scrollbar on right side
            var scrollX = (int)pos.X + PanelWidth - 20;
            var scrollAreaTop = (int)pos.Y + HeaderHeight + 2;
            var scrollAreaHeight = PanelHeight - HeaderHeight - 6;

            // Scroll track background
            _scrollTrackRect = new Rectangle(scrollX, scrollAreaTop, 16, scrollAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollTrackRect, new Color(40, 35, 30, 200));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _scrollTrackRect, new Color(80, 70, 60), 1);

            // Up button
            _scrollUpRect = new Rectangle(scrollX, scrollAreaTop, 16, 16);
            var upColor = _scrollOffset > 0 ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollUpRect, upColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _scrollUpRect, Color.Black, 1);
            _spriteBatch.DrawString(_font, "▲", new Vector2(_scrollUpRect.X + 3, _scrollUpRect.Y + 2), Color.White);

            // Down button
            _scrollDownRect = new Rectangle(scrollX, scrollAreaTop + scrollAreaHeight - 16, 16, 16);
            var downColor = _scrollOffset < Math.Max(0, _filteredList.Count - VisibleRows) ? _styleProvider.ButtonNormal : new Color(60, 55, 50);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _scrollDownRect, downColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, _scrollDownRect, Color.Black, 1);
            _spriteBatch.DrawString(_font, "▼", new Vector2(_scrollDownRect.X + 3, _scrollDownRect.Y + 2), Color.White);

            // Scroll thumb (position indicator)
            if (_filteredList.Count > VisibleRows)
            {
                var thumbTrackHeight = scrollAreaHeight - 36; // Space between buttons
                var thumbHeight = Math.Max(10, thumbTrackHeight * VisibleRows / _filteredList.Count);
                var maxOffset = _filteredList.Count - VisibleRows;
                var thumbY = scrollAreaTop + 17 + (int)((thumbTrackHeight - thumbHeight) * _scrollOffset / (float)maxOffset);
                var thumbRect = new Rectangle(scrollX + 2, thumbY, 12, thumbHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, thumbRect, _styleProvider.ButtonNormal);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, thumbRect, new Color(120, 110, 100), 1);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        private void ApplyFilter()
        {
            switch (_filter)
            {
                case Filter.Friends:
                    _filteredList = _onlineList.Where(x => _friendList.Contains(x.Name, StringComparer.InvariantCultureIgnoreCase)).ToList();
                    break;
                case Filter.Admins:
                    _filteredList = _onlineList.Where(IsAdminIcon).ToList();
                    break;
                case Filter.Party:
                    _filteredList = _onlineList.Where(x => _partyDataProvider.Members.Any(y => string.Equals(y.Name, x.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();
                    break;
                case Filter.All:
                default:
                    _filteredList = new List<OnlinePlayerInfo>(_onlineList);
                    break;
            }
            _scrollOffset = Math.Min(_scrollOffset, Math.Max(0, _filteredList.Count - VisibleRows));
        }

        private static bool IsAdminIcon(OnlinePlayerInfo onlineInfo)
        {
            return onlineInfo.Icon switch
            {
                CharacterIcon.Gm or CharacterIcon.Hgm or CharacterIcon.GmParty or CharacterIcon.HgmParty => true,
                _ => false,
            };
        }

        private Point TransformMousePosition(Point position)
        {
            if (!_clientWindowSizeProvider.IsScaledMode)
                return position;

            var offset = _clientWindowSizeProvider.RenderOffset;
            var scale = _clientWindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Clamp(gameX, 0, _clientWindowSizeProvider.GameWidth - 1),
                Math.Clamp(gameY, 0, _clientWindowSizeProvider.GameHeight - 1));
        }
    }
}
