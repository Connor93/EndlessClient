using System;
using EndlessClient.Controllers;
using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraBardDialog : MyraDialogAdapter
    {
        private readonly IBardController _bardController;
        private ulong _currentTick;

        private const int Columns = 12;
        private const int Rows = 3;

        public MyraBardDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IBardController bardController)
            : base(uiManager, "Bard")
        {
            _bardController = bardController;

            Window.Width = 280;
            Window.Height = 160;
            Window.TitleFont = fontProvider.Header;

            var mainPanel = new VerticalStackPanel { Spacing = 8 };

            var grid = new Grid
            {
                ColumnSpacing = 2,
                RowSpacing = 2,
            };

            for (int c = 0; c < Columns; c++)
                grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            for (int r = 0; r < Rows; r++)
                grid.RowsProportions.Add(new Proportion(ProportionType.Fill));

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var noteIndex = c + (Columns * r);
                    var noteButton = new Button
                    {
                        Content = new Label { Text = "♪", Font = fontProvider.Normal, HorizontalAlignment = HorizontalAlignment.Center },
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                    };
                    noteButton.Click += (_, _) =>
                    {
                        if (_currentTick > 8)
                        {
                            _bardController.PlayInstrumentNote(noteIndex);
                            _currentTick = 0;
                        }
                    };
                    Grid.SetColumn(noteButton, c);
                    Grid.SetRow(noteButton, r);
                    grid.Widgets.Add(noteButton);
                }
            }

            mainPanel.Widgets.Add(grid);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            var cancelButton = new Button
            {
                Content = new Label { Text = "Cancel", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            cancelButton.Click += (_, _) => Close(XNADialogResult.Cancel);
            mainPanel.Widgets.Add(cancelButton);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;
        }

        public override void Update(GameTime gameTime)
        {
            _currentTick++;
            base.Update(gameTime);
        }
    }
}
