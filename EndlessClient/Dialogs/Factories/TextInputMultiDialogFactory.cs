using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
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
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        public TextMultiInputDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                           IChatTextBoxActions chatTextBoxActions,
                                           IEODialogButtonService eoDialogButtonService,
                                           IContentProvider contentProvider,
                                           ISfxPlayer sfxPlayer,
                                           IConfigurationProvider configProvider,
                                           IUIStyleProviderFactory styleProviderFactory,
                                           IGameStateProvider gameStateProvider,
                                           IClientWindowSizeProvider clientWindowSizeProvider,
                                           IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _chatTextBoxActions = chatTextBoxActions;
            _eoDialogButtonService = eoDialogButtonService;
            _contentProvider = contentProvider;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        public ITextMultiInputDialog Create(string title, string prompt, TextMultiInputDialog.DialogSize size, params TextMultiInputDialog.InputInfo[] inputInfo)
        {
            ITextMultiInputDialog dlg;

            if (_configProvider.UIMode == UIMode.Code)
            {
                dlg = new CodeDrawnTextMultiInputDialog(
                    _styleProviderFactory.Create(),
                    _gameStateProvider,
                    _clientWindowSizeProvider,
                    _graphicsDeviceProvider,
                    _contentProvider,
                    _chatTextBoxActions,
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
