using XNAControls;

namespace EndlessClient.Dialogs
{
    public interface ITextInputDialog : IXNADialog
    {
        string ResponseText { get; }
    }
}
