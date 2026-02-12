using System.Collections.Generic;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public interface ITextMultiInputDialog : IXNADialog
    {
        IReadOnlyList<string> Responses { get; }
    }
}
