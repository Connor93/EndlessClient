using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Notifiers;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Actions;

namespace EndlessClient.HUD
{
    /// <summary>
    /// Handles INI editor notifications and forwards them to the dialog.
    /// </summary>
    [AutoMappedType]
    public class IniEditorNotifier : IIniEditorNotifier
    {
        private readonly IInGameDialogActions _dialogActions;
        private readonly IActiveDialogRepository _activeDialogRepository;

        public IniEditorNotifier(IInGameDialogActions dialogActions,
                                 IActiveDialogRepository activeDialogRepository)
        {
            _dialogActions = dialogActions;
            _activeDialogRepository = activeDialogRepository;
        }

        public void NotifyIniFileListReceived(IReadOnlyList<string> configFiles, IReadOnlyList<string> dataFiles)
        {
            // Show the INI editor dialog when we receive the file list from the server
            _dialogActions.ShowIniEditorDialog();
        }

        public void NotifyIniFileContentReceived(int dirType, string filename, string content)
        {
            // Forward to the dialog if it's open
            _activeDialogRepository.IniEditorDialog.MatchSome(dialog =>
            {
                dialog.NotifyIniFileContentReceived(dirType, filename, content);
            });
        }

        public void NotifyIniFileSaveResult(bool success, string message)
        {
            // Forward to the dialog if it's open
            _activeDialogRepository.IniEditorDialog.MatchSome(dialog =>
            {
                dialog.NotifyIniFileSaveResult(success, message);
            });
        }
    }
}
