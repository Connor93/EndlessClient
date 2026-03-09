using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using EOLib.Config;
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
        private readonly IConfigurationProvider _configProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public ItemTransferDialogFactory(IUIStyleProvider styleProvider,
                                         IGameStateProvider gameStateProvider,
                                         IClientWindowSizeProvider clientWindowSizeProvider,
                                         IGraphicsDeviceProvider graphicsDeviceProvider,
                                         IContentProvider contentProvider,
                                         IChatTextBoxActions chatTextBoxActions,
                                         ILocalizedStringFinder localizedStringFinder,
                                         ISfxPlayer sfxPlayer,
                                         IConfigurationProvider configProvider,
                                         IMyraUIManager myraUIManager,
                                         IMyraFontProvider myraFontProvider)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
            _chatTextBoxActions = chatTextBoxActions;
            _localizedStringFinder = localizedStringFinder;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IItemTransferDialog CreateItemTransferDialog(string itemName, ItemTransferType transferType, int totalAmount, EOResourceID message)
        {
            IItemTransferDialog dlg;

            if (_configProvider.UIMode != UIMode.Gfx)
            {
                dlg = new MyraItemTransferDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    itemName,
                    transferType,
                    totalAmount,
                    message);
            }
            else
            {
                dlg = new CodeDrawnItemTransferDialog(_styleProvider,
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
            }

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
        IItemTransferDialog CreateItemTransferDialog(string itemName, ItemTransferType transferType, int totalAmount, EOResourceID message);
    }
}

