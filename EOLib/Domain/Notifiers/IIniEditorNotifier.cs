using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Notifiers
{
    public interface IIniEditorNotifier
    {
        void NotifyIniFileListReceived(IReadOnlyList<string> configFiles, IReadOnlyList<string> dataFiles);
        void NotifyIniFileContentReceived(int dirType, string filename, string content);
        void NotifyIniFileSaveResult(bool success, string message);
    }

    [AutoMappedType]
    public class NoOpIniEditorNotifier : IIniEditorNotifier
    {
        public void NotifyIniFileListReceived(IReadOnlyList<string> configFiles, IReadOnlyList<string> dataFiles) { }
        public void NotifyIniFileContentReceived(int dirType, string filename, string content) { }
        public void NotifyIniFileSaveResult(bool success, string message) { }
    }
}
