using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType(IsSingleton = true)]
    public class PaperdollDialogFactory : IPaperdollDialogFactory
    {
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IPaperdollProvider _paperdollProvider;
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private IInventoryController _inventoryController;

        public PaperdollDialogFactory(INativeGraphicsManager nativeGraphicsManager,
            IPaperdollProvider paperdollProvider,
            IPubFileProvider pubFileProvider,
            IHudControlProvider hudControlProvider,
            IEODialogButtonService eoDialogButtonService,
            IInventorySpaceValidator inventorySpaceValidator,
            IEOMessageBoxFactory eoMessageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ISfxPlayer sfxPlayer,
            IConfigurationProvider configProvider,
            IUIStyleProviderFactory styleProviderFactory,
            IGameStateProvider gameStateProvider,
            IContentProvider contentProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _paperdollProvider = paperdollProvider;
            _pubFileProvider = pubFileProvider;
            _hudControlProvider = hudControlProvider;
            _nativeGraphicsManager = nativeGraphicsManager;
            _eoDialogButtonService = eoDialogButtonService;
            _inventorySpaceValidator = inventorySpaceValidator;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _contentProvider = contentProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        public IXNADialog Create(Character character, bool isMainCharacter)
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnPaperdollDialog(
                    _styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _nativeGraphicsManager,
                    _inventoryController,
                    _paperdollProvider,
                    _pubFileProvider,
                    _hudControlProvider,
                    _inventorySpaceValidator,
                    _eoMessageBoxFactory,
                    _statusLabelSetter,
                    _sfxPlayer,
                    _contentProvider,
                    character,
                    isMainCharacter);
            }

            return new PaperdollDialog(_nativeGraphicsManager,
                _inventoryController,
                _paperdollProvider,
                _pubFileProvider,
                _hudControlProvider,
                _eoDialogButtonService,
                _inventorySpaceValidator,
                _eoMessageBoxFactory,
                _statusLabelSetter,
                _sfxPlayer,
                character,
                isMainCharacter);
        }

        public void InjectInventoryController(IInventoryController inventoryController)
        {
            _inventoryController = inventoryController;
        }
    }

    public interface IPaperdollDialogFactory
    {
        IXNADialog Create(Character character, bool isMainCharacter);

        void InjectInventoryController(IInventoryController inventoryController);
    }
}

