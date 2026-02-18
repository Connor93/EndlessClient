using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.Rendering.Factories;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.IO.Repositories;

namespace EndlessClient.Rendering.Map
{
    [AutoMappedType]
    public class MiniMapRendererFactory : IMiniMapRendererFactory
    {
        private readonly IEndlessGameProvider _endlessGameProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly ICurrentMapStateRepository _currentMapStateProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IRenderTargetFactory _renderTargetFactory;
        private readonly IContentProvider _contentProvider;

        public MiniMapRendererFactory(IEndlessGameProvider endlessGameProvider,
                                      IRenderTargetFactory renderTargetFactory,
                                      IClientWindowSizeProvider clientWindowSizeProvider,
                                      ICurrentMapProvider currentMapProvider,
                                      ICurrentMapStateRepository currentMapStateProvider,
                                      ICharacterProvider characterProvider,
                                      IENFFileProvider enfFileProvider,
                                      IContentProvider contentProvider)
        {
            _endlessGameProvider = endlessGameProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _currentMapProvider = currentMapProvider;
            _currentMapStateProvider = currentMapStateProvider;
            _characterProvider = characterProvider;
            _enfFileProvider = enfFileProvider;
            _renderTargetFactory = renderTargetFactory;
            _contentProvider = contentProvider;
        }

        public RadarMiniMapRenderer Create()
        {
            return new RadarMiniMapRenderer(_endlessGameProvider,
                                            _currentMapProvider,
                                            _currentMapStateProvider,
                                            _characterProvider,
                                            _enfFileProvider,
                                            _clientWindowSizeProvider,
                                            _contentProvider,
                                            _renderTargetFactory);
        }
    }

    public interface IMiniMapRendererFactory
    {
        RadarMiniMapRenderer Create();
    }
}
