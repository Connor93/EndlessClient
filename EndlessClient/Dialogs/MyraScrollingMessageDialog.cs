using EndlessClient.Audio;
using EndlessClient.UI.Myra;
using EOLib.Localization;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraScrollingMessageDialog : MyraDialogAdapter
    {
        private readonly Label _messageLabel;

        public string MessageText
        {
            set => _messageLabel.Text = value ?? string.Empty;
        }

        public MyraScrollingMessageDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider)
            : base(uiManager, string.Empty)
        {
            Window.Width = 360;
            Window.Height = 240;
            Window.TitleFont = fontProvider.Header;

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _messageLabel = new Label
            {
                Text = string.Empty,
                Font = fontProvider.Normal,
                Wrap = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _messageLabel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            mainPanel.Widgets.Add(scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

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
    }
}
