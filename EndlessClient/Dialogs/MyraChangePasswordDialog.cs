using System;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Account;
using EOLib.Domain.Login;
using EOLib.Localization;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraChangePasswordDialog : MyraDialogAdapter, IChangePasswordDialog
    {
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IPlayerInfoProvider _playerInfoProvider;

        private readonly TextBox _usernameBox;
        private readonly TextBox _oldPasswordBox;
        private readonly TextBox _newPasswordBox;
        private readonly TextBox _confirmPasswordBox;

        public IChangePasswordParameters Result =>
            new ChangePasswordParameters(_usernameBox.Text, _oldPasswordBox.Text, _newPasswordBox.Text);

        public MyraChangePasswordDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            IEOMessageBoxFactory eoMessageBoxFactory,
            IPlayerInfoProvider playerInfoProvider)
            : base(uiManager, "Change Password")
        {
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _playerInfoProvider = playerInfoProvider;

            Window.Width = 320;
            Window.Height = 260;
            Window.TitleFont = fontProvider.Header;

            var mainPanel = new VerticalStackPanel { Spacing = 8, Padding = new Thickness(8) };

            var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

            var labels = new[] { "Username", "Old Password", "New Password", "Confirm Password" };
            _usernameBox = CreateTextBox(false);
            _oldPasswordBox = CreateTextBox(true);
            _newPasswordBox = CreateTextBox(true);
            _confirmPasswordBox = CreateTextBox(true);

            var textBoxes = new[] { _usernameBox, _oldPasswordBox, _newPasswordBox, _confirmPasswordBox };

            for (int i = 0; i < labels.Length; i++)
            {
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

                var label = new Label { Text = labels[i] + ":", Font = fontProvider.Normal };
                Grid.SetColumn(label, 0);
                Grid.SetRow(label, i);
                grid.Widgets.Add(label);

                Grid.SetColumn(textBoxes[i], 1);
                Grid.SetRow(textBoxes[i], i);
                grid.Widgets.Add(textBoxes[i]);
            }

            mainPanel.Widgets.Add(grid);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // Button bar
            var buttonBar = new HorizontalStackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var okButton = new Button
            {
                Content = new Label { Text = "OK", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
            };
            okButton.Click += (_, _) => OnOkClicked();

            var cancelButton = new Button
            {
                Content = new Label { Text = "Cancel", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
            };
            cancelButton.Click += (_, _) => Close(XNADialogResult.Cancel);

            buttonBar.Widgets.Add(okButton);
            buttonBar.Widgets.Add(cancelButton);

            mainPanel.Widgets.Add(buttonBar);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;
        }

        private TextBox CreateTextBox(bool isPassword)
        {
            return new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 24,
                PasswordField = isPassword,
            };
        }

        private void OnOkClicked()
        {
            var boxes = new[] { _usernameBox, _oldPasswordBox, _newPasswordBox, _confirmPasswordBox };
            if (boxes.Any(tb => string.IsNullOrWhiteSpace(tb.Text)))
                return;

            if (_usernameBox.Text != _playerInfoProvider.LoggedInAccountName)
            {
                var messageBox = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.CHANGE_PASSWORD_MISMATCH);
                messageBox.ShowDialog();
                return;
            }

            if (_oldPasswordBox.Text != _playerInfoProvider.PlayerPassword)
            {
                var messageBox = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.CHANGE_PASSWORD_MISMATCH);
                messageBox.ShowDialog();
                return;
            }

            if (_newPasswordBox.Text != _confirmPasswordBox.Text)
            {
                var messageBox = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.ACCOUNT_CREATE_PASSWORD_MISMATCH);
                messageBox.ShowDialog();
                return;
            }

            if (_newPasswordBox.Text.Length < 6)
            {
                var messageBox = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.ACCOUNT_CREATE_PASSWORD_TOO_SHORT);
                messageBox.ShowDialog();
                return;
            }

            Close(XNADialogResult.OK);
        }
    }
}
