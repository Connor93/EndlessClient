using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Quest;
using EOLib.Graphics;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class QuestStatusDialogFactory : IQuestStatusDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public QuestStatusDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                 IEODialogButtonService dialogButtonService,
                                 ILocalizedStringFinder localizedStringFinder,
                                 IQuestDataProvider questDataProvider,
                                 ICharacterProvider characterProvider,
                                 IConfigurationProvider configurationProvider,
                                 IMyraUIManager myraUIManager,
                                 IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _localizedStringFinder = localizedStringFinder;
            _questDataProvider = questDataProvider;
            _characterProvider = characterProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraQuestStatusDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _questDataProvider,
                    _characterProvider);
            }

            return new QuestStatusDialog(_nativeGraphicsManager,
                                         _dialogButtonService,
                                         _localizedStringFinder,
                                         _questDataProvider,
                                         _characterProvider);
        }
    }

    public interface IQuestStatusDialogFactory
    {
        IXNADialog Create();
    }
}
