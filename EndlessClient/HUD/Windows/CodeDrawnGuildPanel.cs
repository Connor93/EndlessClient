using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD.Panels;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// A tabbed guild management panel providing GUI access to all guild features.
    /// Sends #guild commands via local chat for server interaction.
    /// </summary>
    public class CodeDrawnGuildPanel : DraggableHudPanel, IZOrderedWindow
    {
        private enum GuildTab { Overview, Members, Bounties, Perks, Buffs }

        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IQuestActions _questActions;
        private readonly IChatActions _chatActions;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ITextMultiInputDialogFactory _textMultiInputDialogFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataRepository _lockerDataRepository;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;

        private const int PanelWidth = 280;
        private const int PanelHeight = 300;
        private const int HeaderHeight = 20;
        private const int TabBarHeight = 22;
        private const int TabWidth = 56;
        private const int TabCount = 5;
        private const int Padding = 6;
        private const int RowHeight = 16;
        private const int ExpBarHeight = 10;
        private const int ContentAreaHeight = PanelHeight - HeaderHeight - TabBarHeight - 36; // visible scrollable area
        private const double PollIntervalSeconds = 2.0;

        private GuildTab _activeTab = GuildTab.Overview;
        private GuildInfoData _guildInfo;
        private IReadOnlyList<CustomBountyData> _customBounties = new List<CustomBountyData>();
        private IReadOnlyList<GuildPerkData> _guildPerks = new List<GuildPerkData>();
        private IReadOnlyList<GuildBuffData> _guildBuffs = new List<GuildBuffData>();
        private IReadOnlyList<GuildMemberInfo> _guildMemberList = new List<GuildMemberInfo>();

        // Per-tab scroll offset (in logical pixels)
        private readonly float[] _tabScrollOffsets = new float[TabCount];
        private readonly Stopwatch _pollStopwatch = new Stopwatch();

        // Blue/silver theme
        private static readonly Color HeaderColor = new Color(40, 55, 85, 230);
        private static readonly Color HeaderAccent = new Color(180, 200, 230);
        private static readonly Color TabActiveColor = new Color(50, 70, 100, 240);
        private static readonly Color TabInactiveColor = new Color(30, 40, 60, 200);
        private static readonly Color TabHoverColor = new Color(60, 80, 110, 220);
        private static readonly Color TabTextActive = new Color(220, 230, 240);
        private static readonly Color TabTextInactive = new Color(140, 150, 170);
        private static readonly Color ExpBarBg = new Color(30, 30, 30, 200);
        private static readonly Color ExpBarFill = new Color(80, 160, 220);
        private static readonly Color BuffActiveColor = new Color(100, 220, 130);
        private static readonly Color SectionHeaderColor = new Color(160, 180, 210);
        private static readonly Color ActionButtonBg = new Color(60, 100, 140, 220);
        private static readonly Color ActionButtonHover = new Color(80, 120, 160, 240);
        private static readonly Color ActionButtonText = new Color(220, 235, 255);
        private static readonly Color DividerColor = new Color(70, 85, 110, 150);

        // Tab hover tracking
        private int _hoveredTabIndex = -1;

        // Action button hover tracking
        private int _hoveredActionIndex = -1;
        private Rectangle[] _actionButtonRects = Array.Empty<Rectangle>();
        private string[] _actionButtonLabels = Array.Empty<string>();
        private string[] _actionButtonCommands = Array.Empty<string>();

        public CodeDrawnGuildPanel(
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IBountyDataProvider bountyDataProvider,
            IQuestActions questActions,
            IChatActions chatActions,
            ITextInputDialogFactory textInputDialogFactory,
            ITextMultiInputDialogFactory textMultiInputDialogFactory,
            ICharacterProvider characterProvider,
            ILockerDataRepository lockerDataRepository)
            : base(true)
        {
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _bountyDataProvider = bountyDataProvider;
            _questActions = questActions;
            _chatActions = chatActions;
            _textInputDialogFactory = textInputDialogFactory;
            _textMultiInputDialogFactory = textMultiInputDialogFactory;
            _characterProvider = characterProvider;
            _lockerDataRepository = lockerDataRepository;
            _contentProvider = contentProvider;
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
            base.Initialize();
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                _questActions.RequestQuestHistory(QuestPage.Progress);
                _pollStopwatch.Restart();
            }
            else
            {
                _pollStopwatch.Stop();
            }
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MonoGame.Extended.Input.MouseButton.Left)
                return base.HandleClick(control, eventArgs);

            // Convert screen-space mouse position to logical coordinates
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var logicalMouseX = (int)((eventArgs.Position.X - offset.X) / scale);
            var logicalMouseY = (int)((eventArgs.Position.Y - offset.Y) / scale);

            var localX = logicalMouseX - DrawAreaWithParentOffset.X;
            var localY = logicalMouseY - DrawAreaWithParentOffset.Y;

            // Tab bar click detection
            if (localY >= HeaderHeight && localY < HeaderHeight + TabBarHeight)
            {
                var tabIndex = localX / TabWidth;
                if (tabIndex >= 0 && tabIndex < TabCount)
                {
                    _activeTab = (GuildTab)tabIndex;
                    _tabScrollOffsets[(int)_activeTab] = 0;
                    RebuildActionButtons();
                    return true;
                }
            }

            // Action button click detection (resolve relative rects to absolute)
            var area = DrawAreaWithParentOffset;
            for (int i = 0; i < _actionButtonRects.Length; i++)
            {
                var absRect = new Rectangle(
                    _actionButtonRects[i].X + area.X,
                    _actionButtonRects[i].Y + area.Y,
                    _actionButtonRects[i].Width,
                    _actionButtonRects[i].Height);
                if (absRect.Contains(logicalMouseX, logicalMouseY))
                {
                    HandleActionButtonClick(i);
                    return true;
                }
            }

            return base.HandleClick(control, eventArgs);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (Visible)
            {
                if (!_pollStopwatch.IsRunning)
                    _pollStopwatch.Start();

                if (_pollStopwatch.Elapsed.TotalSeconds >= PollIntervalSeconds)
                {
                    _pollStopwatch.Restart();
                    _questActions.RequestQuestHistory(QuestPage.Progress);
                }

                var currentInfo = _bountyDataProvider.GuildInfo;
                if (currentInfo != _guildInfo)
                {
                    _guildInfo = currentInfo;
                    RebuildActionButtons();
                }

                var currentBounties = _bountyDataProvider.CustomBounties;
                if (currentBounties != _customBounties)
                {
                    _customBounties = currentBounties;
                    if (_activeTab == GuildTab.Bounties)
                        RebuildActionButtons();
                }

                var currentPerks = _bountyDataProvider.GuildPerks;
                if (currentPerks != _guildPerks)
                {
                    _guildPerks = currentPerks;
                    if (_activeTab == GuildTab.Perks)
                        RebuildActionButtons();
                }

                var currentBuffs = _bountyDataProvider.GuildBuffs;
                if (currentBuffs != _guildBuffs)
                {
                    _guildBuffs = currentBuffs;
                    if (_activeTab == GuildTab.Buffs)
                        RebuildActionButtons();
                }

                var currentMembers = _bountyDataProvider.GuildMemberList;
                if (currentMembers != _guildMemberList)
                {
                    _guildMemberList = currentMembers;
                }

                HandleScrollWheel();
                UpdateHoverState();
            }
            else
            {
                _pollStopwatch.Stop();
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdateHoverState()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

            // Convert screen-space mouse position to logical coordinates
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var mouseX = (int)((mouseState.X - offset.X) / scale);
            var mouseY = (int)((mouseState.Y - offset.Y) / scale);

            var area = DrawAreaWithParentOffset;

            // Tab hover
            var localX = mouseX - area.X;
            var localY = mouseY - area.Y;
            if (localY >= HeaderHeight && localY < HeaderHeight + TabBarHeight && localX >= 0 && localX < TabWidth * TabCount)
            {
                _hoveredTabIndex = localX / TabWidth;
            }
            else
            {
                _hoveredTabIndex = -1;
            }

            // Action button hover (resolve relative rects to absolute)
            _hoveredActionIndex = -1;
            for (int i = 0; i < _actionButtonRects.Length; i++)
            {
                var absRect = new Rectangle(
                    _actionButtonRects[i].X + area.X,
                    _actionButtonRects[i].Y + area.Y,
                    _actionButtonRects[i].Width,
                    _actionButtonRects[i].Height);
                if (absRect.Contains(mouseX, mouseY))
                {
                    _hoveredActionIndex = i;
                    break;
                }
            }
        }

        private int _prevScrollValue;

        private void HandleScrollWheel()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

            // Only scroll when mouse is over the content area
            var scale = _clientWindowSizeProvider.ScaleFactor;
            var offset = _clientWindowSizeProvider.RenderOffset;
            var mouseX = (int)((mouseState.X - offset.X) / scale);
            var mouseY = (int)((mouseState.Y - offset.Y) / scale);
            var area = DrawAreaWithParentOffset;
            var contentTop = area.Y + HeaderHeight + TabBarHeight;
            var contentBottom = area.Y + PanelHeight - 36;

            if (mouseX < area.X || mouseX > area.X + PanelWidth ||
                mouseY < contentTop || mouseY > contentBottom)
            {
                _prevScrollValue = mouseState.ScrollWheelValue;
                return;
            }

            var delta = mouseState.ScrollWheelValue - _prevScrollValue;
            _prevScrollValue = mouseState.ScrollWheelValue;

            if (delta == 0 || _activeTab == GuildTab.Overview) return;

            var tabIndex = (int)_activeTab;
            var scrollChange = -delta / 4f; // positive delta = scroll up
            _tabScrollOffsets[tabIndex] = Math.Max(0, _tabScrollOffsets[tabIndex] + scrollChange);

            // Clamp to max content height
            var maxScroll = GetContentHeight(_activeTab) - ContentAreaHeight;
            if (maxScroll < 0) maxScroll = 0;
            _tabScrollOffsets[tabIndex] = Math.Min(_tabScrollOffsets[tabIndex], maxScroll);
        }

        private float GetContentHeight(GuildTab tab)
        {
            switch (tab)
            {
                case GuildTab.Members:
                    return Padding + RowHeight + _guildMemberList.Count * RowHeight;
                case GuildTab.Perks:
                    return Padding + RowHeight + _guildPerks.Count * (RowHeight * 2 + 4);
                case GuildTab.Buffs:
                    return Padding + RowHeight + _guildBuffs.Count * (RowHeight * 3 + 6);
                case GuildTab.Bounties:
                    var h = Padding + RowHeight; // header
                    var dailyBounties = _bountyDataProvider.Bounties;
                    if (dailyBounties != null)
                        h += dailyBounties.Count * (RowHeight + 14);
                    h += 6 + RowHeight; // divider + request board header
                    h += _customBounties.Count * (RowHeight * 2 + 4);
                    return h;
                default:
                    return 0;
            }
        }

        private void DrawMembersTab(Vector2 pos, float scale, BitmapFont font)
        {
            var scrollOffset = _tabScrollOffsets[(int)GuildTab.Members] * scale;
            var y = pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale - scrollOffset;
            var clipTop = pos.Y + (HeaderHeight + TabBarHeight) * scale;
            var clipBottom = pos.Y + (PanelHeight - 36) * scale;

            // Column header
            if (y >= clipTop)
            {
                _spriteBatch.DrawString(font, "Name",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
                _spriteBatch.DrawString(font, "Lv",
                    new Vector2(pos.X + (PanelWidth - 100) * scale, y), SectionHeaderColor);
                _spriteBatch.DrawString(font, "GP",
                    new Vector2(pos.X + (PanelWidth - 50) * scale, y), SectionHeaderColor);
            }
            y += RowHeight * scale;

            if (_guildMemberList.Count == 0)
            {
                if (y >= clipTop && y < clipBottom)
                    _spriteBatch.DrawString(font, "Loading member data...",
                        new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
                return;
            }

            foreach (var member in _guildMemberList)
            {
                if (y > clipBottom) break;

                if (y + RowHeight * scale > clipTop && y < clipBottom)
                {
                    // Name (left aligned)
                    var displayName = member.Name;
                    if (displayName.Length > 16)
                        displayName = displayName.Substring(0, 14) + "..";
                    _spriteBatch.DrawString(font, displayName,
                        new Vector2(pos.X + Padding * scale, y), _styleProvider.TextPrimary);

                    // Level (right-center)
                    var lvText = member.Level.ToString();
                    _spriteBatch.DrawString(font, lvText,
                        new Vector2(pos.X + (PanelWidth - 100) * scale, y),
                        new Color(140, 180, 220));

                    // Lifetime GP (right)
                    var gpText = member.LifetimeGuildPoints.ToString("N0");
                    _spriteBatch.DrawString(font, gpText,
                        new Vector2(pos.X + (PanelWidth - 50) * scale, y),
                        new Color(220, 180, 60));
                }

                y += RowHeight * scale;
            }
        }

        /// <summary>
        /// Rebuilds action button data. All rects are stored as panel-relative
        /// (origin 0,0 = top-left of panel). They are resolved to absolute
        /// screen coords at render/click/hover time using DrawAreaWithParentOffset.
        /// </summary>
        private void RebuildActionButtons()
        {
            var contentY = HeaderHeight + TabBarHeight + Padding;

            switch (_activeTab)
            {
                case GuildTab.Overview:
                    var overviewBtnY = PanelHeight - 28;
                    _actionButtonRects = new[]
                    {
                        new Rectangle(Padding, overviewBtnY, 90, 20),
                        new Rectangle(Padding + 98, overviewBtnY, 70, 20),
                        new Rectangle(Padding + 176, overviewBtnY, 60, 20),
                    };
                    _actionButtonLabels = new[] { "Donate Gold", "Storage", "Inbox" };
                    _actionButtonCommands = new[] { "donate", "#guild storage", "#guild inbox" };
                    break;

                case GuildTab.Bounties:
                    var btyButtons = new List<Rectangle>();
                    var btyLabels = new List<string>();
                    var btyCommands = new List<string>();

                    btyButtons.Add(new Rectangle(Padding, PanelHeight - 28, 80, 20));
                    btyLabels.Add("Refresh");
                    btyCommands.Add("refresh_bounties");

                    btyButtons.Add(new Rectangle(Padding + 88, PanelHeight - 28, 95, 20));
                    btyLabels.Add("Post Request");
                    btyCommands.Add("post_bounty");

                    // Per-bounty action buttons
                    var myName = _characterProvider?.MainCharacter?.Name ?? "";
                    var bountyContentY = HeaderHeight + TabBarHeight + Padding;
                    // Skip daily bounties section header
                    var btyLineY = bountyContentY + RowHeight;
                    // Account for daily bounties (each takes RowHeight + progress bar height)
                    var dailyBounties = _bountyDataProvider.Bounties;
                    if (dailyBounties != null && dailyBounties.Count > 0)
                    {
                        foreach (var _ in dailyBounties)
                        {
                            btyLineY += RowHeight; // name line
                            btyLineY += 14;        // bar + gap
                        }
                    }
                    // "Request Board:" header
                    btyLineY += RowHeight + 4;

                    foreach (var bounty in _customBounties)
                    {
                        // Each bounty entry uses 2 lines
                        if (btyLineY + RowHeight * 2 > PanelHeight - 36) break;

                        string btnLabel;
                        string btnCmd;
                        if (bounty.Status == CustomBountyStatus.Open && !string.Equals(bounty.Poster, myName, StringComparison.OrdinalIgnoreCase))
                        {
                            btnLabel = "Accept";
                            btnCmd = "#guild accept " + bounty.Id;
                        }
                        else if (bounty.Status == CustomBountyStatus.Accepted && string.Equals(bounty.AcceptedBy, myName, StringComparison.OrdinalIgnoreCase))
                        {
                            btnLabel = "Deliver";
                            btnCmd = "#guild deliver " + bounty.Id;
                        }
                        else if (string.Equals(bounty.Poster, myName, StringComparison.OrdinalIgnoreCase))
                        {
                            btnLabel = "Cancel";
                            btnCmd = "#guild cancel " + bounty.Id;
                        }
                        else
                        {
                            btyLineY += RowHeight * 2 + 4; // skip this bounty
                            continue;
                        }

                        btyButtons.Add(new Rectangle(PanelWidth - Padding - 55, btyLineY, 50, 16));
                        btyLabels.Add(btnLabel);
                        btyCommands.Add(btnCmd);

                        btyLineY += RowHeight * 2 + 4;
                    }

                    _actionButtonRects = btyButtons.ToArray();
                    _actionButtonLabels = btyLabels.ToArray();
                    _actionButtonCommands = btyCommands.ToArray();
                    break;

                case GuildTab.Perks:
                    var perkButtons = new List<Rectangle>();
                    var perkLabels = new List<string>();
                    var perkCommands = new List<string>();

                    // Per-perk unlock buttons for available (not yet unlocked) perks
                    var perkLineY = contentY + RowHeight; // skip header
                    var guildLevel = _guildInfo?.Level ?? 0;
                    foreach (var perk in _guildPerks)
                    {
                        // No longer clip — scroll handles visibility

                        if (!perk.IsUnlocked && guildLevel >= perk.RequiredLevel)
                        {
                            perkButtons.Add(new Rectangle(PanelWidth - Padding - 55, perkLineY + 2, 50, 16));
                            perkLabels.Add("Unlock");
                            perkCommands.Add("#guild upgrade " + perk.PerkIndex);
                        }

                        perkLineY += RowHeight * 2 + 4;
                    }

                    _actionButtonRects = perkButtons.ToArray();
                    _actionButtonLabels = perkLabels.ToArray();
                    _actionButtonCommands = perkCommands.ToArray();
                    break;

                case GuildTab.Buffs:
                    var buffButtons = new List<Rectangle>();
                    var buffLabels = new List<string>();
                    var buffCommands = new List<string>();

                    // Per-buff toggle buttons for unlocked buffs
                    var buffLineY = contentY + RowHeight; // skip header
                    foreach (var buff in _guildBuffs)
                    {
                        // No longer clip — scroll handles visibility

                        if (buff.IsUnlocked)
                        {
                            var label = buff.IsActive ? "Deactivate" : "Activate";
                            var cmd = buff.IsActive
                                ? "#guild buffs deactivate " + buff.DisplayName
                                : "#guild buffs activate " + buff.DisplayName;
                            buffButtons.Add(new Rectangle(PanelWidth - Padding - 65, buffLineY + 2, 60, 16));
                            buffLabels.Add(label);
                            buffCommands.Add(cmd);
                        }

                        buffLineY += RowHeight * 3 + 6;
                    }

                    _actionButtonRects = buffButtons.ToArray();
                    _actionButtonLabels = buffLabels.ToArray();
                    _actionButtonCommands = buffCommands.ToArray();
                    break;

                case GuildTab.Members:
                    _actionButtonRects = Array.Empty<Rectangle>();
                    _actionButtonLabels = Array.Empty<string>();
                    _actionButtonCommands = Array.Empty<string>();
                    break;
            }
        }

        private void HandleActionButtonClick(int buttonIndex)
        {
            var command = _actionButtonCommands[buttonIndex];

            if (command == "donate")
            {
                ShowDonateDialog();
                return;
            }

            if (command == "post_bounty")
            {
                ShowPostBountyDialog();
                return;
            }

            if (command == "refresh_bounties")
            {
                _questActions.RequestQuestHistory(QuestPage.Progress);
                return;
            }

            if (command == "#guild storage")
                _lockerDataRepository.Context = LockerContext.GuildStorage;
            else if (command == "#guild inbox")
                _lockerDataRepository.Context = LockerContext.DeliveryInbox;

            _chatActions.SendChatToServer(command, string.Empty, ChatType.Command);
        }

        private void ShowDonateDialog()
        {
            var dlg = _textInputDialogFactory.Create("Enter gold amount to donate (max 50,000/day):", maxInputChars: 6);
            dlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var text = dlg.ResponseText?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.All(char.IsDigit) && int.TryParse(text, out var amount) && amount > 0)
                    {
                        _chatActions.SendChatToServer("#guild donate " + amount, string.Empty, ChatType.Command);
                    }
                }
            };
            dlg.Initialize();
            dlg.Show();
        }

        private void ShowPostBountyDialog()
        {
            var dlg = _textMultiInputDialogFactory.Create(
                "Post Bounty Request",
                "Enter the item name and amount you need:",
                TextMultiInputDialog.DialogSize.Two,
                new TextMultiInputDialog.InputInfo("Item Name:", MaxChars: 24),
                new TextMultiInputDialog.InputInfo("Amount:", MaxChars: 6, InputRestriction: TextMultiInputDialog.InputInfo.InputRestrict.Numeric));

            dlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var responses = dlg.Responses;
                    var itemName = responses[0]?.Trim();
                    var amountText = responses[1]?.Trim();
                    if (!string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(amountText) &&
                        int.TryParse(amountText, out var amount) && amount > 0)
                    {
                        _chatActions.SendChatToServer(
                            $"#guild post {itemName} {amount}",
                            string.Empty,
                            ChatType.Command);
                    }
                }
            };
            dlg.Initialize();
            dlg.Show();
        }

        // IZOrderedWindow implementation
        private int _zOrder = 10;
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

        public void BringToFront()
        {
        }

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.90f));

            _spriteBatch.End();
        }

        private void DrawPanelBordersAndText(Vector2 scaledPos, float scale)
        {
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scale);

            var scaledWidth = (int)(DrawArea.Width * scale);
            var scaledHeight = (int)(DrawArea.Height * scale);

            _spriteBatch.Begin();

            // Background
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.90f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Header
            var headerRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, (int)(HeaderHeight * scale));
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);

            DrawHeaderContent(scaledPos, scale, font);
            DrawTabBar(scaledPos, scale, font);
            DrawTabContent(scaledPos, scale, font);
            DrawActionButtons(scaledPos, scale, font);

            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos)
        {
            _spriteBatch.Begin();

            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, DrawArea.Height);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.90f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, DrawArea.Width, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, HeaderColor);

            DrawHeaderContent(pos, 1f, _labelFont);
            DrawTabBar(pos, 1f, _labelFont);
            DrawTabContent(pos, 1f, _labelFont);
            DrawActionButtons(pos, 1f, _labelFont);

            _spriteBatch.End();
        }

        private void DrawHeaderContent(Vector2 pos, float scale, BitmapFont font)
        {
            var title = "Guild";
            if (_guildInfo != null)
            {
                title = _guildInfo.GuildName;
                if (title.Length > 22)
                    title = title.Substring(0, 20) + "..";
                title += " [" + _guildInfo.GuildTag + "]";
            }
            _spriteBatch.DrawString(font, title,
                new Vector2(pos.X + Padding * scale, pos.Y + 3 * scale), HeaderAccent);
        }

        private void DrawTabBar(Vector2 pos, float scale, BitmapFont font)
        {
            var tabY = pos.Y + HeaderHeight * scale;
            var tabNames = new[] { "Overview", "Members", "Bounties", "Perks", "Buffs" };

            for (int i = 0; i < tabNames.Length; i++)
            {
                var tabX = pos.X + i * TabWidth * scale;
                var tabRect = new Rectangle((int)tabX, (int)tabY, (int)(TabWidth * scale), (int)(TabBarHeight * scale));

                Color tabBg;
                Color tabText;
                if ((GuildTab)i == _activeTab)
                {
                    tabBg = TabActiveColor;
                    tabText = TabTextActive;
                }
                else if (i == _hoveredTabIndex)
                {
                    tabBg = TabHoverColor;
                    tabText = TabTextActive;
                }
                else
                {
                    tabBg = TabInactiveColor;
                    tabText = TabTextInactive;
                }

                DrawingPrimitives.DrawFilledRect(_spriteBatch, tabRect, tabBg);

                // Active tab indicator line
                if ((GuildTab)i == _activeTab)
                {
                    var indicatorRect = new Rectangle((int)tabX, (int)(tabY + (TabBarHeight - 2) * scale), (int)(TabWidth * scale), (int)(2 * scale));
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, indicatorRect, HeaderAccent);
                }

                var textSize = font.MeasureString(tabNames[i]);
                _spriteBatch.DrawString(font, tabNames[i],
                    new Vector2(tabX + (TabWidth * scale - textSize.Width) / 2,
                                tabY + (TabBarHeight * scale - textSize.Height) / 2),
                    tabText);
            }
        }

        private void DrawTabContent(Vector2 pos, float scale, BitmapFont font)
        {
            switch (_activeTab)
            {
                case GuildTab.Overview:
                    DrawOverviewTab(pos, scale, font);
                    break;
                case GuildTab.Members:
                    DrawMembersTab(pos, scale, font);
                    break;
                case GuildTab.Bounties:
                    DrawBountiesTab(pos, scale, font);
                    break;
                case GuildTab.Perks:
                    DrawPerksTab(pos, scale, font);
                    break;
                case GuildTab.Buffs:
                    DrawBuffsTab(pos, scale, font);
                    break;
            }
        }

        private void DrawOverviewTab(Vector2 pos, float scale, BitmapFont font)
        {
            if (_guildInfo == null)
            {
                _spriteBatch.DrawString(font, "No guild data",
                    new Vector2(pos.X + Padding * scale, pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale),
                    _styleProvider.TextSecondary);
                return;
            }

            var y = pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale;

            // Level
            _spriteBatch.DrawString(font, "Level " + _guildInfo.Level,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextPrimary);

            // Online members (right-aligned)
            if (_guildInfo.OnlineCount > 0)
            {
                var onlineText = _guildInfo.OnlineCount + " Online";
                var onlineSize = font.MeasureString(onlineText);
                _spriteBatch.DrawString(font, onlineText,
                    new Vector2(pos.X + (PanelWidth - Padding) * scale - onlineSize.Width, y), BuffActiveColor);
            }
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
                    DrawingPrimitives.DrawFilledRect(_spriteBatch,
                        new Rectangle(barX, (int)y, fillWidth, barHeight), ExpBarFill);
                }
                var expText = _guildInfo.Exp + "/" + (_guildInfo.Exp + _guildInfo.ExpToNext);
                var expSize = _font.MeasureString(expText);
                _spriteBatch.DrawString(_font, expText,
                    new Vector2(barX + (barWidth - expSize.Width) / 2, y), Color.White);
            }
            else
            {
                DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, ExpBarFill);
                var maxSize = _font.MeasureString("MAX");
                _spriteBatch.DrawString(_font, "MAX",
                    new Vector2(barX + (barWidth - maxSize.Width) / 2, y), Color.White);
            }
            y += (ExpBarHeight + 4) * scale;

            // Points & Contribution
            _spriteBatch.DrawString(font, "Guild Points: " + _guildInfo.Points,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextPrimary);
            y += RowHeight * scale;

            _spriteBatch.DrawString(font, "My Contribution: " + _guildInfo.Contribution,
                new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
            y += RowHeight * scale;

            // Bank balance
            _spriteBatch.DrawString(font, "Guild Bank: " + _guildInfo.Bank.ToString("N0") + "g",
                new Vector2(pos.X + Padding * scale, y), new Color(220, 190, 100));
            y += RowHeight * scale;

            // Divider
            y += 4 * scale;
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle((int)(pos.X + Padding * scale), (int)y, (int)((PanelWidth - Padding * 2) * scale), 1),
                DividerColor);
            y += 6 * scale;

            // Active Buffs
            if (!string.IsNullOrEmpty(_guildInfo.ActiveBuffs))
            {
                _spriteBatch.DrawString(font, "Active Buffs:",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
                y += RowHeight * scale;

                var buffs = _guildInfo.ActiveBuffs.Split(',');
                var buffX = pos.X + Padding * scale + 8 * scale;
                foreach (var buff in buffs)
                {
                    var trimmed = buff.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var label = trimmed.Contains("tier1") ? "T1"
                              : trimmed.Contains("tier2") ? "T2"
                              : trimmed.Contains("tier3") ? "T3"
                              : trimmed;
                    _spriteBatch.DrawString(font, "• " + label,
                        new Vector2(buffX, y), BuffActiveColor);
                    buffX += font.MeasureString("• " + label + "  ").Width;
                }
            }
            else
            {
                _spriteBatch.DrawString(font, "No active buffs",
                    new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
            }
        }

        private void DrawBountiesTab(Vector2 pos, float scale, BitmapFont font)
        {
            var scrollOffset = _tabScrollOffsets[(int)GuildTab.Bounties] * scale;
            var y = pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale - scrollOffset;
            var clipTop = pos.Y + (HeaderHeight + TabBarHeight) * scale;
            var clipBottom = pos.Y + (PanelHeight - 36) * scale;

            // ── Section 1: Daily Bounties ──
            var dailyBounties = _bountyDataProvider.Bounties;
            if (y >= clipTop && y < clipBottom)
                _spriteBatch.DrawString(font, "Daily Bounties:",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
            y += RowHeight * scale;

            if (dailyBounties == null || dailyBounties.Count == 0)
            {
                if (y >= clipTop && y < clipBottom)
                    _spriteBatch.DrawString(font, "No daily bounties. Use Refresh.",
                        new Vector2(pos.X + (Padding + 4) * scale, y), _styleProvider.TextSecondary);
                y += RowHeight * scale;
            }
            else
            {
                foreach (var bounty in dailyBounties)
                {
                    if (y >= clipTop && y < clipBottom)
                        _spriteBatch.DrawString(font, bounty.Name,
                            new Vector2(pos.X + (Padding + 4) * scale, y), _styleProvider.TextPrimary);
                    y += RowHeight * scale;

                    if (y >= clipTop && y < clipBottom)
                    {
                        var barX = (int)(pos.X + (Padding + 8) * scale);
                        var barWidth = (int)((PanelWidth - Padding * 2 - 16) * scale);
                        var barHeight = (int)(8 * scale);
                        var barRect = new Rectangle(barX, (int)y, barWidth, barHeight);
                        DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, ExpBarBg);

                        if (bounty.Target > 0)
                        {
                            var fillWidth = (int)(barWidth * Math.Min(1.0, (double)bounty.Progress / bounty.Target));
                            if (fillWidth > 0)
                            {
                                var fillColor = bounty.Progress >= bounty.Target ? BuffActiveColor : ExpBarFill;
                                DrawingPrimitives.DrawFilledRect(_spriteBatch,
                                    new Rectangle(barX, (int)y, fillWidth, barHeight), fillColor);
                            }
                            var progressText = bounty.Progress + "/" + bounty.Target;
                            var textSize = _font.MeasureString(progressText);
                            _spriteBatch.DrawString(_font, progressText,
                                new Vector2(barX + (barWidth - textSize.Width) / 2, y - 1), Color.White);
                        }
                    }
                    y += 14 * scale;
                }
            }

            // ── Divider ──
            y += 2 * scale;
            if (y >= clipTop && y < clipBottom)
                DrawingPrimitives.DrawFilledRect(_spriteBatch,
                    new Rectangle((int)(pos.X + Padding * scale), (int)y, (int)((PanelWidth - Padding * 2) * scale), 1),
                    DividerColor);
            y += 4 * scale;

            // ── Section 2: Request Board ──
            if (y >= clipTop && y < clipBottom)
                _spriteBatch.DrawString(font, "Request Board:",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
            y += RowHeight * scale;

            var customBounties = _customBounties;
            if (customBounties == null || customBounties.Count == 0)
            {
                if (y >= clipTop && y < clipBottom)
                    _spriteBatch.DrawString(font, "No active requests.",
                        new Vector2(pos.X + (Padding + 4) * scale, y), _styleProvider.TextSecondary);
                y += RowHeight * scale;
                if (y >= clipTop && y < clipBottom)
                    _spriteBatch.DrawString(font, "Use Post Request to add one.",
                        new Vector2(pos.X + (Padding + 4) * scale, y), _styleProvider.TextSecondary);
            }
            else
            {
                var myName = _characterProvider?.MainCharacter?.Name ?? "";
                foreach (var bounty in customBounties)
                {
                    // Line 1: Item name x Amount
                    if (y >= clipTop && y < clipBottom)
                    {
                        var itemLine = $"{bounty.ItemName} x{bounty.Amount}";
                        _spriteBatch.DrawString(font, itemLine,
                            new Vector2(pos.X + (Padding + 4) * scale, y), _styleProvider.TextPrimary);
                    }
                    y += RowHeight * scale;

                    // Line 2: Status info
                    if (y >= clipTop && y < clipBottom)
                    {
                        string statusText;
                        Color statusColor;
                        if (bounty.Status == CustomBountyStatus.Open)
                        {
                            statusText = $"  Posted by {bounty.Poster}";
                            statusColor = _styleProvider.TextSecondary;
                        }
                        else
                        {
                            statusText = $"  {bounty.AcceptedBy} delivering";
                            statusColor = new Color(200, 180, 100);
                        }
                        _spriteBatch.DrawString(font, statusText,
                            new Vector2(pos.X + (Padding + 4) * scale, y), statusColor);
                    }
                    y += (RowHeight + 4) * scale;
                }
            }
        }

        private void DrawPerksTab(Vector2 pos, float scale, BitmapFont font)
        {
            var scrollOffset = _tabScrollOffsets[(int)GuildTab.Perks] * scale;
            var y = pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale - scrollOffset;
            var clipTop = pos.Y + (HeaderHeight + TabBarHeight) * scale;
            var clipBottom = pos.Y + (PanelHeight - 36) * scale;

            if (y >= clipTop)
                _spriteBatch.DrawString(font, "Guild Perks:",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
            y += RowHeight * scale;

            if (_guildPerks.Count == 0)
            {
                _spriteBatch.DrawString(font, "Loading perk data...",
                    new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
                return;
            }

            var guildLevel = _guildInfo?.Level ?? 0;
            var guildBank = _guildInfo?.Bank ?? 0;

            foreach (var perk in _guildPerks)
            {
                if (y > clipBottom) break;
                var rowVisible = (y + RowHeight * scale > clipTop && y < clipBottom);

                // Status indicator and name
                string status;
                Color statusColor;
                if (perk.IsUnlocked)
                {
                    status = "\u2713";
                    statusColor = BuffActiveColor;
                }
                else if (guildLevel >= perk.RequiredLevel)
                {
                    status = "\u25C6";
                    statusColor = new Color(220, 180, 60); // gold for available
                }
                else
                {
                    status = "\u2717";
                    statusColor = new Color(160, 80, 80); // red for locked
                }

                if (rowVisible)
                    _spriteBatch.DrawString(font, status + " " + perk.DisplayName,
                        new Vector2(pos.X + Padding * scale, y),
                        perk.IsUnlocked ? _styleProvider.TextPrimary : _styleProvider.TextSecondary);
                y += RowHeight * scale;

                // Detail line
                string detail;
                if (perk.IsUnlocked)
                {
                    detail = "Unlocked";
                }
                else if (guildLevel >= perk.RequiredLevel)
                {
                    detail = "Cost: " + perk.GoldCost.ToString("N0") + "g";
                    if (guildBank < perk.GoldCost)
                        detail += " (need more gold)";
                }
                else
                {
                    detail = "Requires Lv." + perk.RequiredLevel + ", " + perk.GoldCost.ToString("N0") + "g";
                }

                if (rowVisible)
                    _spriteBatch.DrawString(font, "  " + detail,
                        new Vector2(pos.X + Padding * scale, y),
                        new Color(120, 130, 150));
                y += RowHeight * scale + 4 * scale;
            }
        }

        private void DrawBuffsTab(Vector2 pos, float scale, BitmapFont font)
        {
            var scrollOffset = _tabScrollOffsets[(int)GuildTab.Buffs] * scale;
            var y = pos.Y + (HeaderHeight + TabBarHeight + Padding) * scale - scrollOffset;
            var clipTop = pos.Y + (HeaderHeight + TabBarHeight) * scale;
            var clipBottom = pos.Y + (PanelHeight - 36) * scale;

            if (y >= clipTop)
                _spriteBatch.DrawString(font, "Guild Buffs:",
                    new Vector2(pos.X + Padding * scale, y), SectionHeaderColor);
            y += RowHeight * scale;

            if (_guildBuffs.Count == 0)
            {
                _spriteBatch.DrawString(font, "Loading buff data...",
                    new Vector2(pos.X + Padding * scale, y), _styleProvider.TextSecondary);
                return;
            }

            foreach (var buff in _guildBuffs)
            {
                if (y > clipBottom) break;
                var rowVisible = (y + RowHeight * scale > clipTop && y < clipBottom);

                // Status and name
                string status;
                Color nameColor;
                if (buff.IsActive)
                {
                    status = "[ACTIVE]";
                    nameColor = BuffActiveColor;
                }
                else if (buff.IsUnlocked)
                {
                    status = "[Ready]";
                    nameColor = new Color(220, 180, 60);
                }
                else
                {
                    status = "[Locked]";
                    nameColor = new Color(160, 80, 80);
                }

                if (rowVisible)
                    _spriteBatch.DrawString(font, buff.DisplayName + " " + status,
                        new Vector2(pos.X + Padding * scale, y), nameColor);
                y += RowHeight * scale;

                // Stats line
                if (!string.IsNullOrEmpty(buff.StatsDescription) && rowVisible)
                {
                    _spriteBatch.DrawString(font, "  " + buff.StatsDescription,
                        new Vector2(pos.X + Padding * scale, y),
                        new Color(140, 180, 220));
                }
                y += RowHeight * scale;

                // Upkeep line
                if (rowVisible)
                    _spriteBatch.DrawString(font, "  Upkeep: " + buff.UpkeepPoints + "pts + " + buff.UpkeepGold.ToString("N0") + "g/day",
                        new Vector2(pos.X + Padding * scale, y),
                        new Color(120, 130, 150));
                y += RowHeight * scale + 6 * scale;
            }
        }

        private void DrawActionButtons(Vector2 pos, float scale, BitmapFont font)
        {
            for (int i = 0; i < _actionButtonRects.Length; i++)
            {
                // Resolve panel-relative rect to absolute screen position
                var rel = _actionButtonRects[i];
                var absRect = new Rectangle(
                    (int)(pos.X + rel.X * scale),
                    (int)(pos.Y + rel.Y * scale),
                    (int)(rel.Width * scale),
                    (int)(rel.Height * scale));

                var bg = i == _hoveredActionIndex ? ActionButtonHover : ActionButtonBg;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, absRect, bg);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, absRect, _styleProvider.PanelBorder, 1);

                var textSize = font.MeasureString(_actionButtonLabels[i]);
                _spriteBatch.DrawString(font, _actionButtonLabels[i],
                    new Vector2(
                        absRect.X + (absRect.Width - textSize.Width) / 2,
                        absRect.Y + (absRect.Height - textSize.Height) / 2),
                    ActionButtonText);
            }
        }
    }
}
