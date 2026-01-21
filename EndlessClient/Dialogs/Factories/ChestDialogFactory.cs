using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering.Map;
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
    public class ChestDialogFactory : IChestDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IChestActions _chestActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IMapItemGraphicProvider _mapItemGraphicProvider;
        private readonly IChestDataProvider _chestDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IContentProvider _contentProvider;

        public ChestDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                  IChestActions chestActions,
                                  IEOMessageBoxFactory messageBoxFactory,
                                  IEODialogButtonService dialogButtonService,
                                  IStatusLabelSetter statusLabelSetter,
                                  ILocalizedStringFinder localizedStringFinder,
                                  IInventorySpaceValidator inventorySpaceValidator,
                                  IMapItemGraphicProvider mapItemGraphicProvider,
                                  IChestDataProvider chestDataProvider,
                                  IEIFFileProvider eifFileProvider,
                                  ICharacterProvider characterProvider,
                                  IConfigurationProvider configProvider,
                                  IUIStyleProviderFactory styleProviderFactory,
                                  IGameStateProvider gameStateProvider,
                                  IContentProvider contentProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _chestActions = chestActions;
            _messageBoxFactory = messageBoxFactory;
            _dialogButtonService = dialogButtonService;
            _statusLabelSetter = statusLabelSetter;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _mapItemGraphicProvider = mapItemGraphicProvider;
            _chestDataProvider = chestDataProvider;
            _eifFileProvider = eifFileProvider;
            _characterProvider = characterProvider;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _contentProvider = contentProvider;
        }

        public IXNADialog Create()
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnChestDialog(_styleProviderFactory.Create(),
                    _gameStateProvider,
                    _chestActions,
                    _messageBoxFactory,
                    _statusLabelSetter,
                    _localizedStringFinder,
                    _inventorySpaceValidator,
                    _mapItemGraphicProvider,
                    _chestDataProvider,
                    _eifFileProvider,
                    _characterProvider,
                    _contentProvider);
            }

            return new ChestDialog(_nativeGraphicsManager,
                                   _chestActions,
                                   _messageBoxFactory,
                                   _dialogButtonService,
                                   _statusLabelSetter,
                                   _localizedStringFinder,
                                   _inventorySpaceValidator,
                                   _mapItemGraphicProvider,
                                   _chestDataProvider,
                                   _eifFileProvider,
                                   _characterProvider);
        }
    }

    public interface IChestDialogFactory
    {
        IXNADialog Create();
    }
}

