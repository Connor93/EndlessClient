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
    public class NpcInfoDialogFactory : INpcInfoDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly INpcSourceProvider _npcSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;

        public NpcInfoDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                    IEODialogButtonService dialogButtonService,
                                    INpcSourceProvider npcSourceProvider,
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
            _npcSourceProvider = npcSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
        }

        public IXNADialog Create(ENFRecord npc)
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnNpcInfoDialog(
                    _styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _contentProvider,
                    _npcSourceProvider,
                    _eifFileProvider,
                    _nativeGraphicsManager,
                    npc);
            }

            // Load NPC graphic from GFXTypes.NPC
            // Formula: (graphic - 1) * 40 + frame_offset (1 = standing south)
            var npcGraphic = npc.Graphic > 0
                ? _nativeGraphicsManager.TextureFromResource(GFXTypes.NPC, (npc.Graphic - 1) * 40 + 1, transparent: true)
                : null;

            return new NpcInfoDialog(_nativeGraphicsManager, _dialogButtonService,
                                     _npcSourceProvider, _eifFileProvider, _enfFileProvider,
                                     npc, npcGraphic);
        }
    }

    public interface INpcInfoDialogFactory
    {
        IXNADialog Create(ENFRecord npc);
    }
}
