using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class BookDialogFactory : IBookDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IPaperdollProvider _paperdollProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public BookDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                 IEODialogButtonService eoDialogButtonService,
                                 IPubFileProvider pubFileProvider,
                                 IPaperdollProvider paperdollProvider,
                                 IConfigurationProvider configurationProvider,
                                 IMyraUIManager myraUIManager,
                                 IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _eoDialogButtonService = eoDialogButtonService;
            _pubFileProvider = pubFileProvider;
            _paperdollProvider = paperdollProvider;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create(Character character, bool isMainCharacter)
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                return new MyraBookDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _pubFileProvider,
                    _paperdollProvider,
                    character,
                    isMainCharacter);
            }

            return new BookDialog(_nativeGraphicsManager,
                _eoDialogButtonService,
                _pubFileProvider,
                _paperdollProvider,
                character,
                isMainCharacter);
        }
    }

    public interface IBookDialogFactory
    {
        IXNADialog Create(Character character, bool isMainCharacter);
    }
}
