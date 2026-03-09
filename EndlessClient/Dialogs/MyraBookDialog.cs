using System.Collections.Generic;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Extensions;
using EOLib.IO.Repositories;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Optional;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraBookDialog : MyraDialogAdapter
    {
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IPaperdollProvider _paperdollProvider;
        private readonly IMyraFontProvider _fontProvider;

        private readonly Label _name, _home, _classLabel, _partner, _title, _guild, _rank;
        private readonly VerticalStackPanel _questListPanel;

        public Character Character { get; }

        private Option<PaperdollData> _paperdollData;

        public MyraBookDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IPubFileProvider pubFileProvider,
            IPaperdollProvider paperdollProvider,
            Character character,
            bool isMainCharacter)
            : base(uiManager, Capitalize(character.Name))
        {
            _pubFileProvider = pubFileProvider;
            _paperdollProvider = paperdollProvider;
            _fontProvider = fontProvider;
            Character = character;

            Window.Width = 340;
            Window.Height = 360;
            Window.TitleFont = fontProvider.Header;

            _paperdollData = Option.None<PaperdollData>();

            var mainPanel = new VerticalStackPanel { Spacing = 4, Padding = new Thickness(4) };

            // Character info grid
            var infoGrid = new Grid();
            infoGrid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            infoGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            infoGrid.ColumnSpacing = 8;

            var labels = new[] { "Name", "Home", "Class", "Partner", "Title", "Guild", "Rank" };
            _name = new Label { Font = fontProvider.Normal };
            _home = new Label { Font = fontProvider.Normal };
            _classLabel = new Label { Font = fontProvider.Normal };
            _partner = new Label { Font = fontProvider.Normal };
            _title = new Label { Font = fontProvider.Normal };
            _guild = new Label { Font = fontProvider.Normal };
            _rank = new Label { Font = fontProvider.Normal };

            var valueLabels = new[] { _name, _home, _classLabel, _partner, _title, _guild, _rank };
            for (int i = 0; i < labels.Length; i++)
            {
                infoGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
                var headerLabel = new Label { Text = labels[i] + ":", Font = fontProvider.Normal, TextColor = new Color(160, 160, 180) };
                Grid.SetColumn(headerLabel, 0);
                Grid.SetRow(headerLabel, i);
                infoGrid.Widgets.Add(headerLabel);

                Grid.SetColumn(valueLabels[i], 1);
                Grid.SetRow(valueLabels[i], i);
                infoGrid.Widgets.Add(valueLabels[i]);
            }

            mainPanel.Widgets.Add(infoGrid);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            // Separator
            mainPanel.Widgets.Add(new HorizontalSeparator());
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            // Quest list
            _questListPanel = new VerticalStackPanel { Spacing = 2 };
            var scrollViewer = new ScrollViewer
            {
                Content = _questListPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            mainPanel.Widgets.Add(scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // OK button
            var okButton = new Button
            {
                Content = new Label { Text = "OK", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            okButton.Click += (_, _) => Close(XNADialogResult.OK);
            mainPanel.Widgets.Add(okButton);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;
        }

        public override void Update(GameTime gameTime)
        {
            _paperdollData = _paperdollData.FlatMap(paperdollData =>
                paperdollData.NoneWhen(d => _paperdollProvider.VisibleCharacterPaperdolls.ContainsKey(Character.ID) &&
                                           !_paperdollProvider.VisibleCharacterPaperdolls[Character.ID].Equals(d)));

            _paperdollData.MatchNone(() =>
            {
                if (_paperdollProvider.VisibleCharacterPaperdolls.ContainsKey(Character.ID))
                {
                    var paperdollData = _paperdollProvider.VisibleCharacterPaperdolls[Character.ID];
                    _paperdollData = Option.Some(paperdollData);
                    UpdateDisplayedData(paperdollData);
                }
            });

            base.Update(gameTime);
        }

        private void UpdateDisplayedData(PaperdollData paperdollData)
        {
            _name.Text = Capitalize(paperdollData.Name);
            _home.Text = Capitalize(paperdollData.Home);

            paperdollData.Class.SomeWhen(x => x != 0)
                .MatchSome(classId => _classLabel.Text = Capitalize(_pubFileProvider.ECFFile[classId].Name));

            _partner.Text = Capitalize(paperdollData.Partner);
            _title.Text = Capitalize(paperdollData.Title);
            _guild.Text = Capitalize(paperdollData.Guild);
            _rank.Text = Capitalize(paperdollData.Rank);

            _questListPanel.Widgets.Clear();
            foreach (var questName in paperdollData.QuestNames)
            {
                var questLabel = new Label
                {
                    Text = "• " + questName,
                    Font = _fontProvider.Normal,
                    Wrap = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                _questListPanel.Widgets.Add(questLabel);
            }
        }

        private static string Capitalize(string input) =>
            string.IsNullOrEmpty(input) ? string.Empty : char.ToUpper(input[0]) + input[1..].ToLower();
    }
}
