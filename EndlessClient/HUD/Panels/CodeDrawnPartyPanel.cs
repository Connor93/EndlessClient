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
    public class CodeDrawnPartyPanel : DraggableHudPanel
    {
        private readonly IPartyActions _partyActions;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _nameFont;
        private readonly BitmapFont _detailFont;

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
                                   IContentProvider contentProvider)
            : base(true) // Enable dragging so player can move the panel
        {
            _partyActions = partyActions;
            _partyDataProvider = partyDataProvider;
            _characterProvider = characterProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _nameFont = contentProvider.Fonts[Constants.FontSize09];
            _detailFont = contentProvider.Fonts[Constants.FontSize08];

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

            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;
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
            DrawRemoveButton(_leaveButtonRect, "X", new Color(180, 60, 60));

            // Check if current character is leader
            var isLeader = _cachedParty.Any(m => m.IsLeader && m.CharacterID == _characterProvider.MainCharacter.ID);

            // Draw each party member
            var memberIndex = 0;
            foreach (var member in _cachedParty.OrderByDescending(m => m.IsLeader))
            {
                DrawPartyMember(pos, member, memberIndex, isLeader);
                memberIndex++;
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
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
                DrawRemoveButton(removeRect, "x", new Color(140, 50, 50));
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

        private void DrawRemoveButton(Rectangle rect, string label, Color bgColor)
        {
            DrawingPrimitives.DrawFilledRect(_spriteBatch, rect, bgColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, rect, Color.Black, 1);

            var textSize = _detailFont.MeasureString(label);
            var textPos = new Vector2(
                rect.X + (rect.Width - textSize.Width) / 2,
                rect.Y + (rect.Height - textSize.Height) / 2 - 1);
            _spriteBatch.DrawString(_detailFont, label, textPos, Color.White);
        }
    }
}

