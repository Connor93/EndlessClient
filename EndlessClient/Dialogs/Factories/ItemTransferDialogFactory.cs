using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class ItemTransferDialogFactory : IItemTransferDialogFactory
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IChatTextBoxActions _chatTextBoxActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ISfxPlayer _sfxPlayer;

        public ItemTransferDialogFactory(IUIStyleProvider styleProvider,
                                         IGameStateProvider gameStateProvider,
                                         IClientWindowSizeProvider clientWindowSizeProvider,
                                         IGraphicsDeviceProvider graphicsDeviceProvider,
                                         IContentProvider contentProvider,
                                         IChatTextBoxActions chatTextBoxActions,
                                         ILocalizedStringFinder localizedStringFinder,
                                         ISfxPlayer sfxPlayer)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
            _chatTextBoxActions = chatTextBoxActions;
            _localizedStringFinder = localizedStringFinder;
            _sfxPlayer = sfxPlayer;
        }

        public CodeDrawnItemTransferDialog CreateItemTransferDialog(string itemName, CodeDrawnItemTransferDialog.TransferType transferType, int totalAmount, EOResourceID message)
        {
            var dlg = new CodeDrawnItemTransferDialog(_styleProvider,
                _gameStateProvider,
                _clientWindowSizeProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _chatTextBoxActions,
                _localizedStringFinder,
                itemName,
                transferType,
                totalAmount,
                message);

            dlg.DialogClosing += (sender, args) =>
            {
                if (args.Result == XNADialogResult.Cancel)
                {
                    _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);
                }
            };
            return dlg;
        }
    }

    public interface IItemTransferDialogFactory
    {
        CodeDrawnItemTransferDialog CreateItemTransferDialog(string itemName, CodeDrawnItemTransferDialog.TransferType transferType, int totalAmount, EOResourceID message);
    }
}
