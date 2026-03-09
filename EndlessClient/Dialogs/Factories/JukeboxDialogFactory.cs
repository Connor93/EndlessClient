using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Jukebox;
using EOLib.Graphics;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class JukeboxDialogFactory : IJukeboxDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IEODialogIconService _dialogIconService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IDataFileProvider _dataFileProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IJukeboxActions _jukeboxActions;
        private readonly IJukeboxRepository _jukeboxRepository;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public JukeboxDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                    IEODialogButtonService dialogButtonService,
                                    IEODialogIconService dialogIconService,
                                    ILocalizedStringFinder localizedStringFinder,
                                    IDataFileProvider dataFileProvider,
                                    IEOMessageBoxFactory messageBoxFactory,
                                    IJukeboxActions jukeboxActions,
                                    IJukeboxRepository jukeboxRepository,
                                    ICharacterInventoryProvider characterInventoryProvider,
                                    ISfxPlayer sfxPlayer,
                                    IConfigurationProvider configurationProvider,
                                    IMyraUIManager myraUIManager,
                                    IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _dialogIconService = dialogIconService;
            _localizedStringFinder = localizedStringFinder;
            _dataFileProvider = dataFileProvider;
            _messageBoxFactory = messageBoxFactory;
            _jukeboxActions = jukeboxActions;
            _jukeboxRepository = jukeboxRepository;
            _characterInventoryProvider = characterInventoryProvider;
            _sfxPlayer = sfxPlayer;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraJukeboxDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _dataFileProvider,
                    _messageBoxFactory,
                    _jukeboxActions,
                    _jukeboxRepository,
                    _characterInventoryProvider,
                    _sfxPlayer);
            }

            return new JukeboxDialog(_nativeGraphicsManager,
                                     _dialogButtonService,
                                     _dialogIconService,
                                     _localizedStringFinder,
                                     _dataFileProvider,
                                     _messageBoxFactory,
                                     _jukeboxActions,
                                     _jukeboxRepository,
                                     _characterInventoryProvider,
                                     _sfxPlayer);
        }
    }

    public interface IJukeboxDialogFactory
    {
        IXNADialog Create();
    }
}
