using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Config;
using EOLib.Graphics;

namespace EndlessClient.Dialogs.Factories
{
    [MappedType(BaseType = typeof(ICreateCharacterDialogFactory))]
    public class CreateCharacterDialogFactory : ICreateCharacterDialogFactory
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly ICharacterRendererFactory _characterRendererFactory;
        private readonly IContentProvider _contentProvider;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IXnaControlSoundMapper _xnaControlSoundMapper;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;
        private readonly IConfigurationProvider _configProvider;

        public CreateCharacterDialogFactory(IUIStyleProvider styleProvider,
                                            IGameStateProvider gameStateProvider,
                                            ICharacterRendererFactory characterRendererFactory,
                                            IContentProvider contentProvider,
                                            IEOMessageBoxFactory eoMessageBoxFactory,
                                            IXnaControlSoundMapper xnaControlSoundMapper,
                                            IClientWindowSizeProvider clientWindowSizeProvider,
                                            IGraphicsDeviceProvider graphicsDeviceProvider,
                                            IMyraUIManager myraUIManager,
                                            IMyraFontProvider myraFontProvider,
                                            IConfigurationProvider configProvider)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _characterRendererFactory = characterRendererFactory;
            _contentProvider = contentProvider;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _xnaControlSoundMapper = xnaControlSoundMapper;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
            _configProvider = configProvider;
        }

        public ICreateCharacterResult BuildCreateCharacterDialog()
        {
            if (_configProvider.UIMode != UIMode.Gfx)
            {
                var dialog = new MyraCreateCharacterDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _characterRendererFactory,
                    _eoMessageBoxFactory,
                    _clientWindowSizeProvider);
                dialog.InitializeOverlay();
                return dialog;
            }

            return new CodeDrawnCreateCharacterDialog(_styleProvider,
                                                      _gameStateProvider,
                                                      _characterRendererFactory,
                                                      _contentProvider,
                                                      _eoMessageBoxFactory,
                                                      _xnaControlSoundMapper,
                                                      _clientWindowSizeProvider,
                                                      _graphicsDeviceProvider);
        }
    }

    /// <summary>
    /// Common interface for accessing character creation results from either dialog type.
    /// </summary>
    public interface ICreateCharacterResult : XNAControls.IXNADialog
    {
        string CharacterName { get; }
        int Gender { get; }
        int HairStyle { get; }
        int HairColor { get; }
        int Race { get; }
    }

    public interface ICreateCharacterDialogFactory
    {
        ICreateCharacterResult BuildCreateCharacterDialog();
    }
}
