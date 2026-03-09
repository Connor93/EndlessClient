using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.HUD.Chat;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Shared;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class TextMultiInputDialogFactory : ITextMultiInputDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IChatTextBoxActions _chatTextBoxActions;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IContentProvider _contentProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigurationProvider _configProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public TextMultiInputDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                           IChatTextBoxActions chatTextBoxActions,
                                           IEODialogButtonService eoDialogButtonService,
                                           IContentProvider contentProvider,
                                           ISfxPlayer sfxPlayer,
                                           IConfigurationProvider configProvider,
                                           IMyraUIManager myraUIManager,
                                           IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _chatTextBoxActions = chatTextBoxActions;
            _eoDialogButtonService = eoDialogButtonService;
            _contentProvider = contentProvider;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public ITextMultiInputDialog Create(string title, string prompt, TextMultiInputDialog.DialogSize size, params TextMultiInputDialog.InputInfo[] inputInfo)
        {
            ITextMultiInputDialog dlg;

            if (_configProvider.UIMode != UIMode.Gfx)
            {
                dlg = new MyraTextMultiInputDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    title,
                    prompt,
                    inputInfo);
            }
            else
            {
                dlg = new TextMultiInputDialog(_nativeGraphicsManager,
                    _chatTextBoxActions,
                    _eoDialogButtonService,
                    _contentProvider,
                    size,
                    title,
                    prompt,
                    inputInfo);
            }

            dlg.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);
            return dlg;
        }
    }

    public interface ITextMultiInputDialogFactory
    {
        ITextMultiInputDialog Create(string title,
                                    string prompt,
                                    TextMultiInputDialog.DialogSize size,
                                    params TextMultiInputDialog.InputInfo[] inputInfo);
    }
}

