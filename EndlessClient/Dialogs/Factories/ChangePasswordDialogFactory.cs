using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Input;
using EndlessClient.UI.Myra;
using EndlessClient.UIControls;
using EOLib.Config;
using EOLib.Domain.Login;
using EOLib.Graphics;
using EOLib.Localization;

namespace EndlessClient.Dialogs.Factories
{
    [MappedType(BaseType = typeof(IChangePasswordDialogFactory))]
    public class ChangePasswordDialogFactory : IChangePasswordDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IXnaControlSoundMapper _xnaControlSoundMapper;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public ChangePasswordDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                           IGameStateProvider gameStateProvider,
                                           IContentProvider contentProvider,
                                           IEOMessageBoxFactory eoMessageBoxFactory,
                                           IPlayerInfoProvider playerInfoProvider,
                                           IEODialogButtonService eoDialogButtonService,
                                           IXnaControlSoundMapper xnaControlSoundMapper,
                                           ILocalizedStringFinder localizedStringFinder,
                                           IConfigurationProvider configurationProvider,
                                           IMyraUIManager myraUIManager,
                                           IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _gameStateProvider = gameStateProvider;
            _contentProvider = contentProvider;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _playerInfoProvider = playerInfoProvider;
            _eoDialogButtonService = eoDialogButtonService;
            _xnaControlSoundMapper = xnaControlSoundMapper;
            _localizedStringFinder = localizedStringFinder;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IChangePasswordDialog BuildChangePasswordDialog()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraChangePasswordDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _eoMessageBoxFactory,
                    _playerInfoProvider);
            }

            return new ChangePasswordDialog(_nativeGraphicsManager,
                                            _gameStateProvider,
                                            _contentProvider,
                                            _eoMessageBoxFactory,
                                            _playerInfoProvider,
                                            _eoDialogButtonService,
                                            _xnaControlSoundMapper);
        }
    }
}
