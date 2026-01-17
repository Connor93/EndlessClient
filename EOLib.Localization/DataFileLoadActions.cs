using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AutomaticTypeMapper;
using EOLib.Shared;

namespace EOLib.Localization
{
    [MappedType(BaseType = typeof(IDataFileLoadActions))]
    public class DataFileLoadActions : IDataFileLoadActions
    {
        private readonly IDataFileRepository _dataFileRepository;
        private readonly IEDFLoaderService _edfLoaderService;

        public DataFileLoadActions(IDataFileRepository dataFileRepository,
                                   IEDFLoaderService edfLoaderService)
        {
            _dataFileRepository = dataFileRepository;
            _edfLoaderService = edfLoaderService;
        }

        public void LoadDataFiles()
        {
            if (!Directory.Exists(Constants.DataFilePath))
                throw new DataFileLoadException($"Data directory not found: {Constants.DataFilePath}");

            var files = Directory.GetFiles(Constants.DataFilePath, "*.edf")
                                 .OrderBy(x => x)
                                 .ToArray();
            if (files.Length != Constants.ExpectedNumberOfDataFiles)
                throw new DataFileLoadException($"Invalid number of data files! Found {files.Length}, expected {Constants.ExpectedNumberOfDataFiles}");

            _dataFileRepository.DataFiles.Clear();
            for (int i = 1; i <= Constants.ExpectedNumberOfDataFiles; ++i)
            {
                if (!DataFileNameIsValid(i, files[i - 1]))
                    throw new DataFileLoadException($"Invalid data file name! Expected {string.Format(Constants.DataFileFormat, i)}, found {files[i - 1]}");

                var fileToLoad = (DataFiles)i;
                _dataFileRepository.DataFiles[fileToLoad] = _edfLoaderService.LoadFile(files[i - 1], fileToLoad);
            }
        }

        private bool DataFileNameIsValid(int fileNumber, string fileName)
        {
            var expectedFormat = string.Format(Constants.DataFileFormat, fileNumber).Replace('/', Path.DirectorySeparatorChar);
            return expectedFormat == fileName;
        }
    }

    public interface IDataFileLoadActions
    {
        void LoadDataFiles();
    }
}
