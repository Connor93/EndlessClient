using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework.Graphics;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based grid locker dialog — displays stored items in a tile grid with category tabs.
    /// </summary>
    public class MyraGridLockerDialog : MyraGridDialog
    {
        private enum ItemCategory { All, Equip, Use, Misc }

        private readonly INativeGraphicsManager _graphicsManager;
        private readonly ILockerActions _lockerActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataProvider _lockerDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;

        private HashSet<InventoryItem> _cachedItems;

        public MyraGridLockerDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            INativeGraphicsManager graphicsManager,
            ILockerActions lockerActions,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            IStatusLabelSetter statusLabelSetter,
            IEOMessageBoxFactory messageBoxFactory,
            ICharacterProvider characterProvider,
            ILockerDataProvider lockerDataProvider,
            IEIFFileProvider eifFileProvider)
            : base(uiManager, fontProvider, GetTitle(lockerDataProvider, characterProvider, localizedStringFinder))
        {
            _graphicsManager = graphicsManager;
            _lockerActions = lockerActions;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _characterProvider = characterProvider;
            _lockerDataProvider = lockerDataProvider;
            _eifFileProvider = eifFileProvider;

            _cachedItems = new HashSet<InventoryItem>();

            // Add category tabs
            AddTab("All");
            AddTab("Equip");
            AddTab("Use");
            AddTab("Misc");

            // Add close button
            AddButton("Close", () => Close(XNADialogResult.Cancel));
        }

        private static string GetTitle(ILockerDataProvider lockerDataProvider, ICharacterProvider characterProvider, ILocalizedStringFinder localizedStringFinder)
        {
            var count = $" [{lockerDataProvider.Items.Count}]";
            return lockerDataProvider.Context switch
            {
                LockerContext.GuildStorage => "Guild Storage" + count,
                LockerContext.DeliveryInbox => "Personal Inbox" + count,
                _ => characterProvider.MainCharacter.Name + "'s " +
                     localizedStringFinder.GetString(EOResourceID.DIALOG_TITLE_PRIVATE_LOCKER) + count,
            };
        }

        protected override void PollData()
        {
            if (!_cachedItems.SetEquals(_lockerDataProvider.Items))
            {
                _cachedItems = _lockerDataProvider.Items.ToHashSet();

                // Update title with current item count
                Window.Title = GetTitle(_lockerDataProvider, _characterProvider, _localizedStringFinder);

                RefreshGrid();
            }
        }

        protected override IReadOnlyList<GridTileData> GetTileData()
        {
            var filteredItems = GetFilteredItems().ToList();
            var tiles = new List<GridTileData>();

            foreach (var item in filteredItems)
            {
                var itemData = _eifFileProvider.EIFFile[item.ItemID];

                Texture2D iconTexture = null;
                try
                {
                    iconTexture = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * itemData.Graphic - 1, transparent: true);
                }
                catch { /* gracefully handle missing graphics */ }

                var tooltipLines = new List<string> { itemData.Name };
                tooltipLines.Add($"Type: {itemData.Type}");
                if (item.Amount > 1) tooltipLines.Add($"Amount: {item.Amount}");
                if (itemData.Weight > 0) tooltipLines.Add($"Weight: {itemData.Weight}");

                // Equipment stats
                if (GetCategory(itemData.Type) == ItemCategory.Equip)
                {
                    if (itemData.MinDam > 0 || itemData.MaxDam > 0)
                        tooltipLines.Add($"Damage: {itemData.MinDam}-{itemData.MaxDam}");
                    if (itemData.Accuracy > 0) tooltipLines.Add($"Accuracy: {itemData.Accuracy}");
                    if (itemData.Evade > 0) tooltipLines.Add($"Evade: {itemData.Evade}");
                    if (itemData.Armor > 0) tooltipLines.Add($"Armor: {itemData.Armor}");
                    if (itemData.HP > 0) tooltipLines.Add($"HP: +{itemData.HP}");
                    if (itemData.TP > 0) tooltipLines.Add($"TP: +{itemData.TP}");

                    var stats = new List<string>();
                    if (itemData.Str > 0) stats.Add($"Str+{itemData.Str}");
                    if (itemData.Int > 0) stats.Add($"Int+{itemData.Int}");
                    if (itemData.Wis > 0) stats.Add($"Wis+{itemData.Wis}");
                    if (itemData.Agi > 0) stats.Add($"Agi+{itemData.Agi}");
                    if (itemData.Con > 0) stats.Add($"Con+{itemData.Con}");
                    if (itemData.Cha > 0) stats.Add($"Cha+{itemData.Cha}");
                    if (stats.Count > 0) tooltipLines.Add(string.Join(" ", stats));

                    if (itemData.LevelReq > 0) tooltipLines.Add($"Lvl Req: {itemData.LevelReq}");
                }

                // Consumable stats
                if (itemData.Type == ItemType.Heal && itemData.HP > 0)
                    tooltipLines.Add($"Heals: {itemData.HP} HP");
                if (itemData.Type == ItemType.Heal && itemData.TP > 0)
                    tooltipLines.Add($"Restores: {itemData.TP} TP");

                tiles.Add(new GridTileData
                {
                    ItemID = item.ItemID,
                    Name = itemData.Name,
                    Amount = item.Amount,
                    IconTexture = iconTexture,
                    Tag = item,
                    TooltipText = string.Join("\n", tooltipLines)
                });
            }

            return tiles;
        }

        protected override void OnTileClicked(GridTileData tileData)
        {
            if (tileData.Tag is InventoryItem item)
            {
                var itemData = _eifFileProvider.EIFFile[item.ItemID];
                TakeItem(itemData, item);
            }
        }

        private IEnumerable<InventoryItem> GetFilteredItems()
        {
            var items = _cachedItems.AsEnumerable();

            var category = ActiveTabIndex switch
            {
                1 => ItemCategory.Equip,
                2 => ItemCategory.Use,
                3 => ItemCategory.Misc,
                _ => ItemCategory.All
            };

            if (category != ItemCategory.All)
                items = items.Where(i => GetCategory(_eifFileProvider.EIFFile[i.ItemID].Type) == category);

            return items;
        }

        private static ItemCategory GetCategory(ItemType type) => type switch
        {
            ItemType.Weapon or ItemType.Shield or ItemType.Armor or
            ItemType.Hat or ItemType.Boots or ItemType.Gloves or
            ItemType.Accessory or ItemType.Belt or ItemType.Necklace or
            ItemType.Ring or ItemType.Armlet or ItemType.Bracer => ItemCategory.Equip,

            ItemType.Heal or ItemType.Teleport or ItemType.Spell or
            ItemType.EXPReward or ItemType.StatReward or ItemType.SkillReward or
            ItemType.Beer or ItemType.EffectPotion or ItemType.HairDye or
            ItemType.CureCurse => ItemCategory.Use,

            _ => ItemCategory.Misc
        };

        private void TakeItem(EOLib.IO.Pub.EIFRecord itemData, InventoryItem item)
        {
            if (!_inventorySpaceValidator.ItemFits(item.ItemID))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(
                    EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT,
                    EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
                _statusLabelSetter.SetStatusLabel(
                    EOResourceID.STATUS_LABEL_TYPE_INFORMATION,
                    EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT);
            }
            else if (_characterProvider.MainCharacter.Stats[CharacterStat.Weight] >=
                     _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(
                    EOResourceID.DIALOG_ITS_TOO_HEAVY_WEIGHT,
                    EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
            }
            else
            {
                _lockerActions.TakeItemFromLocker(item.ItemID);
            }
        }
    }
}
