using AutomaticTypeMapper;
using EndlessClient.GameExecution;
using EOLib.Config;
using EOLib.Graphics;

namespace EndlessClient.Rendering.Factories
{
    [AutoMappedType]
    public class HealthBarRendererFactory : IHealthBarRendererFactory
    {
        private readonly IEndlessGameProvider _endlessGameProvider;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IConfigurationProvider _configurationProvider;

        public HealthBarRendererFactory(IEndlessGameProvider endlessGameProvider,
                                        INativeGraphicsManager nativeGraphicsManager,
                                        IClientWindowSizeProvider clientWindowSizeProvider,
                                        IConfigurationProvider configurationProvider)
        {
            _endlessGameProvider = endlessGameProvider;
            _nativeGraphicsManager = nativeGraphicsManager;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _configurationProvider = configurationProvider;
        }

        public IHealthBarRenderer CreateHealthBarRenderer(IMapActor parentReference)
        {
            return new HealthBarRenderer(_endlessGameProvider, _nativeGraphicsManager, _clientWindowSizeProvider, _configurationProvider, parentReference);
        }
    }

    public interface IHealthBarRendererFactory
    {
        IHealthBarRenderer CreateHealthBarRenderer(IMapActor entity);
    }
}
