using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.IniEditor
{
    public interface IIniEditorRepository
    {
        List<string> ConfigFiles { get; set; }
        List<string> DataFiles { get; set; }
        string CurrentFilename { get; set; }
        int CurrentDirType { get; set; }  // 0 = config, 1 = data
        string CurrentContent { get; set; }
    }

    public interface IIniEditorProvider
    {
        IReadOnlyList<string> ConfigFiles { get; }
        IReadOnlyList<string> DataFiles { get; }
        string CurrentFilename { get; }
        int CurrentDirType { get; }
        string CurrentContent { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class IniEditorRepository : IIniEditorRepository, IIniEditorProvider
    {
        public List<string> ConfigFiles { get; set; } = new List<string>();
        public List<string> DataFiles { get; set; } = new List<string>();
        public string CurrentFilename { get; set; } = string.Empty;
        public int CurrentDirType { get; set; }
        public string CurrentContent { get; set; } = string.Empty;

        IReadOnlyList<string> IIniEditorProvider.ConfigFiles => ConfigFiles;
        IReadOnlyList<string> IIniEditorProvider.DataFiles => DataFiles;
    }
}
