using System;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraProgressDialog : MyraDialogAdapter
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly HorizontalProgressBar _progressBar;
        private TimeSpan? _timeOpened;

        public MyraProgressDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IConfigurationProvider configurationProvider,
            string messageText,
            string captionText)
            : base(uiManager, captionText)
        {
            _configurationProvider = configurationProvider;

            Window.Width = 300;
            Window.Height = 160;
            Window.TitleFont = fontProvider.Header;
            Window.CloseButton.Visible = false;

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var messageLabel = new Label
            {
                Text = messageText,
                Font = fontProvider.Normal,
                Wrap = true,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            mainPanel.Widgets.Add(messageLabel);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            _progressBar = new HorizontalProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Filler = new SolidBrush(new Color(0xb4, 0xdc, 0xe6)),
            };
            mainPanel.Widgets.Add(_progressBar);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

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
            if (_timeOpened == null)
                _timeOpened = gameTime.TotalGameTime;

            var pbPercent = (int)((gameTime.TotalGameTime.TotalSeconds - _timeOpened.Value.TotalSeconds)
                / _configurationProvider.AccountCreateTimeout.TotalSeconds * 100);
            _progressBar.Value = Math.Min(pbPercent, 100);

            if (pbPercent >= 100)
                Close(XNADialogResult.NO_BUTTON_PRESSED);

            base.Update(gameTime);
        }
    }
}
