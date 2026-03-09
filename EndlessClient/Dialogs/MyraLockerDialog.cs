using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based locker dialog — replaces CodeDrawnLockerDialog.
    /// Shows items in a locker with click-to-take functionality.
    /// </summary>
    public class MyraLockerDialog : MyraScrollingListDialog
    {
        private readonly ILockerActions _lockerActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataProvider _lockerDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;

        private HashSet<InventoryItem> _cachedItems;

        public MyraLockerDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILockerActions lockerActions,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            ICharacterProvider characterProvider,
            ILockerDataProvider lockerDataProvider,
            IEIFFileProvider eifFileProvider)
            : base(uiManager, fontProvider, GetDialogTitle(lockerDataProvider, characterProvider, localizedStringFinder), width: 320, height: 340)
        {
            _lockerActions = lockerActions;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _characterProvider = characterProvider;
            _lockerDataProvider = lockerDataProvider;
            _eifFileProvider = eifFileProvider;

            _cachedItems = new HashSet<InventoryItem>();

            SetupButtons(showOk: false, showCancel: true);

            // Poll locker data on each render frame
            Window.BeforeRender += _ => PollLockerData();
        }

        private void PollLockerData()
        {
            if (!_cachedItems.SetEquals(_lockerDataProvider.Items))
            {
                _cachedItems = _lockerDataProvider.Items.ToHashSet();
                Window.Title = GetDialogTitle(_lockerDataProvider, _characterProvider, _localizedStringFinder);
                RefreshItemList();
            }
        }

        private void RefreshItemList()
        {
            ClearItems();

            foreach (var item in _cachedItems)
            {
                var itemData = _eifFileProvider.EIFFile[item.ItemID];
                var subText = $"x{item.Amount}  {(itemData.Type == ItemType.Armor ? $"({_localizedStringFinder.GetString(EOResourceID.FEMALE - itemData.Gender)})" : string.Empty)}";

                var lockerItem = item;
                AddItem(itemData.Name, subText: subText, data: lockerItem,
                    onClick: _ => TakeItem(itemData, lockerItem),
                    isLink: true);
            }
        }

        private void TakeItem(EOLib.IO.Pub.EIFRecord itemData, InventoryItem item)
        {
            if (!_inventorySpaceValidator.ItemFits(item.ItemID))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();

                _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_INFORMATION, EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT);
            }
            else if (_characterProvider.MainCharacter.Stats[CharacterStat.Weight] >= _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_ITS_TOO_HEAVY_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
            }
            else
            {
                _lockerActions.TakeItemFromLocker(item.ItemID);
            }
        }

        private static string GetDialogTitle(ILockerDataProvider lockerDataProvider, ICharacterProvider characterProvider, ILocalizedStringFinder localizedStringFinder)
        {
            var count = $" [{lockerDataProvider.Items.Count}]";
            return lockerDataProvider.Context switch
            {
                LockerContext.GuildStorage => "Guild Storage" + count,
                LockerContext.DeliveryInbox => "Personal Inbox" + count,
                _ => characterProvider.MainCharacter.Name + "'s " + localizedStringFinder.GetString(EOResourceID.DIALOG_TITLE_PRIVATE_LOCKER) + count,
            };
        }
    }
}
