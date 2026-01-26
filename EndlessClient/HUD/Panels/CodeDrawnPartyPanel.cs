using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Party;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Vertical MMO-style party panel that floats on the left side of the screen.
    /// Shows party members stacked with Name, Level, Class, and Health bar.
    /// </summary>
    public class CodeDrawnPartyPanel : CodeDrawnHudPanelBase
    {
        private readonly IPartyActions _partyActions;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _nameFont;
        private readonly BitmapFont _detailFont;
        private readonly BitmapFont _scaledNameFont;
        private readonly BitmapFont _scaledDetailFont;

        private const int PanelWidth = 160;
        private const int MemberRowHeight = 45;
        private const int HeaderHeight = 20;
        private const int Padding = 4;
        private const int HealthBarHeight = 8;
        private const int RemoveButtonSize = 14;

        private HashSet<PartyMember> _cachedParty;
        private readonly Dictionary<int, Rectangle> _removeButtonRects;
        private Rectangle _leaveButtonRect;
        private bool _wasMouseDown;

        public CodeDrawnPartyPanel(IPartyActions partyActions,
                                   IPartyDataProvider partyDataProvider,
                                   ICharacterProvider characterProvider,
                                   IUIStyleProvider styleProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider,
                                   IContentProvider contentProvider,
                                   IClientWindowSizeProvider clientWindowSizeProvider)
            : base(clientWindowSizeProvider)
        {
            _partyActions = partyActions;
            _partyDataProvider = partyDataProvider;
            _characterProvider = characterProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _nameFont = contentProvider.Fonts[Constants.FontSize09];
            _detailFont = contentProvider.Fonts[Constants.FontSize08];
            _scaledNameFont = contentProvider.Fonts[Constants.FontSize10];
            _scaledDetailFont = contentProvider.Fonts[Constants.FontSize10];

            _cachedParty = new HashSet<PartyMember>();
            _removeButtonRects = new Dictionary<int, Rectangle>();

            // Position on left side of screen, below top UI
            DrawArea = new Rectangle(5, 100, PanelWidth, HeaderHeight);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (!_cachedParty.SetEquals(_partyDataProvider.Members))
            {
                _cachedParty = _partyDataProvider.Members.ToHashSet();

                // Update panel height based on member count
                var memberCount = _cachedParty.Count;
                var panelHeight = HeaderHeight + (memberCount * MemberRowHeight) + Padding;
                DrawArea = new Rectangle(DrawArea.X, DrawArea.Y, PanelWidth, panelHeight);
            }

            // Handle button clicks
            var mouseState = Mouse.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);
            var isMouseDown = mouseState.LeftButton == ButtonState.Pressed;

            // Fire Activated when mouse is pressed inside panel to trigger z-order update
            var panelRect = new Rectangle(DrawArea.X, DrawArea.Y, PanelWidth, DrawArea.Height);
            if (isMouseDown && !_wasMouseDown && panelRect.Contains(mousePos))
            {
                OnActivated();
            }

            // Detect click (mouse up after mouse down)
            if (_wasMouseDown && !isMouseDown)
            {
                // Check leave button
                if (_leaveButtonRect.Contains(mousePos))
                {
                    _partyActions.RemovePartyMember(_characterProvider.MainCharacter.ID);
                }

                // Check remove buttons (only if leader)
                var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);
                if (isLeader)
                {
                    foreach (var kvp in _removeButtonRects)
                    {
                        if (kvp.Value.Contains(mousePos) && kvp.Key != _characterProvider.MainCharacter.ID)
                        {
                            _partyActions.RemovePartyMember(kvp.Key);
                            break;
                        }
                    }
                }
            }

            _wasMouseDown = isMouseDown;

            base.OnUpdateControl(gameTime);
        }

        protected override void OnVisibleChanged(object sender, System.EventArgs args)
        {
            if (Visible)
            {
                _partyActions.ListParty();
            }

            base.OnVisibleChanged(sender, args);
        }


        protected override void OnDrawControl(GameTime gameTime)
        {
            if (_cachedParty.Count == 0)
            {
                base.OnDrawControl(gameTime);
                return;
            }

            _removeButtonRects.Clear();

            // Delegate to base class for draw dispatch
            base.OnDrawControl(gameTime);
        }

        public override void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible || _cachedParty.Count == 0) return;

            var scaledPos = CalculateScaledPosition(scaleFactor, renderOffset);

            // Draw fills first, then text/borders
            DrawFillsScaled(scaledPos, scaleFactor);
            DrawBordersAndTextScaled(scaledPos, scaleFactor);
        }

        protected override void DrawFillsScaled(Vector2 pos, float scale)
        {
            _spriteBatch.Begin();

            var panelHeight = (int)((HeaderHeight + (_cachedParty.Count * MemberRowHeight) + Padding) * scale);
            var scaledWidth = (int)(PanelWidth * scale);
            var scaledHeaderHeight = (int)(HeaderHeight * scale);
            var scaledRowHeight = (int)(MemberRowHeight * scale);

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, scaledWidth, panelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.9f));

            // Header fill
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, scaledWidth, scaledHeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));

            // Leave button fill
            var scaledBtnSize = (int)(RemoveButtonSize * scale);
            var leaveRect = new Rectangle((int)pos.X + scaledWidth - scaledBtnSize - (int)(4 * scale), (int)pos.Y + (int)(3 * scale), scaledBtnSize, scaledBtnSize);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, leaveRect, new Color(180, 60, 60));

            // Member row fills and health bar backgrounds
            var memberIndex = 0;
            var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);
            foreach (var member in _cachedParty.OrderByDescending(m => m.IsLeader))
            {
                var rowY = (int)pos.Y + scaledHeaderHeight + (memberIndex * scaledRowHeight);
                var rowRect = new Rectangle((int)pos.X + (int)(2 * scale), rowY, scaledWidth - (int)(4 * scale), scaledRowHeight - (int)(2 * scale));
                var rowBgColor = memberIndex % 2 == 0 ? new Color(80, 70, 60, 180) : new Color(70, 60, 50, 180);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowBgColor);

                // Remove button fill
                if (isLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
                {
                    var removeRect = new Rectangle((int)pos.X + scaledWidth - scaledBtnSize - (int)(8 * scale), rowY + (int)(2 * scale), scaledBtnSize, scaledBtnSize);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, removeRect, new Color(140, 50, 50));
                }

                // Health bar background
                var healthBarY = rowY + scaledRowHeight - (int)(HealthBarHeight * scale) - (int)(4 * scale);
                var healthBarRect = new Rectangle((int)pos.X + (int)(Padding * scale), healthBarY, scaledWidth - (int)((Padding * 2 + 4) * scale), (int)(HealthBarHeight * scale));
                DrawingPrimitives.DrawFilledRect(_spriteBatch, healthBarRect, new Color(40, 40, 40, 200));

                // Health bar foreground
                var healthPercent = member.PercentHealth;
                var healthColor = healthPercent > 0.5f ? new Color(60, 180, 60) : healthPercent > 0.25f ? new Color(200, 180, 60) : new Color(200, 60, 60);
                var healthWidth = (int)((healthBarRect.Width - 4) * healthPercent);
                var healthFillRect = new Rectangle(healthBarRect.X + 2, healthBarRect.Y + 2, healthWidth, healthBarRect.Height - 4);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, healthFillRect, healthColor);

                memberIndex++;
            }

            _spriteBatch.End();
        }

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            var panelHeight = HeaderHeight + (_cachedParty.Count * MemberRowHeight) + Padding;

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, panelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.9f));

            // Header fill
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));

            // Leave button fill
            _leaveButtonRect = new Rectangle((int)pos.X + PanelWidth - RemoveButtonSize - 4, (int)pos.Y + 3, RemoveButtonSize, RemoveButtonSize);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, _leaveButtonRect, new Color(180, 60, 60));

            // Member row fills and health bar backgrounds
            var memberIndex = 0;
            var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);
            foreach (var member in _cachedParty.OrderByDescending(m => m.IsLeader))
            {
                var rowY = (int)pos.Y + HeaderHeight + (memberIndex * MemberRowHeight);
                var rowRect = new Rectangle((int)pos.X + 2, rowY, PanelWidth - 4, MemberRowHeight - 2);
                var rowBgColor = memberIndex % 2 == 0 ? new Color(80, 70, 60, 180) : new Color(70, 60, 50, 180);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowBgColor);

                // Remove button fill
                if (isLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
                {
                    var removeRect = new Rectangle((int)pos.X + PanelWidth - RemoveButtonSize - 8, rowY + 2, RemoveButtonSize, RemoveButtonSize);
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, removeRect, new Color(140, 50, 50));
                    _removeButtonRects[member.CharacterID] = removeRect;
                }

                // Health bar background
                var healthBarY = rowY + MemberRowHeight - HealthBarHeight - 4;
                var healthBarRect = new Rectangle((int)pos.X + Padding, healthBarY, PanelWidth - (Padding * 2) - 4, HealthBarHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, healthBarRect, new Color(40, 40, 40, 200));

                // Health bar fill
                var healthPercent = member.PercentHealth / 100f;
                var healthFillWidth = (int)((healthBarRect.Width - 2) * healthPercent);
                var healthColor = member.PercentHealth > 50 ? new Color(80, 180, 80) :
                                  member.PercentHealth > 25 ? new Color(220, 180, 50) :
                                  new Color(200, 60, 60);
                var healthFillRect = new Rectangle(healthBarRect.X + 1, healthBarRect.Y + 1, healthFillWidth, HealthBarHeight - 2);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, healthFillRect, healthColor);

                memberIndex++;
            }

            _spriteBatch.End();
        }

        protected override void DrawBordersAndTextScaled(Vector2 scaledPos, float scale)
        {
            _spriteBatch.Begin();

            // Select font based on scale
            BitmapFont nameFont, detailFont;
            if (scale >= 1.75f) { nameFont = _scaledNameFont; detailFont = _scaledDetailFont; }
            else if (scale >= 1.25f) { nameFont = _nameFont; detailFont = _detailFont; }
            else { nameFont = _nameFont; detailFont = _detailFont; }

            var panelHeight = (int)((HeaderHeight + (_cachedParty.Count * MemberRowHeight) + Padding) * scale);

            // Panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, (int)(PanelWidth * scale), panelHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Header text
            _spriteBatch.DrawString(nameFont, $"Party ({_cachedParty.Count})",
                new Vector2(scaledPos.X + Padding * scale, scaledPos.Y + 2 * scale), Color.White);

            // Leave button border and text
            var leaveRect = new Rectangle(
                (int)(scaledPos.X + (PanelWidth - RemoveButtonSize - 4) * scale),
                (int)(scaledPos.Y + 3 * scale),
                (int)(RemoveButtonSize * scale),
                (int)(RemoveButtonSize * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, leaveRect, Color.Black, 1);
            var xSize = detailFont.MeasureString("X");
            _spriteBatch.DrawString(detailFont, "X", new Vector2(leaveRect.X + (leaveRect.Width - xSize.Width) / 2, leaveRect.Y + (leaveRect.Height - xSize.Height) / 2 - 1), Color.White);

            // Draw each party member text
            var memberIndex = 0;
            var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);
            foreach (var member in _cachedParty.OrderByDescending(m => m.IsLeader))
            {
                var rowY = (int)(scaledPos.Y + (HeaderHeight + memberIndex * MemberRowHeight) * scale);

                // Name
                var nameColor = member.IsLeader ? new Color(255, 215, 0) : Color.White;
                var nameText = member.IsLeader ? $"★ {member.Name}" : member.Name;
                _spriteBatch.DrawString(nameFont, nameText, new Vector2(scaledPos.X + (Padding + 2) * scale, rowY + 2 * scale), nameColor);

                // Remove button border for other members when leader
                if (isLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
                {
                    var removeRect = new Rectangle(
                        (int)(scaledPos.X + (PanelWidth - RemoveButtonSize - 8) * scale),
                        (int)(rowY + 2 * scale),
                        (int)(RemoveButtonSize * scale),
                        (int)(RemoveButtonSize * scale));
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, removeRect, Color.Black, 1);
                    var txtSize = detailFont.MeasureString("x");
                    _spriteBatch.DrawString(detailFont, "x", new Vector2(removeRect.X + (removeRect.Width - txtSize.Width) / 2, removeRect.Y + (removeRect.Height - txtSize.Height) / 2 - 1), Color.White);
                }

                // Level
                var levelText = $"Lv. {member.Level}";
                _spriteBatch.DrawString(detailFont, levelText, new Vector2(scaledPos.X + (Padding + 2) * scale, rowY + 16 * scale), _styleProvider.TextSecondary);

                // Health percentage text
                var healthBarY = rowY + (MemberRowHeight - HealthBarHeight - 4) * scale;
                var healthText = $"{member.PercentHealth}%";
                var textSize = detailFont.MeasureString(healthText);
                var healthBarWidth = (PanelWidth - (Padding * 2) - 4) * scale;
                _spriteBatch.DrawString(detailFont, healthText, new Vector2(scaledPos.X + Padding * scale + (healthBarWidth - textSize.Width) / 2, healthBarY - 1 * scale), Color.White);

                memberIndex++;
            }

            _spriteBatch.End();
        }

        protected override void DrawComplete(Vector2 pos)
        {
            _spriteBatch.Begin();

            var panelHeight = HeaderHeight + (_cachedParty.Count * MemberRowHeight) + Padding;

            // Draw panel background with slight transparency
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, panelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, new Color(_styleProvider.PanelBackground, 0.9f));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw header with leave button
            var headerRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, HeaderHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, headerRect, new Color(60, 50, 40, 230));
            _spriteBatch.DrawString(_nameFont, $"Party ({_cachedParty.Count})",
                new Vector2(pos.X + Padding, pos.Y + 2), Color.White);

            // Leave button (X in top right corner)
            _leaveButtonRect = new Rectangle((int)pos.X + PanelWidth - RemoveButtonSize - 4, (int)pos.Y + 3, RemoveButtonSize, RemoveButtonSize);
            DrawRemoveButton(_leaveButtonRect, "X", new Color(180, 60, 60), _detailFont);

            // Check if current character is leader
            var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);

            // Draw each party member
            var memberIndex = 0;
            foreach (var member in _cachedParty.OrderByDescending(m => m.IsLeader))
            {
                DrawPartyMemberComplete(pos, member, memberIndex, isLeader, _nameFont, _detailFont);
                memberIndex++;
            }

            _spriteBatch.End();
        }


        private void DrawPartyMember(Vector2 panelPos, PartyMember member, int index, bool isLeader)
        {
            var rowY = (int)panelPos.Y + HeaderHeight + (index * MemberRowHeight);
            var rowRect = new Rectangle((int)panelPos.X + 2, rowY, PanelWidth - 4, MemberRowHeight - 2);

            // Alternating row background
            var rowBgColor = index % 2 == 0 ? new Color(80, 70, 60, 180) : new Color(70, 60, 50, 180);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowBgColor);

            // Leader indicator
            var nameColor = member.IsLeader ? new Color(255, 215, 0) : Color.White; // Gold for leader

            // Name (with star for leader)
            var nameText = member.IsLeader ? $"★ {member.Name}" : member.Name;
            _spriteBatch.DrawString(_nameFont, nameText,
                new Vector2(panelPos.X + Padding + 2, rowY + 2), nameColor);

            // Remove button (X) for leader to kick other members
            if (isLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
            {
                var removeRect = new Rectangle((int)panelPos.X + PanelWidth - RemoveButtonSize - 8, rowY + 2, RemoveButtonSize, RemoveButtonSize);
                DrawRemoveButton(removeRect, "x", new Color(140, 50, 50), _detailFont);
                _removeButtonRects[member.CharacterID] = removeRect;
            }

            // Level
            var levelText = $"Lv. {member.Level}";
            _spriteBatch.DrawString(_detailFont, levelText,
                new Vector2(panelPos.X + Padding + 2, rowY + 16), _styleProvider.TextSecondary);

            // Health bar background
            var healthBarY = rowY + MemberRowHeight - HealthBarHeight - 4;
            var healthBarRect = new Rectangle((int)panelPos.X + Padding, healthBarY, PanelWidth - (Padding * 2) - 4, HealthBarHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, healthBarRect, new Color(40, 40, 40, 200));

            // Health bar fill
            var healthPercent = member.PercentHealth / 100f;
            var healthFillWidth = (int)((healthBarRect.Width - 2) * healthPercent);
            var healthColor = member.PercentHealth > 50 ? new Color(80, 180, 80) :
                              member.PercentHealth > 25 ? new Color(220, 180, 50) :
                              new Color(200, 60, 60);
            var healthFillRect = new Rectangle(healthBarRect.X + 1, healthBarRect.Y + 1, healthFillWidth, HealthBarHeight - 2);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, healthFillRect, healthColor);

            // Health percentage text
            var healthText = $"{member.PercentHealth}%";
            var textSize = _detailFont.MeasureString(healthText);
            _spriteBatch.DrawString(_detailFont, healthText,
                new Vector2(healthBarRect.X + (healthBarRect.Width - textSize.Width) / 2, healthBarY - 1),
                Color.White);
        }

        private void DrawPartyMemberComplete(Vector2 panelPos, PartyMember member, int index, bool isLeader, BitmapFont nameFont, BitmapFont detailFont)
        {
            var rowY = (int)panelPos.Y + HeaderHeight + (index * MemberRowHeight);
            var rowRect = new Rectangle((int)panelPos.X + 2, rowY, PanelWidth - 4, MemberRowHeight - 2);

            // Alternating row background
            var rowBgColor = index % 2 == 0 ? new Color(80, 70, 60, 180) : new Color(70, 60, 50, 180);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, rowBgColor);

            // Name (with star for leader)
            var nameColor = member.IsLeader ? new Color(255, 215, 0) : Color.White;
            var nameText = member.IsLeader ? $"★ {member.Name}" : member.Name;
            _spriteBatch.DrawString(nameFont, nameText, new Vector2(panelPos.X + Padding + 2, rowY + 2), nameColor);

            // Remove button for leader to kick other members
            if (isLeader && member.CharacterID != _characterProvider.MainCharacter.ID)
            {
                var removeRect = new Rectangle((int)panelPos.X + PanelWidth - RemoveButtonSize - 8, rowY + 2, RemoveButtonSize, RemoveButtonSize);
                DrawRemoveButton(removeRect, "x", new Color(140, 50, 50), detailFont);
                _removeButtonRects[member.CharacterID] = removeRect;
            }

            // Level
            var levelText = $"Lv. {member.Level}";
            _spriteBatch.DrawString(detailFont, levelText, new Vector2(panelPos.X + Padding + 2, rowY + 16), _styleProvider.TextSecondary);

            // Health bar background
            var healthBarY = rowY + MemberRowHeight - HealthBarHeight - 4;
            var healthBarRect = new Rectangle((int)panelPos.X + Padding, healthBarY, PanelWidth - (Padding * 2) - 4, HealthBarHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, healthBarRect, new Color(40, 40, 40, 200));

            // Health bar fill
            var healthPercent = member.PercentHealth / 100f;
            var healthFillWidth = (int)((healthBarRect.Width - 2) * healthPercent);
            var healthColor = member.PercentHealth > 50 ? new Color(80, 180, 80) :
                              member.PercentHealth > 25 ? new Color(220, 180, 50) :
                              new Color(200, 60, 60);
            var healthFillRect = new Rectangle(healthBarRect.X + 1, healthBarRect.Y + 1, healthFillWidth, HealthBarHeight - 2);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, healthFillRect, healthColor);

            // Health percentage text
            var healthText = $"{member.PercentHealth}%";
            var textSize = detailFont.MeasureString(healthText);
            _spriteBatch.DrawString(detailFont, healthText,
                new Vector2(healthBarRect.X + (healthBarRect.Width - textSize.Width) / 2, healthBarY - 1),
                Color.White);
        }

        private void DrawRemoveButton(Rectangle rect, string label, Color bgColor, BitmapFont font)
        {
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rect, bgColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rect, Color.Black, 1);

            var textSize = font.MeasureString(label);
            var textPos = new Vector2(
                rect.X + (rect.Width - textSize.Width) / 2,
                rect.Y + (rect.Height - textSize.Height) / 2 - 1);
            _spriteBatch.DrawString(font, label, textPos, Color.White);
        }
    }
}

