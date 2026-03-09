using System;
using EndlessClient.UI.Myra;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraGameLoadingDialog : MyraDialogAdapter, IGameLoadingDialog
    {
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly Label _captionLabel;
        private readonly Label _messageLabel;

        public MyraGameLoadingDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder)
            : base(uiManager, localizedStringFinder.GetString(EOResourceID.LOADING_GAME_PLEASE_WAIT))
        {
            _localizedStringFinder = localizedStringFinder;

            Window.Width = 250;
            Window.Height = 120;
            Window.TitleFont = fontProvider.Header;
            Window.CloseButton.Visible = false;

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _captionLabel = new Label
            {
                Text = localizedStringFinder.GetString(EOResourceID.LOADING_GAME_PLEASE_WAIT),
                Font = fontProvider.Normal,
                Wrap = true,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            mainPanel.Widgets.Add(_captionLabel);

            var gen = new Random();
            var messageTextID = (EOResourceID)gen.Next(
                (int)EOResourceID.LOADING_GAME_HINT_FIRST,
                (int)EOResourceID.LOADING_GAME_HINT_LAST);

            _messageLabel = new Label
            {
                Text = localizedStringFinder.GetString(messageTextID),
                Font = fontProvider.Normal,
                Wrap = true,
                TextColor = new Color(160, 160, 180),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            mainPanel.Widgets.Add(_messageLabel);

            Window.Content = mainPanel;
        }

        public void SetState(GameLoadingDialogState whichState)
        {
            _captionLabel.Text = whichState switch
            {
                GameLoadingDialogState.Map => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_UPDATING_MAP),
                GameLoadingDialogState.Item => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_UPDATING_ITEMS),
                GameLoadingDialogState.NPC => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_UPDATING_NPCS),
                GameLoadingDialogState.Spell => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_UPDATING_SKILLS),
                GameLoadingDialogState.Class => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_UPDATING_CLASSES),
                GameLoadingDialogState.LoadingGame => _localizedStringFinder.GetString(EOResourceID.LOADING_GAME_LOADING_GAME),
                _ => throw new ArgumentOutOfRangeException(nameof(whichState), whichState, null)
            };
        }

        public void CloseDialog()
        {
            Close(XNADialogResult.NO_BUTTON_PRESSED);
        }
    }
}
