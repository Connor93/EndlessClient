using XNAControls;

namespace EndlessClient.Dialogs
{
    public enum ItemTransferType
    {
        DropItems,
        JunkItems,
        GiveItems,
        TradeItems,
        ShopTransfer,
        BankTransfer
    }

    public interface IItemTransferDialog : IXNADialog
    {
        int SelectedAmount { get; }
    }
}
