using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering.Map;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based chest dialog — replaces CodeDrawnChestDialog.
    /// Shows items in a chest with click-to-take functionality.
    /// </summary>
    public class MyraChestDialog : MyraScrollingListDialog
    {
        private readonly IChestActions _chestActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IMapItemGraphicProvider _mapItemGraphicProvider;
        private readonly IChestDataProvider _chestDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICharacterProvider _characterProvider;

        private HashSet<ChestItem> _cachedItems;

        public MyraChestDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IChestActions chestActions,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            IMapItemGraphicProvider mapItemGraphicProvider,
            IChestDataProvider chestDataProvider,
            IEIFFileProvider eifFileProvider,
            ICharacterProvider characterProvider)
            : base(uiManager, fontProvider, "Chest", width: 320, height: 340)
        {
            _chestActions = chestActions;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _mapItemGraphicProvider = mapItemGraphicProvider;
            _chestDataProvider = chestDataProvider;
            _eifFileProvider = eifFileProvider;
            _characterProvider = characterProvider;

            _cachedItems = new HashSet<ChestItem>();

            // Single cancel button
            SetupButtons(showOk: false, showCancel: true);

            // Poll chest data on each render frame (MyraDialogAdapter.Update is a no-op)
            Window.BeforeRender += _ => PollChestData();

            _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION,
                EOResourceID.STATUS_LABEL_CHEST_YOU_OPENED,
                " " + _localizedStringFinder.GetString(EOResourceID.STATUS_LABEL_DRAG_AND_DROP_ITEMS));
        }

        /// <summary>
        /// Override Update to poll chest data. Called from the game loop
        /// via MyraDialogAdapter.Update (which is a no-op), but we need
        /// a different mechanism. Use Window.BeforeRender to poll.
        /// </summary>
        public void PollChestData()
        {
            if (!_cachedItems.SetEquals(_chestDataProvider.Items))
            {
                _cachedItems = _chestDataProvider.Items.ToHashSet();
                RefreshItemList();
            }
        }

        private void RefreshItemList()
        {
            ClearItems();

            foreach (var item in _cachedItems)
            {
                var itemData = _eifFileProvider.EIFFile[item.ItemID];
                var subText = $"x {item.Amount}  " +
                    $"{(itemData.Type == ItemType.Armor ? "(" + _localizedStringFinder.GetString(EOResourceID.FEMALE - itemData.Gender) + ")" : "")}";
                var itemIcon = _mapItemGraphicProvider.GetItemGraphic(item.ItemID, item.Amount);

                var chestItem = item;
                AddItem(itemData.Name, subText: subText, data: chestItem,
                    onClick: _ => TakeItem(chestItem, itemData),
                    isLink: true,
                    icon: itemIcon);
            }
        }

        private void TakeItem(ChestItem item, EOLib.IO.Pub.EIFRecord itemData)
        {
            if (!_inventorySpaceValidator.ItemFits(item.ItemID))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();

                _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_INFORMATION, EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT);
            }
            else if (itemData.Weight * item.Amount + _characterProvider.MainCharacter.Stats[CharacterStat.Weight] > _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_ITS_TOO_HEAVY_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
            }
            else
            {
                _chestActions.TakeItemFromChest(item.ItemID);
            }
        }
    }
}
