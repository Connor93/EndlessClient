using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EOLib.Config;

namespace EndlessClient.Rendering.Factories
{
    [AutoMappedType]
    public class HealthBarRendererFactory : IHealthBarRendererFactory
    {
        private readonly IEndlessGameProvider _endlessGameProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IContentProvider _contentProvider;

        public HealthBarRendererFactory(IEndlessGameProvider endlessGameProvider,
                                        IClientWindowSizeProvider clientWindowSizeProvider,
                                        IConfigurationProvider configurationProvider,
                                        IContentProvider contentProvider)
        {
            _endlessGameProvider = endlessGameProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _configurationProvider = configurationProvider;
            _contentProvider = contentProvider;
        }

        public IHealthBarRenderer CreateHealthBarRenderer(IMapActor parentReference)
        {
            return new HealthBarRenderer(_endlessGameProvider, _clientWindowSizeProvider, _configurationProvider, _contentProvider, parentReference);
        }
    }

    public interface IHealthBarRendererFactory
    {
        IHealthBarRenderer CreateHealthBarRenderer(IMapActor entity);
    }
}
