using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Barber;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class BarberDialogFactory : IBarberDialogFactory
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly ICharacterRendererFactory _characterRendererFactory;
        private readonly IContentProvider _contentProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ICharacterRepository _characterRepository;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IBarberActions _barberActions;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public BarberDialogFactory(IUIStyleProvider styleProvider,
                                   IGameStateProvider gameStateProvider,
                                   ICharacterRendererFactory characterRendererFactory,
                                   IContentProvider contentProvider,
                                   IEOMessageBoxFactory messageBoxFactory,
                                   ICharacterRepository characterRepository,
                                   ILocalizedStringFinder localizedStringFinder,
                                   IBarberActions barberActions,
                                   ICharacterInventoryProvider characterInventoryProvider,
                                   IEIFFileProvider eifFileProvider,
                                   ISfxPlayer sfxPlayer,
                                   IClientWindowSizeProvider clientWindowSizeProvider,
                                   IGraphicsDeviceProvider graphicsDeviceProvider,
                                   IConfigurationProvider configProvider,
                                   IMyraUIManager myraUIManager,
                                   IMyraFontProvider myraFontProvider)
        {
            _styleProvider = styleProvider;
            _gameStateProvider = gameStateProvider;
            _characterRendererFactory = characterRendererFactory;
            _contentProvider = contentProvider;
            _messageBoxFactory = messageBoxFactory;
            _characterRepository = characterRepository;
            _localizedStringFinder = localizedStringFinder;
            _barberActions = barberActions;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _sfxPlayer = sfxPlayer;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _configProvider = configProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IBarberDialog Create()
        {
            if (_configProvider.UIMode != UIMode.Gfx)
            {
                return new MyraBarberDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _characterRendererFactory,
                    _characterRepository,
                    _clientWindowSizeProvider,
                    _localizedStringFinder,
                    _barberActions,
                    _characterInventoryProvider,
                    _eifFileProvider);
            }

            return new CodeDrawnBarberDialog(_styleProvider,
                                             _gameStateProvider,
                                             _characterRendererFactory,
                                             _contentProvider,
                                             _messageBoxFactory,
                                             _characterRepository,
                                             _localizedStringFinder,
                                             _barberActions,
                                             _characterInventoryProvider,
                                             _eifFileProvider,
                                             _sfxPlayer,
                                             _clientWindowSizeProvider,
                                             _graphicsDeviceProvider);
        }
    }

    public interface IBarberDialogFactory
    {
        IBarberDialog Create();
    }
}
