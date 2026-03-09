using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [MappedType(BaseType = typeof(ICreateAccountWarningDialogFactory))]
    public class CreateAccountWarningDialogFactory : ICreateAccountWarningDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IContentProvider _contentProvider;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public CreateAccountWarningDialogFactory(
            INativeGraphicsManager nativeGraphicsManager,
            IContentProvider contentProvider,
            IGameStateProvider gameStateProvider,
            IEODialogButtonService eoDialogButtonService,
            ISfxPlayer sfxPlayer,
            IConfigurationProvider configurationProvider,
            IMyraUIManager myraUIManager,
            IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _contentProvider = contentProvider;
            _gameStateProvider = gameStateProvider;
            _eoDialogButtonService = eoDialogButtonService;
            _sfxPlayer = sfxPlayer;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog ShowCreateAccountWarningDialog(string warningMessage)
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                var myraDialog = new MyraScrollingMessageDialog(
                    _myraUIManager,
                    _myraFontProvider)
                {
                    MessageText = warningMessage
                };
                myraDialog.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);
                return myraDialog;
            }

            var dialog = new ScrollingMessageDialog(_nativeGraphicsManager, _contentProvider, _gameStateProvider, _eoDialogButtonService)
            {
                MessageText = warningMessage
            };
            dialog.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            return dialog;
        }
    }

    public interface ICreateAccountWarningDialogFactory
    {
        IXNADialog ShowCreateAccountWarningDialog(string warningMessage);
    }
}
