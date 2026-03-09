using XNAControls;

namespace EndlessClient.Dialogs
{
    public interface IGameLoadingDialog : IXNADialog
    {
        void SetState(GameLoadingDialogState whichState);

        void CloseDialog();
    }
}
