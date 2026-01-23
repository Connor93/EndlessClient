using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Styles;
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

        public CodeDrawnSearchResultsDialogFactory(
            IUIStyleProviderFactory styleProviderFactory,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider)
        {
            _styleProviderFactory = styleProviderFactory;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
        }

        public CodeDrawnSearchResultsDialog Create(string title)
        {
            var font = _contentProvider.Fonts[Constants.FontSize08];
            var headerFont = _contentProvider.Fonts[Constants.FontSize09];
            var scaledFont = _contentProvider.Fonts[Constants.FontSize10];
            var scaledHeaderFont = _contentProvider.Fonts[Constants.FontSize10];

            var dialog = new CodeDrawnSearchResultsDialog(
                _styleProviderFactory.Create(),
                _clientWindowSizeProvider,
                _graphicsDeviceProvider,
                font,
                headerFont,
                scaledFont,
                scaledHeaderFont)
            {
                Title = title
            };

            return dialog;
        }
    }

    public interface ICodeDrawnSearchResultsDialogFactory
    {
        CodeDrawnSearchResultsDialog Create(string title);
    }
}
