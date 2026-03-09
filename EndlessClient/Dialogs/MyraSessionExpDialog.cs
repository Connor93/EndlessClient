using System;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraSessionExpDialog : MyraDialogAdapter
    {
        public MyraSessionExpDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            ICharacterProvider characterProvider,
            IExperienceTableProvider expTableProvider,
            ICharacterSessionProvider characterSessionProvider)
            : base(uiManager, localizedStringFinder.GetString(EOResourceID.DIALOG_TITLE_PERFORMANCE))
        {
            Window.Width = 300;
            Window.Height = 320;
            Window.TitleFont = fontProvider.Header;

            var grid = new Grid
            {
                ColumnSpacing = 12,
                RowSpacing = 4,
            };

            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

            for (int i = 0; i < 8; i++)
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            var c = characterProvider.MainCharacter;
            var level = c.Stats[CharacterStat.Level];
            var exp = c.Stats[CharacterStat.Experience];
            var usage = c.Stats[CharacterStat.Usage];
            var todayExp = characterSessionProvider.TodayTotalExp;
            int sessionTimeMinutes = (int)(DateTime.Now - characterSessionProvider.SessionStartTime).TotalMinutes;

            var labels = new[]
            {
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_TOTALEXP),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_NEXT_LEVEL),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_EXP_NEEDED),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_TODAY_EXP),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_TOTAL_AVG),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_TODAY_AVG),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_BEST_KILL),
                localizedStringFinder.GetString(EOResourceID.DIALOG_PERFORMANCE_LAST_KILL),
            };

            var values = new[]
            {
                $"{exp}",
                $"{expTableProvider.ExperienceByLevel[level + 1]}",
                $"{expTableProvider.ExperienceByLevel[level + 1] - exp}",
                $"{todayExp}",
                $"{(int)(exp / (usage / 60.0))}",
                $"{(sessionTimeMinutes > 0 ? (int)(todayExp / (sessionTimeMinutes / 60.0)) : 0)}",
                $"{characterSessionProvider.BestKillExp}",
                $"{characterSessionProvider.LastKillExp}",
            };

            for (int i = 0; i < 8; i++)
            {
                var left = new Label
                {
                    Text = labels[i],
                    Font = fontProvider.Normal,
                };
                Grid.SetColumn(left, 0);
                Grid.SetRow(left, i);
                grid.Widgets.Add(left);

                var right = new Label
                {
                    Text = values[i],
                    Font = fontProvider.Normal,
                };
                Grid.SetColumn(right, 1);
                Grid.SetRow(right, i);
                grid.Widgets.Add(right);
            }

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 8,
            };

            mainPanel.Widgets.Add(grid);

            var okButton = new Button
            {
                Content = new Label { Text = "OK", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            okButton.Click += (_, _) => Close(XNADialogResult.OK);
            mainPanel.Widgets.Add(okButton);

            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;
        }
    }
}
