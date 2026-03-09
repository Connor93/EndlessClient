using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Passive Spells panel. Currently a placeholder — passive spell
    /// functionality will be expanded in a future update.
    /// </summary>
    public class MyraPassiveSpellsPanel : MyraHudPanelBase
    {
        private readonly IMyraFontProvider _fontProvider;

        public MyraPassiveSpellsPanel(Game game,
                                      IMyraUIManager uiManager,
                                      IMyraFontProvider fontProvider)
            : base(game, uiManager, "Passive Spells")
        {
            _fontProvider = fontProvider;
        }

        public override void Initialize()
        {
            Window.Width = 484;
            Window.Height = 140;
            Window.TitleFont = _fontProvider.Large;

            var content = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Placeholder grid (8×2 empty slots)
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                ColumnSpacing = 2,
                RowSpacing = 2,
            };

            for (int col = 0; col < 8; col++)
                grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 45));
            for (int row = 0; row < 2; row++)
                grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 45));

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var slot = new Panel
                    {
                        Width = 43,
                        Height = 43,
                        Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0x2A, 0x2A, 0x3A)),
                        Border = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0x55, 0x55, 0x66)),
                        BorderThickness = new Thickness(1),
                    };
                    Grid.SetColumn(slot, col);
                    Grid.SetRow(slot, row);
                    grid.Widgets.Add(slot);
                }
            }

            content.Widgets.Add(grid);

            // TODO: Passive spell functionality will be expanded in a future update
            content.Widgets.Add(new Label
            {
                Text = "Passive spells will be available in a future update.",
                Font = _fontProvider.Normal,
                TextColor = new Color(0x88, 0x88, 0x99),
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            Window.Content = content;

            base.Initialize();
        }
    }
}
