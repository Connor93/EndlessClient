using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
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

        public CreateCharacterDialogFactory(IUIStyleProvider styleProvider,
                                            IGameStateProvider gameStateProvider,
                                            ICharacterRendererFactory characterRendererFactory,
                                            IContentProvider contentProvider,
                                            IEOMessageBoxFactory eoMessageBoxFactory,
                                            IXnaControlSoundMapper xnaControlSoundMapper,
                                            IClientWindowSizeProvider clientWindowSizeProvider,
                                            IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _characterRendererFactory = characterRendererFactory;
            _contentProvider = contentProvider;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _xnaControlSoundMapper = xnaControlSoundMapper;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        public CodeDrawnCreateCharacterDialog BuildCreateCharacterDialog()
        {
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

    public interface ICreateCharacterDialogFactory
    {
        CodeDrawnCreateCharacterDialog BuildCreateCharacterDialog();
    }
}
