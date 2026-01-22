using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering.Map;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of ChestDialog with procedural rendering.
    /// </summary>
    public class CodeDrawnChestDialog : CodeDrawnScrollingListDialog
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
        private readonly IContentProvider _contentProvider;

        private HashSet<ChestItem> _cachedItems;
        private CodeDrawnButton _cancelButton;

        public CodeDrawnChestDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IChestActions chestActions,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            IMapItemGraphicProvider mapItemGraphicProvider,
            IChestDataProvider chestDataProvider,
            IEIFFileProvider eifFileProvider,
            ICharacterProvider characterProvider,
            IContentProvider contentProvider)
            : base(styleProvider, gameStateProvider, contentProvider.Fonts[Constants.FontSize08pt5])
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
            _contentProvider = contentProvider;

            _cachedItems = new HashSet<ChestItem>();

            // Configure for chest dialog size
            DialogWidth = 320;
            DialogHeight = 340;
            ListAreaTop = 45;
            ListAreaHeight = 240;
            ItemHeight = 36;

            UpdateScrollBarLayout();

            Title = "Chest";

            CreateButtons(styleProvider);
            CenterInGameView();

            _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION,
                EOResourceID.STATUS_LABEL_CHEST_YOU_OPENED,
                " " + _localizedStringFinder.GetString(EOResourceID.STATUS_LABEL_DRAG_AND_DROP_ITEMS));
        }

        private void CreateButtons(IUIStyleProvider styleProvider)
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var buttonWidth = 72;
            var buttonHeight = 26;
            var buttonY = DialogHeight - 36;

            _cancelButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle((DialogWidth - buttonWidth) / 2, buttonY, buttonWidth, buttonHeight),
                Visible = true
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);
        }

        public override void Initialize()
        {
            _cancelButton?.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (!_cachedItems.SetEquals(_chestDataProvider.Items))
            {
                _cachedItems = _chestDataProvider.Items.ToHashSet();
                RefreshItemList();
            }

            base.OnUpdateControl(gameTime);
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
