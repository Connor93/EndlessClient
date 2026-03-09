using System.Linq;
using EndlessClient.Controllers;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Extensions;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Stats panel. Redesigned layout showing character stats in columns
    /// with stat training buttons when stat points are available.
    /// </summary>
    public class MyraStatsPanel : MyraHudPanelBase
    {
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITrainingController _trainingController;
        private readonly IMyraFontProvider _fontProvider;

        // Labels that get updated
        private Label _strVal, _intVal, _wisVal, _agiVal, _conVal, _chaVal;
        private Label _hpVal, _tpVal, _atkVal, _accVal, _defVal, _evaVal;
        private Label _nameVal, _guildVal, _weightVal, _statPtsVal, _skillPtsVal;
        private Label _levelVal, _goldVal, _expVal, _tnlVal, _karmaVal;
        private readonly TextButton[] _trainButtons = new TextButton[6];

        private CharacterStats _lastStats;
        private InventoryItem _lastGold;
        private bool _confirmedTraining;

        public MyraStatsPanel(Game game,
                              IMyraUIManager uiManager,
                              IMyraFontProvider fontProvider,
                              ICharacterProvider characterProvider,
                              ICharacterInventoryProvider characterInventoryProvider,
                              IExperienceTableProvider experienceTableProvider,
                              IEOMessageBoxFactory messageBoxFactory,
                              ITrainingController trainingController)
            : base(game, uiManager, "Character Stats")
        {
            _fontProvider = fontProvider;
            _characterProvider = characterProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _experienceTableProvider = experienceTableProvider;
            _messageBoxFactory = messageBoxFactory;
            _trainingController = trainingController;
        }

        public override void Initialize()
        {
            Window.Width = 500;
            Window.Height = 165;
            Window.TitleFont = _fontProvider.Large;

            var grid = new Grid
            {
                ColumnSpacing = 4,
                RowSpacing = 1,
                Padding = new Thickness(4, 2),
            };

            // 4 columns: Basic | Combat | Info | Resources
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 110));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 120));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 135));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

            // Header row + 6 data rows
            for (int i = 0; i < 7; i++)
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            var headerColor = new Color(0xD4, 0xA5, 0x37);
            var labelColor = new Color(0x99, 0x99, 0xAA);

            // Column headers
            AddCell(grid, "Basic", 0, 0, _fontProvider.Normal, headerColor);
            AddCell(grid, "Combat", 1, 0, _fontProvider.Normal, headerColor);
            AddCell(grid, "Info", 2, 0, _fontProvider.Normal, headerColor);
            AddCell(grid, "Resources", 3, 0, _fontProvider.Normal, headerColor);

            // Column 1: Basic stats with train buttons
            string[] basicLabels = { "Str", "Int", "Wis", "Agi", "Con", "Cha" };
            Label[] basicValues = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                var row = new HorizontalStackPanel { Spacing = 4 };
                row.Widgets.Add(MakeLabel(basicLabels[i], _fontProvider.Small, labelColor, 30));
                basicValues[i] = MakeLabel("0", _fontProvider.Small, null, 30);
                row.Widgets.Add(basicValues[i]);

                var trainBtn = new TextButton { Text = "+", Width = 20, Height = 16, Visible = false };
                var statIndex = i;
                trainBtn.Click += (_, _) => HandleTrain((CharacterStat)(CharacterStat.Strength + statIndex));
                _trainButtons[i] = trainBtn;
                row.Widgets.Add(trainBtn);

                Grid.SetColumn(row, 0);
                Grid.SetRow(row, i + 1);
                grid.Widgets.Add(row);
            }
            _strVal = basicValues[0]; _intVal = basicValues[1]; _wisVal = basicValues[2];
            _agiVal = basicValues[3]; _conVal = basicValues[4]; _chaVal = basicValues[5];

            // Column 2: Combat stats
            string[] combatLabels = { "HP", "TP", "Atk", "Acc", "Def", "Eva" };
            Label[] combatValues = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                var row = new HorizontalStackPanel { Spacing = 4 };
                row.Widgets.Add(MakeLabel(combatLabels[i], _fontProvider.Small, labelColor, 30));
                combatValues[i] = MakeLabel("0", _fontProvider.Small, null, 70);
                row.Widgets.Add(combatValues[i]);

                Grid.SetColumn(row, 1);
                Grid.SetRow(row, i + 1);
                grid.Widgets.Add(row);
            }
            _hpVal = combatValues[0]; _tpVal = combatValues[1]; _atkVal = combatValues[2];
            _accVal = combatValues[3]; _defVal = combatValues[4]; _evaVal = combatValues[5];

            // Column 3: Character info
            _nameVal = AddValueRow(grid, "Name", 2, 1, labelColor);
            _guildVal = AddValueRow(grid, "Guild", 2, 2, labelColor);
            _weightVal = AddValueRow(grid, "Weight", 2, 3, labelColor);
            _statPtsVal = AddValueRow(grid, "St.Pts", 2, 4, labelColor);
            _skillPtsVal = AddValueRow(grid, "Sk.Pts", 2, 5, labelColor);

            // Column 4: Resources
            _levelVal = AddValueRow(grid, "Level", 3, 1, labelColor);
            _goldVal = AddValueRow(grid, "Gold", 3, 2, labelColor);
            _expVal = AddValueRow(grid, "Exp", 3, 3, labelColor);
            _tnlVal = AddValueRow(grid, "TNL", 3, 4, labelColor);
            _karmaVal = AddValueRow(grid, "Karma", 3, 5, labelColor);

            Window.Content = grid;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Window.Visible) { base.Update(gameTime); return; }

            var currentGold = _characterInventoryProvider.ItemInventory.Single(x => x.ItemID == 1);
            if (_lastStats != _characterProvider.MainCharacter.Stats || _lastGold != currentGold)
            {
                _lastStats = _characterProvider.MainCharacter.Stats;
                _lastGold = currentGold;
                RefreshStats();
            }

            base.Update(gameTime);
        }

        private void RefreshStats()
        {
            var s = _lastStats;
            _strVal.Text = $"{s[CharacterStat.Strength]}";
            _intVal.Text = $"{s[CharacterStat.Intelligence]}";
            _wisVal.Text = $"{s[CharacterStat.Wisdom]}";
            _agiVal.Text = $"{s[CharacterStat.Agility]}";
            _conVal.Text = $"{s[CharacterStat.Constitution]}";
            _chaVal.Text = $"{s[CharacterStat.Charisma]}";

            _hpVal.Text = $"{s[CharacterStat.HP]}";
            _tpVal.Text = $"{s[CharacterStat.TP]}";
            _atkVal.Text = $"{s[CharacterStat.MinDam]} - {s[CharacterStat.MaxDam]}";
            _accVal.Text = $"{s[CharacterStat.Accuracy]}";
            _defVal.Text = $"{s[CharacterStat.Armor]}";
            _evaVal.Text = $"{s[CharacterStat.Evade]}";

            _nameVal.Text = _characterProvider.MainCharacter.Name;
            _guildVal.Text = _characterProvider.MainCharacter.GuildName;
            _weightVal.Text = $"{s[CharacterStat.Weight]} / {s[CharacterStat.MaxWeight]}";
            _statPtsVal.Text = $"{s[CharacterStat.StatPoints]}";
            _skillPtsVal.Text = $"{s[CharacterStat.SkillPoints]}";

            _levelVal.Text = $"{s[CharacterStat.Level]}";
            _goldVal.Text = $"{_lastGold.Amount}";
            _expVal.Text = $"{s[CharacterStat.Experience]}";

            var tnl = _experienceTableProvider.ExperienceByLevel[s[CharacterStat.Level] + 1] - s[CharacterStat.Experience];
            _tnlVal.Text = $"{tnl}";
            _karmaVal.Text = s.GetKarmaString();

            var hasStatPoints = s.Stats[CharacterStat.StatPoints] > 0;
            foreach (var btn in _trainButtons)
                btn.Visible = hasStatPoints;

            if (!hasStatPoints)
                _confirmedTraining = false;
        }

        private void HandleTrain(CharacterStat stat)
        {
            if (!_confirmedTraining)
            {
                var dialog = _messageBoxFactory.CreateMessageBox("Do you want to train?",
                    "Character training", EODialogButtons.OkCancel);
                dialog.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        _confirmedTraining = true;
                };
                dialog.ShowDialog();
            }
            else
            {
                _trainingController.AddStatPoint(stat);
            }
        }

        // Helper: add a cell label
        private static void AddCell(Grid grid, string text, int col, int row, SpriteFontBase font, Color? color = null)
        {
            var label = new Label { Text = text, Font = font, Padding = new Thickness(2, 0) };
            if (color.HasValue) label.TextColor = color.Value;
            Grid.SetColumn(label, col);
            Grid.SetRow(label, row);
            grid.Widgets.Add(label);
        }

        // Helper: add a label+value row in a column
        private Label AddValueRow(Grid grid, string labelText, int col, int row, Color labelColor)
        {
            var panel = new HorizontalStackPanel { Spacing = 4 };
            panel.Widgets.Add(MakeLabel(labelText, _fontProvider.Small, labelColor, 42));
            var val = MakeLabel("", _fontProvider.Small, null, 60);
            panel.Widgets.Add(val);
            Grid.SetColumn(panel, col);
            Grid.SetRow(panel, row);
            grid.Widgets.Add(panel);
            return val;
        }

        private static Label MakeLabel(string text, SpriteFontBase font, Color? color, int? width = null)
        {
            var label = new Label { Text = text, Font = font };
            if (color.HasValue) label.TextColor = color.Value;
            if (width.HasValue) label.Width = width.Value;
            return label;
        }
    }
}
