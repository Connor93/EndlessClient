using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Interact.Citizen;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class InnkeeperDialogFactory : IInnkeeperDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IEODialogIconService _dialogIconService;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ICitizenActions _citizenActions;
        private readonly IContentProvider _contentProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly ICitizenDataProvider _citizenDataProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public InnkeeperDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                               IEODialogButtonService dialogButtonService,
                               IEODialogIconService dialogIconService,
                               ILocalizedStringFinder localizedStringFinder,
                               IEOMessageBoxFactory messageBoxFactory,
                               ITextInputDialogFactory textInputDialogFactory,
                               ICitizenActions citizenActions,
                               IContentProvider contentProvider,
                               IENFFileProvider enfFileProvider,
                               ICitizenDataProvider citizenDataProvider,
                               IConfigurationProvider configurationProvider,
                               IMyraUIManager myraUIManager,
                               IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _dialogIconService = dialogIconService;
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _textInputDialogFactory = textInputDialogFactory;
            _citizenActions = citizenActions;
            _contentProvider = contentProvider;
            _enfFileProvider = enfFileProvider;
            _citizenDataProvider = citizenDataProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraInnkeeperDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder,
                    _messageBoxFactory,
                    _textInputDialogFactory,
                    _citizenActions,
                    _citizenDataProvider,
                    _enfFileProvider);
            }

            return new InnkeeperDialog(_nativeGraphicsManager,
                _dialogButtonService,
                _dialogIconService,
                _localizedStringFinder,
                _messageBoxFactory,
                _textInputDialogFactory,
                _citizenActions,
                _contentProvider,
                _enfFileProvider,
                _citizenDataProvider);
        }
    }

    public interface IInnkeeperDialogFactory
    {
        IXNADialog Create();
    }
}
