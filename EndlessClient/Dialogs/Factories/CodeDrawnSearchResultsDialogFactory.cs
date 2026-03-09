using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Shared;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class CodeDrawnSearchResultsDialogFactory : ICodeDrawnSearchResultsDialogFactory
    {
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public CodeDrawnSearchResultsDialogFactory(
            IUIStyleProviderFactory styleProviderFactory,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IConfigurationProvider configurationProvider,
            IMyraUIManager myraUIManager,
            IMyraFontProvider myraFontProvider)
        {
            _styleProviderFactory = styleProviderFactory;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public ISearchResultsDialog Create(string title)
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraSearchResultsDialog(
                    _myraUIManager,
                    _myraFontProvider)
                {
                    Title = title
                };
            }

            var font = _contentProvider.Fonts[Constants.FontSize08];
            var headerFont = _contentProvider.Fonts[Constants.FontSize09];
            var scaledFont = _contentProvider.Fonts[Constants.FontSize10];
            var scaledHeaderFont = _contentProvider.Fonts[Constants.FontSize10];

            return new CodeDrawnSearchResultsDialog(
                _styleProviderFactory.Create(),
                _clientWindowSizeProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                font,
                headerFont,
                scaledFont,
                scaledHeaderFont)
            {
                Title = title
            };
        }
    }

    public interface ICodeDrawnSearchResultsDialogFactory
    {
        ISearchResultsDialog Create(string title);
    }
}
