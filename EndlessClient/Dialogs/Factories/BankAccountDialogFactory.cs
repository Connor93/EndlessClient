using AutomaticTypeMapper;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Services;
using EndlessClient.HUD;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Bank;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class BankAccountDialogFactory : IBankAccountDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IBankActions _bankActions;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IEODialogIconService _dialogIconService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IBankDataProvider _bankDataProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public BankAccountDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                        IBankActions bankActions,
                                        IEODialogButtonService dialogButtonService,
                                        IEODialogIconService dialogIconService,
                                        ILocalizedStringFinder localizedStringFinder,
                                        IStatusLabelSetter statusLabelSetter,
                                        IEOMessageBoxFactory messageBoxFactory,
                                        IItemTransferDialogFactory itemTransferDialogFactory,
                                        IHudControlProvider hudControlProvider,
                                        IBankDataProvider bankDataProvider,
                                        ICharacterInventoryProvider characterInventoryProvider,
                                        IEIFFileProvider eifFileProvider,
                                        IConfigurationProvider configProvider,
                                        IMyraUIManager myraUIManager,
                                        IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _bankActions = bankActions;
            _dialogButtonService = dialogButtonService;
            _dialogIconService = dialogIconService;
            _localizedStringFinder = localizedStringFinder;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _hudControlProvider = hudControlProvider;
            _bankDataProvider = bankDataProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _configProvider = configProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configProvider.UIMode != UIMode.Gfx)
            {
                return new MyraBankAccountDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _bankActions,
                    _localizedStringFinder,
                    _statusLabelSetter,
                    _messageBoxFactory,
                    _itemTransferDialogFactory,
                    _bankDataProvider,
                    _characterInventoryProvider,
                    _eifFileProvider);
            }

            return new BankAccountDialog(_nativeGraphicsManager,
                                         _bankActions,
                                         _dialogButtonService,
                                         _dialogIconService,
                                         _localizedStringFinder,
                                         _statusLabelSetter,
                                         _messageBoxFactory,
                                         _itemTransferDialogFactory,
                                         _hudControlProvider,
                                         _bankDataProvider,
                                         _characterInventoryProvider,
                                         _eifFileProvider);
        }
    }

    public interface IBankAccountDialogFactory
    {
        IXNADialog Create();
    }
}
