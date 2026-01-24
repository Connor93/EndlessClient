using System;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Extensions;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnStatsPanel : DraggableHudPanel, IPostScaleDrawable
    {
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITrainingController _trainingController;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;
        private readonly BitmapFont _scaledFont;
        private readonly BitmapFont _scaledLabelFont;

        private const int STR = 0, INT = 1, WIS = 2, AGI = 3, CON = 4, CHA = 5;
        private readonly CodeDrawnButton[] _arrowButtons;

        private CharacterStats _lastCharacterStats;
        private InventoryItem _lastCharacterGold;
        private bool _confirmedTraining;

        private const int PanelWidth = 476;
        private const int PanelHeight = 125;
        private const int RowHeight = 17;
        private const int HeaderHeight = 22;

        public CodeDrawnStatsPanel(ICharacterProvider characterProvider,
                                   ICharacterInventoryProvider characterInventoryProvider,
                                   IExperienceTableProvider experienceTableProvider,
                                   IEOMessageBoxFactory messageBoxFactory,
                                   ITrainingController trainingController,
                                   IUIStyleProvider styleProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider,
                                   IContentProvider contentProvider,
                                   IClientWindowSizeProvider clientWindowSizeProvider)
            : base(clientWindowSizeProvider.Resizable)
        {
            _characterProvider = characterProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _experienceTableProvider = experienceTableProvider;
            _messageBoxFactory = messageBoxFactory;
            _trainingController = trainingController;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];
            _scaledFont = contentProvider.Fonts[Constants.FontSize10];
            _scaledLabelFont = contentProvider.Fonts[Constants.FontSize10];

            DrawArea = new Rectangle(102, 330, PanelWidth, PanelHeight);

            _arrowButtons = new CodeDrawnButton[6];
            for (int i = 0; i < 6; i++)
            {
                _arrowButtons[i] = new CodeDrawnButton(styleProvider, _labelFont)
                {
                    Text = "+",
                    DrawArea = new Rectangle(75, HeaderHeight + 2 + i * RowHeight, 18, 14),
                    Visible = false
                };
            }
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            foreach (var btn in _arrowButtons)
            {
                btn.OnClick += HandleArrowButtonClick;
                btn.SetParentControl(this);
                btn.Initialize();
            }

            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (_lastCharacterStats != _characterProvider.MainCharacter.Stats ||
                _lastCharacterGold != CurrentCharacterGold)
            {
                _lastCharacterStats = _characterProvider.MainCharacter.Stats;
                _lastCharacterGold = CurrentCharacterGold;

                if (_lastCharacterStats.Stats[CharacterStat.StatPoints] > 0)
                {
                    foreach (var button in _arrowButtons)
                        button.Visible = true;
                }
                else
                {
                    foreach (var button in _arrowButtons)
                        button.Visible = false;
                    _confirmedTraining = false;
                }
            }

            base.OnUpdateControl(gameTime);
        }

        // IPostScaleDrawable implementation
        public int PostScaleDrawOrder => 0;
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider.IsScaledMode;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                // In scaled mode: draw only fills to render target
                DrawPanelFills(DrawPositionWithParentOffset);
                base.OnDrawControl(gameTime);
                return;
            }

            // Normal mode: draw everything
            DrawPanelComplete(DrawPositionWithParentOffset, _font, _labelFont);
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

        private void DrawPanelFills(Vector2 pos)
        {
            _spriteBatch.Begin();

            // Panel background fill
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);

            _spriteBatch.End();
        }

        private void DrawPanelBordersAndText(Vector2 scaledPos, float scale)
        {
            _spriteBatch.Begin();

            // Select font based on scale
            BitmapFont font, labelFont;
            if (scale >= 1.75f) { font = _scaledFont; labelFont = _scaledLabelFont; }
            else if (scale >= 1.25f) { font = _labelFont; labelFont = _labelFont; }
            else { font = _font; labelFont = _labelFont; }

            var panelWidth = (int)(PanelWidth * scale);
            var panelHeight = (int)(PanelHeight * scale);

            // Panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, Math.Max(1, (int)(2 * scale)));

            // Grid lines
            DrawGridScaled(scaledPos, scale);

            // Stats
            DrawStatsScaled(scaledPos, scale, font, labelFont);

            _spriteBatch.End();
        }

        private void DrawPanelComplete(Vector2 pos, BitmapFont font, BitmapFont labelFont)
        {
            _spriteBatch.Begin();

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw grid lines
            DrawGrid(pos);

            // Draw stat values
            DrawStats(pos);

            _spriteBatch.End();
        }


        private void DrawGrid(Vector2 pos)
        {
            var lineColor = new Color((byte)_styleProvider.PanelBorder.R, (byte)_styleProvider.PanelBorder.G, (byte)_styleProvider.PanelBorder.B, (byte)100);

            // Column widths: Basic(100), Combat(120), Info(150), Other(106)
            int[] colWidths = { 100, 120, 150, 106 };
            int x = (int)pos.X;
            for (int i = 0; i < colWidths.Length - 1; i++)
            {
                x += colWidths[i];
                DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(x, (int)pos.Y + 2, 1, PanelHeight - 4), lineColor);
            }

            // Horizontal row lines
            for (int i = 1; i <= 6; i++)
            {
                var y = (int)pos.Y + HeaderHeight + i * RowHeight;
                if (y < pos.Y + PanelHeight - 2)
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X + 2, y, PanelWidth - 4, 1), lineColor);
            }
        }

        private void DrawStats(Vector2 pos)
        {
            var stats = _lastCharacterStats;
            if (stats == null) return;

            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            // Column 1: Basic Stats (Str, Int, Wis, Agi, Con, Dex)
            string[] basicLabels = { "Str", "Int", "Wis", "Agi", "Con", "Dex" };
            CharacterStat[] basicStats = { CharacterStat.Strength, CharacterStat.Intelligence, CharacterStat.Wisdom,
                                           CharacterStat.Agility, CharacterStat.Constitution, CharacterStat.Charisma };
            for (int i = 0; i < 6; i++)
            {
                var y = pos.Y + HeaderHeight + 3 + i * RowHeight;
                _spriteBatch.DrawString(_labelFont, basicLabels[i], new Vector2(pos.X + 8, y), labelColor);
                _spriteBatch.DrawString(_font, $"{stats[basicStats[i]]}", new Vector2(pos.X + 45, y), valueColor);
            }

            // Column 2: Combat Stats (HP, TP, Atk, Acc, Def, Eva)
            string[] combatLabels = { "HP", "TP", "Atk", "Acc", "Def", "Eva" };
            int col2X = (int)pos.X + 100;
            _spriteBatch.DrawString(_labelFont, combatLabels[0], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.HP]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3), valueColor);

            _spriteBatch.DrawString(_labelFont, combatLabels[1], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3 + RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.TP]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3 + RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, combatLabels[2], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3 + 2 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.MinDam]} - {stats[CharacterStat.MaxDam]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3 + 2 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, combatLabels[3], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3 + 3 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Accuracy]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3 + 3 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, combatLabels[4], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3 + 4 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Armor]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3 + 4 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, combatLabels[5], new Vector2(col2X + 8, pos.Y + HeaderHeight + 3 + 5 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Evade]}", new Vector2(col2X + 40, pos.Y + HeaderHeight + 3 + 5 * RowHeight), valueColor);

            // Column 3: Character Info (Name, Guild, Weight, St.Pts, Sk.Pts)
            int col3X = (int)pos.X + 220;
            _spriteBatch.DrawString(_labelFont, "Name", new Vector2(col3X + 8, pos.Y + HeaderHeight + 3), labelColor);
            _spriteBatch.DrawString(_font, $"{_characterProvider.MainCharacter.Name}", new Vector2(col3X + 55, pos.Y + HeaderHeight + 3), valueColor);

            _spriteBatch.DrawString(_labelFont, "Guild", new Vector2(col3X + 8, pos.Y + HeaderHeight + 3 + RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{_characterProvider.MainCharacter.GuildName}", new Vector2(col3X + 55, pos.Y + HeaderHeight + 3 + RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, "Weight", new Vector2(col3X + 8, pos.Y + HeaderHeight + 3 + 2 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Weight]} / {stats[CharacterStat.MaxWeight]}", new Vector2(col3X + 55, pos.Y + HeaderHeight + 3 + 2 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, "St.Pts", new Vector2(col3X + 8, pos.Y + HeaderHeight + 3 + 3 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.StatPoints]}", new Vector2(col3X + 55, pos.Y + HeaderHeight + 3 + 3 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, "Sk.Pts", new Vector2(col3X + 8, pos.Y + HeaderHeight + 3 + 4 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.SkillPoints]}", new Vector2(col3X + 55, pos.Y + HeaderHeight + 3 + 4 * RowHeight), valueColor);

            // Column 4: Resources (Gold, Exp, TNL, LvL, Karma)
            int col4X = (int)pos.X + 370;
            _spriteBatch.DrawString(_labelFont, "LvL", new Vector2(col4X + 8, pos.Y + HeaderHeight + 3), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Level]}", new Vector2(col4X + 45, pos.Y + HeaderHeight + 3), valueColor);

            _spriteBatch.DrawString(_labelFont, "Gold", new Vector2(col4X + 8, pos.Y + HeaderHeight + 3 + 2 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{CurrentCharacterGold.Amount}", new Vector2(col4X + 45, pos.Y + HeaderHeight + 3 + 2 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, "Exp", new Vector2(col4X + 8, pos.Y + HeaderHeight + 3 + 3 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{stats[CharacterStat.Experience]}", new Vector2(col4X + 45, pos.Y + HeaderHeight + 3 + 3 * RowHeight), valueColor);

            _spriteBatch.DrawString(_labelFont, "TNL", new Vector2(col4X + 8, pos.Y + HeaderHeight + 3 + 4 * RowHeight), labelColor);
            _spriteBatch.DrawString(_font, $"{ExperienceToNextLevel}", new Vector2(col4X + 45, pos.Y + HeaderHeight + 3 + 4 * RowHeight), valueColor);

            _spriteBatch.DrawString(_font, $"{stats.GetKarmaString()}", new Vector2(col4X + 8, pos.Y + HeaderHeight + 3 + 5 * RowHeight), valueColor);
        }

        private void DrawGridScaled(Vector2 pos, float scale)
        {
            var lineColor = new Color((byte)_styleProvider.PanelBorder.R, (byte)_styleProvider.PanelBorder.G, (byte)_styleProvider.PanelBorder.B, (byte)100);

            // Column widths: Basic(100), Combat(120), Info(150), Other(106)
            int[] colWidths = { 100, 120, 150, 106 };
            float x = pos.X;
            for (int i = 0; i < colWidths.Length - 1; i++)
            {
                x += colWidths[i] * scale;
                DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)x, (int)(pos.Y + 2 * scale), Math.Max(1, (int)scale), (int)((PanelHeight - 4) * scale)), lineColor);
            }

            // Horizontal row lines
            for (int i = 1; i <= 6; i++)
            {
                var y = (int)(pos.Y + (HeaderHeight + i * RowHeight) * scale);
                if (y < pos.Y + PanelHeight * scale - 2)
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)(pos.X + 2 * scale), y, (int)((PanelWidth - 4) * scale), Math.Max(1, (int)scale)), lineColor);
            }
        }

        private void DrawStatsScaled(Vector2 pos, float scale, BitmapFont font, BitmapFont labelFont)
        {
            var stats = _lastCharacterStats;
            if (stats == null) return;

            var labelColor = _styleProvider.TextSecondary;
            var valueColor = _styleProvider.TextPrimary;

            // Column 1: Basic Stats
            string[] basicLabels = { "Str", "Int", "Wis", "Agi", "Con", "Dex" };
            CharacterStat[] basicStats = { CharacterStat.Strength, CharacterStat.Intelligence, CharacterStat.Wisdom,
                                           CharacterStat.Agility, CharacterStat.Constitution, CharacterStat.Charisma };
            for (int i = 0; i < 6; i++)
            {
                var y = pos.Y + (HeaderHeight + 3 + i * RowHeight) * scale;
                _spriteBatch.DrawString(labelFont, basicLabels[i], new Vector2(pos.X + 8 * scale, y), labelColor);
                _spriteBatch.DrawString(font, $"{stats[basicStats[i]]}", new Vector2(pos.X + 45 * scale, y), valueColor);
            }

            // Column 2: Combat Stats
            string[] combatLabels = { "HP", "TP", "Atk", "Acc", "Def", "Eva" };
            float col2X = pos.X + 100 * scale;
            _spriteBatch.DrawString(labelFont, combatLabels[0], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.HP]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, combatLabels[1], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3 + RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.TP]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3 + RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, combatLabels[2], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.MinDam]} - {stats[CharacterStat.MaxDam]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, combatLabels[3], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Accuracy]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, combatLabels[4], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Armor]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, combatLabels[5], new Vector2(col2X + 8 * scale, pos.Y + (HeaderHeight + 3 + 5 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Evade]}", new Vector2(col2X + 40 * scale, pos.Y + (HeaderHeight + 3 + 5 * RowHeight) * scale), valueColor);

            // Column 3: Character Info
            float col3X = pos.X + 220 * scale;
            _spriteBatch.DrawString(labelFont, "Name", new Vector2(col3X + 8 * scale, pos.Y + (HeaderHeight + 3) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{_characterProvider.MainCharacter.Name}", new Vector2(col3X + 55 * scale, pos.Y + (HeaderHeight + 3) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "Guild", new Vector2(col3X + 8 * scale, pos.Y + (HeaderHeight + 3 + RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{_characterProvider.MainCharacter.GuildName}", new Vector2(col3X + 55 * scale, pos.Y + (HeaderHeight + 3 + RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "Weight", new Vector2(col3X + 8 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Weight]} / {stats[CharacterStat.MaxWeight]}", new Vector2(col3X + 55 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "St.Pts", new Vector2(col3X + 8 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.StatPoints]}", new Vector2(col3X + 55 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "Sk.Pts", new Vector2(col3X + 8 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.SkillPoints]}", new Vector2(col3X + 55 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), valueColor);

            // Column 4: Resources
            float col4X = pos.X + 370 * scale;
            _spriteBatch.DrawString(labelFont, "LvL", new Vector2(col4X + 8 * scale, pos.Y + (HeaderHeight + 3) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Level]}", new Vector2(col4X + 45 * scale, pos.Y + (HeaderHeight + 3) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "Gold", new Vector2(col4X + 8 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{CurrentCharacterGold.Amount}", new Vector2(col4X + 45 * scale, pos.Y + (HeaderHeight + 3 + 2 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "Exp", new Vector2(col4X + 8 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{stats[CharacterStat.Experience]}", new Vector2(col4X + 45 * scale, pos.Y + (HeaderHeight + 3 + 3 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(labelFont, "TNL", new Vector2(col4X + 8 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), labelColor);
            _spriteBatch.DrawString(font, $"{ExperienceToNextLevel}", new Vector2(col4X + 45 * scale, pos.Y + (HeaderHeight + 3 + 4 * RowHeight) * scale), valueColor);

            _spriteBatch.DrawString(font, $"{stats.GetKarmaString()}", new Vector2(col4X + 8 * scale, pos.Y + (HeaderHeight + 3 + 5 * RowHeight) * scale), valueColor);
        }

        private void HandleArrowButtonClick(object sender, EventArgs e)
        {
            if (!_confirmedTraining)
            {
                var dialog = _messageBoxFactory.CreateMessageBox("Do you want to train?",
                    "Character training",
                    EODialogButtons.OkCancel);

                dialog.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        _confirmedTraining = true;
                };

                dialog.ShowDialog();
            }
            else
            {
                var index = _arrowButtons.Select((btn, ndx) => new { btn, ndx })
                                         .Single(x => x.btn == sender).ndx;
                var characterStat = CharacterStat.Strength + index;
                _trainingController.AddStatPoint(characterStat);
            }
        }

        private InventoryItem CurrentCharacterGold
            => _characterInventoryProvider.ItemInventory.Single(x => x.ItemID == 1);

        private int ExperienceToNextLevel =>
            _experienceTableProvider.ExperienceByLevel[
                _characterProvider.MainCharacter.Stats[CharacterStat.Level] + 1
                ] - _lastCharacterStats[CharacterStat.Experience];
    }
}
