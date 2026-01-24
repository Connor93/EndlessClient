using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Shop;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class ShopDialogFactory : IShopDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IShopActions _shopActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IEODialogIconService _dialogIconService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IShopDataProvider _shopDataProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        public ShopDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                 IShopActions shopActions,
                                 IEOMessageBoxFactory messageBoxFactory,
                                 IItemTransferDialogFactory itemTransferDialogFactory,
                                 IEODialogButtonService dialogButtonService,
                                 IEODialogIconService dialogIconService,
                                 ILocalizedStringFinder localizedStringFinder,
                                 IShopDataProvider shopDataProvider,
                                 ICharacterInventoryProvider characterInventoryProvider,
                                 IEIFFileProvider eifFileProvider,
                                 ICharacterProvider characterProvider,
                                 IInventorySpaceValidator inventorySpaceValidator,
                                 IConfigurationProvider configProvider,
                                 IUIStyleProviderFactory styleProviderFactory,
                                 IGameStateProvider gameStateProvider,
                                 IContentProvider contentProvider,
                                 IClientWindowSizeProvider clientWindowSizeProvider,
                                 IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _shopActions = shopActions;
            _messageBoxFactory = messageBoxFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _dialogButtonService = dialogButtonService;
            _dialogIconService = dialogIconService;
            _localizedStringFinder = localizedStringFinder;
            _shopDataProvider = shopDataProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _characterProvider = characterProvider;
            _inventorySpaceValidator = inventorySpaceValidator;
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
                return new CodeDrawnShopDialog(_styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _nativeGraphicsManager,
                    _shopActions,
                    _messageBoxFactory,
                    _itemTransferDialogFactory,
                    _localizedStringFinder,
                    _shopDataProvider,
                    _characterInventoryProvider,
                    _eifFileProvider,
                    _characterProvider,
                    _inventorySpaceValidator,
                    _contentProvider);
            }

            return new ShopDialog(_nativeGraphicsManager,
                _shopActions,
                _messageBoxFactory,
                _itemTransferDialogFactory,
                _dialogButtonService,
                _dialogIconService,
                _localizedStringFinder,
                _shopDataProvider,
                _characterInventoryProvider,
                _eifFileProvider,
                _characterProvider,
                _inventorySpaceValidator);
        }
    }

    public interface IShopDialogFactory
    {
        IXNADialog Create();
    }
}

