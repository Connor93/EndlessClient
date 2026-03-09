using EOLib.Domain.Account;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public interface IChangePasswordDialog : IXNADialog
    {
        IChangePasswordParameters Result { get; }
    }
}
