using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Interact.Law;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class LawDialogFactory : ILawDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IEODialogIconService _dialogIconService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ILawActions _lawActions;
        private readonly IContentProvider _contentProvider;
        private readonly ICurrentMapStateProvider _currentMapStateProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public LawDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                IEODialogButtonService dialogButtonService,
                                IEODialogIconService dialogIconService,
                                ILocalizedStringFinder localizedStringFinder,
                                ITextInputDialogFactory textInputDialogFactory,
                                ILawActions lawActions,
                                IContentProvider contentProvider,
                                ICurrentMapStateProvider currentMapStateProvider,
                                IENFFileProvider enfFileProvider,
                                IConfigurationProvider configurationProvider,
                                IMyraUIManager myraUIManager,
                                IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _dialogIconService = dialogIconService;
            _localizedStringFinder = localizedStringFinder;
            _textInputDialogFactory = textInputDialogFactory;
            _lawActions = lawActions;
            _contentProvider = contentProvider;
            _currentMapStateProvider = currentMapStateProvider;
            _enfFileProvider = enfFileProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraLawDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _textInputDialogFactory,
                    _lawActions,
                    _currentMapStateProvider,
                    _enfFileProvider);
            }

            return new LawDialog(_nativeGraphicsManager,
                                 _dialogButtonService,
                                 _dialogIconService,
                                 _localizedStringFinder,
                                 _textInputDialogFactory,
                                 _lawActions,
                                 _contentProvider,
                                 _currentMapStateProvider,
                                 _enfFileProvider);
        }
    }

    public interface ILawDialogFactory
    {
        IXNADialog Create();
    }
}
