using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Domain.IniEditor;
using EOLib.Graphics;
using EOLib.Shared;


namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class IniEditorDialogFactory : IIniEditorDialogFactory
    {
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IIniEditorActions _iniEditorActions;
        private readonly IIniEditorProvider _iniEditorProvider;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IContentProvider _contentProvider;
        private readonly IHudControlProvider _hudControlProvider;

        public IniEditorDialogFactory(IUIStyleProviderFactory styleProviderFactory,
                                      IGameStateProvider gameStateProvider,
                                      IClientWindowSizeProvider clientWindowSizeProvider,
                                      IGraphicsDeviceProvider graphicsDeviceProvider,
                                      IIniEditorActions iniEditorActions,
                                      IIniEditorProvider iniEditorProvider,
                                      IEOMessageBoxFactory eoMessageBoxFactory,
                                      IContentProvider contentProvider,
                                      IHudControlProvider hudControlProvider)
        {
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _iniEditorActions = iniEditorActions;
            _iniEditorProvider = iniEditorProvider;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _contentProvider = contentProvider;
            _hudControlProvider = hudControlProvider;
        }

        public IniEditorDialog Create()
        {
            var font = _contentProvider.Fonts[Constants.FontSize08];
            var scaledFont = _contentProvider.Fonts[Constants.FontSize10];

            return new IniEditorDialog(_styleProviderFactory.Create(),
                                       _gameStateProvider,
                                       _clientWindowSizeProvider,
                                       _graphicsDeviceProvider,
                                       _iniEditorActions,
                                       _iniEditorProvider,
                                       _eoMessageBoxFactory,
                                       _contentProvider,
                                       _hudControlProvider,
                                       font,
                                       scaledFont);
        }
    }

    public interface IIniEditorDialogFactory
    {
        IniEditorDialog Create();
    }
}
