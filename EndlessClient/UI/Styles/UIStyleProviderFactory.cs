using AutomaticTypeMapper;
using EOLib.Config;

namespace EndlessClient.UI.Styles
{
    public interface IUIStyleProviderFactory
    {
        IUIStyleProvider Create();
    }

    [MappedType(BaseType = typeof(IUIStyleProviderFactory))]
    public class UIStyleProviderFactory : IUIStyleProviderFactory
    {
        private readonly IConfigurationProvider _configProvider;
        private readonly GlassmorphismStyleProvider _glassStyle;
        private readonly FlatStyleProvider _flatStyle;
        private readonly ClassicStyleProvider _classicStyle;

        public UIStyleProviderFactory(IConfigurationProvider configProvider,
                                      GlassmorphismStyleProvider glassStyle,
                                      FlatStyleProvider flatStyle,
                                      ClassicStyleProvider classicStyle)
        {
            _configProvider = configProvider;
            _glassStyle = glassStyle;
            _flatStyle = flatStyle;
            _classicStyle = classicStyle;
        }

        public IUIStyleProvider Create()
        {
            return _configProvider.UIStyle switch
            {
                UIStyle.Glass => _glassStyle,
                UIStyle.Flat => _flatStyle,
                UIStyle.Classic => _classicStyle,
                _ => _glassStyle
            };
        }
    }
}
