using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class LockerDialogFactory : ILockerDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly ILockerActions _lockerActions;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataProvider _lockerDataProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        public LockerDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                   ILockerActions lockerActions,
                                   IEODialogButtonService dialogButtonService,
                                   ILocalizedStringFinder localizedStringFinder,
                                   IInventorySpaceValidator inventorySpaceValidator,
                                   IStatusLabelSetter statusLabelSetter,
                                   IEOMessageBoxFactory messageBoxFactory,
                                   ICharacterProvider characterProvider,
                                   ILockerDataProvider lockerDataProvider,
                                   IHudControlProvider hudControlProvider,
                                   IEIFFileProvider eifFileProvider,
                                   IConfigurationProvider configProvider,
                                   IUIStyleProviderFactory styleProviderFactory,
                                   IGameStateProvider gameStateProvider,
                                   IContentProvider contentProvider,
                                   IClientWindowSizeProvider clientWindowSizeProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _lockerActions = lockerActions;
            _dialogButtonService = dialogButtonService;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _characterProvider = characterProvider;
            _lockerDataProvider = lockerDataProvider;
            _hudControlProvider = hudControlProvider;
            _eifFileProvider = eifFileProvider;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _contentProvider = contentProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        public IXNADialog Create()
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnLockerDialog(_styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _nativeGraphicsManager,
                    _lockerActions,
                    _localizedStringFinder,
                    _inventorySpaceValidator,
                    _statusLabelSetter,
                    _messageBoxFactory,
                    _characterProvider,
                    _lockerDataProvider,
                    _eifFileProvider,
                    _contentProvider);
            }

            return new LockerDialog(_nativeGraphicsManager,
                                    _lockerActions,
                                    _dialogButtonService,
                                    _localizedStringFinder,
                                    _inventorySpaceValidator,
                                    _statusLabelSetter,
                                    _messageBoxFactory,
                                    _characterProvider,
                                    _lockerDataProvider,
                                    _hudControlProvider,
                                    _eifFileProvider);
        }
    }

    public interface ILockerDialogFactory
    {
        IXNADialog Create();
    }
}

