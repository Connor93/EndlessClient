using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [MappedType(BaseType = typeof(ICreateAccountProgressDialogFactory))]
    public class CreateAccountProgressDialogFactory : ICreateAccountProgressDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IConfigurationProvider _configProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public CreateAccountProgressDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                                  IGameStateProvider gameStateProvider,
                                                  IConfigurationProvider configProvider,
                                                  ILocalizedStringFinder localizedStringFinder,
                                                  IEODialogButtonService eoDialogButtonService,
                                                  IMyraUIManager myraUIManager,
                                                  IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _gameStateProvider = gameStateProvider;
            _configProvider = configProvider;
            _localizedStringFinder = localizedStringFinder;
            _eoDialogButtonService = eoDialogButtonService;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog BuildCreateAccountProgressDialog()
        {
            var message = _localizedStringFinder.GetString(DialogResourceID.ACCOUNT_CREATE_ACCEPTED + 1);
            var caption = _localizedStringFinder.GetString(DialogResourceID.ACCOUNT_CREATE_ACCEPTED);

            if (_configProvider.UIMode != UIMode.Gfx)
            {
                return new MyraProgressDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _configProvider,
                    message,
                    caption);
            }

            return new ProgressDialog(_nativeGraphicsManager,
                                      _gameStateProvider,
                                      _configProvider,
                                      _eoDialogButtonService,
                                      message, caption);
        }
    }

    public interface ICreateAccountProgressDialogFactory
    {
        IXNADialog BuildCreateAccountProgressDialog();
    }
}
