using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class SessionExpDialogFactory : ISessionExpDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ICharacterProvider _characterProvider;
        private readonly IExperienceTableProvider _expTableProvider;
        private readonly ICharacterSessionProvider _characterSessionProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public SessionExpDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                       IEODialogButtonService dialogButtonService,
                                       ILocalizedStringFinder localizedStringFinder,
                                       ICharacterProvider characterProvider,
                                       IExperienceTableProvider expTableProvider,
                                       ICharacterSessionProvider characterSessionProvider,
                                       IConfigurationProvider configurationProvider,
                                       IMyraUIManager myraUIManager,
                                       IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _localizedStringFinder = localizedStringFinder;
            _characterProvider = characterProvider;
            _expTableProvider = expTableProvider;
            _characterSessionProvider = characterSessionProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraSessionExpDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _characterProvider,
                    _expTableProvider,
                    _characterSessionProvider);
            }

            return new SessionExpDialog(_nativeGraphicsManager,
                                        _dialogButtonService,
                                        _localizedStringFinder,
                                        _characterProvider,
                                        _expTableProvider,
                                        _characterSessionProvider);
        }
    }

    public interface ISessionExpDialogFactory
    {
        IXNADialog Create();
    }
}
