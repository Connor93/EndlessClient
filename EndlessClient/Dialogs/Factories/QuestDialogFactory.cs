using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Interact.Quest;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class QuestDialogFactory : IQuestDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IQuestActions _questActions;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IContentProvider _contentProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUIStyleProviderFactory _styleProviderFactory;
        private readonly IGameStateProvider _gameStateProvider;

        public QuestDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                  IQuestActions questActions,
                                  IEODialogButtonService dialogButtonService,
                                  IQuestDataProvider questDataProvider,
                                  IENFFileProvider enfFileProvider,
                                  IContentProvider contentProvider,
                                  ILocalizedStringFinder localizedStringFinder,
                                  IConfigurationProvider configProvider,
                                  IUIStyleProviderFactory styleProviderFactory,
                                  IGameStateProvider gameStateProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _questActions = questActions;
            _dialogButtonService = dialogButtonService;
            _questDataProvider = questDataProvider;
            _enfFileProvider = enfFileProvider;
            _contentProvider = contentProvider;
            _localizedStringFinder = localizedStringFinder;
            _configProvider = configProvider;
            _styleProviderFactory = styleProviderFactory;
            _gameStateProvider = gameStateProvider;
        }

        public IXNADialog Create()
        {
            if (_configProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnQuestDialog(_styleProviderFactory.Create(),
                                                _gameStateProvider,
                                                _questActions,
                                                _questDataProvider,
                                                _enfFileProvider,
                                                _contentProvider,
                                                _localizedStringFinder);
            }

            return new QuestDialog(_nativeGraphicsManager,
                                   _questActions,
                                   _dialogButtonService,
                                   _questDataProvider,
                                   _enfFileProvider,
                                   _contentProvider,
                                   _localizedStringFinder);
        }
    }

    public interface IQuestDialogFactory
    {
        IXNADialog Create();
    }
}

