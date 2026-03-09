using AutomaticTypeMapper;
using EndlessClient.Controllers;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class BardDialogFactory : IBardDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IBardController _bardController;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public BardDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                 IBardController bardController,
                                 IEODialogButtonService dialogButtonService,
                                 IConfigurationProvider configurationProvider,
                                 IMyraUIManager myraUIManager,
                                 IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _bardController = bardController;
            _dialogButtonService = dialogButtonService;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraBardDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _bardController);
            }

            return new BardDialog(_nativeGraphicsManager,
                                  _bardController,
                                  _dialogButtonService);
        }
    }

    public interface IBardDialogFactory
    {
        IXNADialog Create();
    }
}
