using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class ItemInfoDialogFactory : IItemInfoDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IItemSourceProvider _itemSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;

        public ItemInfoDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                     IEODialogButtonService dialogButtonService,
                                     IItemSourceProvider itemSourceProvider,
                                     IEIFFileProvider eifFileProvider,
                                     IENFFileProvider enfFileProvider,
                                     IConfigurationProvider configProvider,
                                     IUIStyleProviderFactory styleProviderFactory,
                                     IGameStateProvider gameStateProvider,
                                     IClientWindowSizeProvider clientWindowSizeProvider,
                                     IGraphicsDeviceProvider graphicsDeviceProvider,
                                     IContentProvider contentProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _itemSourceProvider = itemSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
        }

        public IXNADialog Create(EIFRecord item)
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnItemInfoDialog(
                    _styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _contentProvider,
                    _itemSourceProvider,
                    _eifFileProvider,
                    _enfFileProvider,
                    _nativeGraphicsManager,
                    item);
            }

            // Load item graphic from GFXTypes.Items (23)
            var itemGraphic = item.Graphic > 0
                ? _nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Graphic - 1, transparent: true)
                : null;

            return new ItemInfoDialog(_nativeGraphicsManager, _dialogButtonService,
                                       _itemSourceProvider, _eifFileProvider, _enfFileProvider,
                                       item, itemGraphic);
        }
    }

    public interface IItemInfoDialogFactory
    {
        IXNADialog Create(EIFRecord item);
    }
}
