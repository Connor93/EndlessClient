using System.IO;
using AutomaticTypeMapper;
using EOLib.Config;
using EOLib.Graphics;
using PELoaderLib;

namespace EndlessClient.Initialization
{
    [MappedType(BaseType = typeof(IGameInitializer))]
    public class GraphicsInitializer : IGameInitializer
    {
        private readonly IPEFileCollection _peFileCollection;
        private readonly IThemeProvider _themeProvider;
        private readonly IConfigurationProvider _configProvider;

        public GraphicsInitializer(IPEFileCollection peFileCollection,
                                   IThemeProvider themeProvider,
                                   IConfigurationProvider configProvider)
        {
            _peFileCollection = peFileCollection;
            _themeProvider = themeProvider;
            _configProvider = configProvider;
        }

        public void Initialize()
        {
            // Set up theme before loading GFX files
            if (!string.IsNullOrEmpty(_configProvider.Theme))
            {
                _themeProvider.SetActiveTheme(_configProvider.Theme);
            }

            _peFileCollection.SetThemeProvider(_themeProvider);
            _peFileCollection.PopulateCollectionWithStandardGFX();

            foreach (var filePair in _peFileCollection)
                TryInitializePEFiles(filePair.Key, filePair.Value);
        }

        private static void TryInitializePEFiles(GFXTypes file, IPEFile peFile)
        {
            var number = ((int)file).ToString("D3");

            try
            {
                peFile.Initialize();
            }
            catch (IOException)
            {
                throw new LibraryLoadException(number, file);
            }

            if (!peFile.Initialized)
                throw new LibraryLoadException(number, file);
        }
    }
}

