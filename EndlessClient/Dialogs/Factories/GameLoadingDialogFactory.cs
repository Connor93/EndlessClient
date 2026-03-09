using AutomaticTypeMapper;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Localization;

namespace EndlessClient.Dialogs.Factories
{
    [MappedType(BaseType = typeof(IGameLoadingDialogFactory))]
    public class GameLoadingDialogFactory : IGameLoadingDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public GameLoadingDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                        IGameStateProvider gameStateProvider,
                                        IClientWindowSizeProvider clientWindowSizeProvider,
                                        ILocalizedStringFinder localizedStringFinder,
                                        IConfigurationProvider configurationProvider,
                                        IMyraUIManager myraUIManager,
                                        IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _localizedStringFinder = localizedStringFinder;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IGameLoadingDialog CreateGameLoadingDialog()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraGameLoadingDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder);
            }

            return new GameLoadingDialog(_nativeGraphicsManager,
                _gameStateProvider,
                _clientWindowSizeProvider,
                _localizedStringFinder);
        }
    }

    public interface IGameLoadingDialogFactory
    {
        IGameLoadingDialog CreateGameLoadingDialog();
    }
}
