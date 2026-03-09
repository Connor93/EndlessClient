using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD.Inventory;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Shop;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework.Graphics;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based shop dialog — Buy/Sell/Craft tabs using the MyraGridDialog base.
    /// </summary>
    public class MyraShopDialog : MyraGridDialog
    {
        private enum ShopTab { Buy, Sell, Craft }

        private readonly INativeGraphicsManager _graphicsManager;
        private readonly IShopActions _shopActions;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IShopDataProvider _shopDataProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;

        private IReadOnlyList<IShopItem> _buyItems, _sellItems;
        private IReadOnlyList<IShopCraftItem> _craftItems;
        private Option<int> _cachedShopId;
        private HashSet<InventoryItem> _cachedInventory;
        private ulong _tick;
        private bool _hasCraftTab;

        public MyraShopDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            INativeGraphicsManager graphicsManager,
            IShopActions shopActions,
            IEOMessageBoxFactory messageBoxFactory,
            IItemTransferDialogFactory itemTransferDialogFactory,
            ILocalizedStringFinder localizedStringFinder,
            IShopDataProvider shopDataProvider,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider,
            ICharacterProvider characterProvider,
            IInventorySpaceValidator inventorySpaceValidator)
            : base(uiManager, fontProvider, "Shop")
        {
            _graphicsManager = graphicsManager;
            _shopActions = shopActions;
            _messageBoxFactory = messageBoxFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _localizedStringFinder = localizedStringFinder;
            _shopDataProvider = shopDataProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _characterProvider = characterProvider;
            _inventorySpaceValidator = inventorySpaceValidator;

            _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);
            _buyItems = new List<IShopItem>();
            _sellItems = new List<IShopItem>();
            _craftItems = new List<IShopCraftItem>();

            // Add tabs — Craft tab added later if shop has craft items
            AddTab("Buy");
            AddTab("Sell");

            // Close button
            AddButton("Close", () => Close(XNADialogResult.Cancel));
        }

        protected override void PollData()
        {
            _cachedShopId.MatchNone(() =>
            {
                _shopDataProvider.SessionID.SomeWhen(x => x > 0)
                    .MatchSome(_ =>
                    {
                        _cachedShopId = Option.Some(_shopDataProvider.SessionID);

                        // Update title
                        var shopName = _shopDataProvider.ShopName;
                        Window.Title = string.IsNullOrEmpty(shopName) ? "Shop" : shopName;

                        _buyItems = _shopDataProvider.TradeItems.Where(x => x.Buy > 0).ToList();
                        _sellItems = _shopDataProvider.TradeItems
                            .Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0))
                            .ToList();
                        _craftItems = _shopDataProvider.CraftItems;

                        // Add craft tab if needed
                        if (_craftItems.Count > 0 && !_hasCraftTab)
                        {
                            AddTab("Craft");
                            _hasCraftTab = true;
                        }

                        // Default to Buy tab
                        ActiveTabIndex = _buyItems.Count > 0 ? 0 : 1;
                        RefreshGrid();
                    });
            });

            // Periodically refresh sell items when inventory changes
            if (++_tick % 8 == 0 && !_cachedInventory.SetEquals(_characterInventoryProvider.ItemInventory))
            {
                _sellItems = _shopDataProvider.TradeItems
                    .Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0))
                    .ToList();
                _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);

                if (ActiveTabIndex == 1) // Sell tab
                    RefreshGrid();
            }
        }

        protected override IReadOnlyList<GridTileData> GetTileData()
        {
            var activeTab = GetShopTab();
            var tiles = new List<GridTileData>();

            switch (activeTab)
            {
                case ShopTab.Buy:
                    foreach (var item in _buyItems)
                    {
                        var data = _eifFileProvider.EIFFile[item.ID];
                        var tooltip = BuildItemTooltip(data);
                        tooltip.Insert(1, $"Buy Price: {item.Buy:N0} gold");
                        tiles.Add(CreateShopTile(item.ID, data.Name, data.Graphic, FormatGold(item.Buy), item,
                            string.Join("\n", tooltip)));
                    }
                    break;

                case ShopTab.Sell:
                    foreach (var item in _sellItems)
                    {
                        var data = _eifFileProvider.EIFFile[item.ID];
                        var invItem = _characterInventoryProvider.ItemInventory.FirstOrDefault(x => x.ItemID == item.ID);
                        var tooltip = BuildItemTooltip(data);
                        tooltip.Insert(1, $"Sell Price: {item.Sell:N0} gold");
                        tooltip.Add($"You have: {invItem.Amount}");
                        tiles.Add(CreateShopTile(item.ID, data.Name, data.Graphic, FormatGold(item.Sell), item,
                            string.Join("\n", tooltip)));
                    }
                    break;

                case ShopTab.Craft:
                    foreach (var item in _craftItems)
                    {
                        var data = _eifFileProvider.EIFFile[item.ID];
                        var tooltip = BuildItemTooltip(data);
                        tooltip.Add("");
                        tooltip.Add("-- Ingredients --");
                        foreach (var ingr in item.Ingredients)
                        {
                            var ingrData = _eifFileProvider.EIFFile[ingr.ID];
                            var owned = _characterInventoryProvider.ItemInventory
                                .Where(x => x.ItemID == ingr.ID)
                                .Select(x => x.Amount)
                                .FirstOrDefault();
                            var have = owned >= ingr.Amount ? "OK" : $"need {ingr.Amount - owned} more";
                            tooltip.Add($"  {ingrData.Name} x{ingr.Amount} ({have})");
                        }
                        tiles.Add(CreateShopTile(item.ID, data.Name, data.Graphic, $"{item.Ingredients.Count} ingr.", item,
                            string.Join("\n", tooltip)));
                    }
                    break;
            }

            return tiles;
        }

        private GridTileData CreateShopTile(int itemId, string name, int graphic, string priceText, object source, string tooltipText = null)
        {
            Texture2D iconTexture = null;
            try
            {
                iconTexture = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * graphic - 1, transparent: true);
            }
            catch { /* gracefully handle missing graphics */ }

            return new GridTileData
            {
                ItemID = itemId,
                Name = $"{name}\n{priceText}",
                Amount = 0, // Price is embedded in the name
                IconTexture = iconTexture,
                Tag = source,
                TooltipText = tooltipText ?? name
            };
        }

        /// <summary>
        /// Build rich tooltip lines for an item (stats, weight, damage, etc.).
        /// </summary>
        private List<string> BuildItemTooltip(EOLib.IO.Pub.EIFRecord data)
        {
            var lines = new List<string> { data.Name };
            lines.Add($"Type: {data.Type}");
            if (data.Weight > 0) lines.Add($"Weight: {data.Weight}");

            if (data.MinDam > 0 || data.MaxDam > 0)
                lines.Add($"Damage: {data.MinDam}-{data.MaxDam}");
            if (data.Accuracy > 0) lines.Add($"Accuracy: {data.Accuracy}");
            if (data.Evade > 0) lines.Add($"Evade: {data.Evade}");
            if (data.Armor > 0) lines.Add($"Armor: {data.Armor}");
            if (data.HP > 0) lines.Add($"HP: +{data.HP}");
            if (data.TP > 0) lines.Add($"TP: +{data.TP}");

            var stats = new List<string>();
            if (data.Str > 0) stats.Add($"Str+{data.Str}");
            if (data.Int > 0) stats.Add($"Int+{data.Int}");
            if (data.Wis > 0) stats.Add($"Wis+{data.Wis}");
            if (data.Agi > 0) stats.Add($"Agi+{data.Agi}");
            if (data.Con > 0) stats.Add($"Con+{data.Con}");
            if (data.Cha > 0) stats.Add($"Cha+{data.Cha}");
            if (stats.Count > 0) lines.Add(string.Join(" ", stats));

            if (data.LevelReq > 0) lines.Add($"Lvl Req: {data.LevelReq}");

            if (data.Type == ItemType.Heal && data.HP > 0)
                lines.Add($"Heals: {data.HP} HP");
            if (data.Type == ItemType.Heal && data.TP > 0)
                lines.Add($"Restores: {data.TP} TP");

            return lines;
        }

        protected override void OnTileClicked(GridTileData tileData)
        {
            var activeTab = GetShopTab();

            if (activeTab == ShopTab.Buy && tileData.Tag is IShopItem buyItem)
                TradeItem(buyItem, buying: true);
            else if (activeTab == ShopTab.Sell && tileData.Tag is IShopItem sellItem)
                TradeItem(sellItem, buying: false);
            else if (activeTab == ShopTab.Craft && tileData.Tag is IShopCraftItem craftItem)
                CraftItem(craftItem);
        }

        private ShopTab GetShopTab() => ActiveTabIndex switch
        {
            0 => ShopTab.Buy,
            1 => ShopTab.Sell,
            2 => ShopTab.Craft,
            _ => ShopTab.Buy
        };

        private static string FormatGold(int amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:0.#}M";
            if (amount >= 10_000) return $"{amount / 1_000f:0.#}K";
            return amount.ToString("N0");
        }

        // ---- Trade & Craft Logic ----

        private void TradeItem(IShopItem shopItem, bool buying)
        {
            var data = _eifFileProvider.EIFFile[shopItem.ID];
            var inventoryItem = _characterInventoryProvider.ItemInventory
                .SingleOrNone(x => buying ? x.ItemID == 1 : x.ItemID == shopItem.ID);

            // Validation
            if (buying)
            {
                if (!_inventorySpaceValidator.ItemFits(data.ID))
                {
                    var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_SPACE, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                    msg.ShowDialog();
                    return;
                }

                var stats = _characterProvider.MainCharacter.Stats;
                if (data.Weight + stats[CharacterStat.Weight] > stats[CharacterStat.MaxWeight])
                {
                    var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                    msg.ShowDialog();
                    return;
                }

                var hasEnoughGold = inventoryItem.Match(some: x => x.Amount >= shopItem.Buy, none: () => false);
                if (!hasEnoughGold)
                {
                    var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH, " gold.");
                    msg.ShowDialog();
                    return;
                }
            }
            else
            {
                var hasEnoughItem = inventoryItem.Match(some: x => x.Amount > 0, none: () => false);
                if (!hasEnoughItem)
                {
                    var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SHOP_NOT_BUYING_YOUR_ITEMS);
                    msg.ShowDialog();
                    return;
                }
            }

            var needItemTransferDialog = (buying && shopItem.MaxBuy != 1) || (!buying && inventoryItem.Match(x => x.Amount != 1, () => false));

            if (needItemTransferDialog)
            {
                var itemTransferDialog = _itemTransferDialogFactory.CreateItemTransferDialog(data.Name,
                    ItemTransferType.ShopTransfer,
                    buying ? shopItem.MaxBuy : inventoryItem.Match(x => x.Amount, () => 0),
                    buying ? EOResourceID.DIALOG_TRANSFER_BUY : EOResourceID.DIALOG_TRANSFER_SELL);
                itemTransferDialog.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        ConfirmAndExecuteTrade(itemTransferDialog.SelectedAmount);
                };

                itemTransferDialog.ShowDialog();
            }
            else
            {
                ConfirmAndExecuteTrade(amount: 1);
            }

            void ConfirmAndExecuteTrade(int amount)
            {
                var message = $"{_localizedStringFinder.GetString(buying ? EOResourceID.DIALOG_WORD_BUY : EOResourceID.DIALOG_WORD_SELL)} {amount} {data.Name} " +
                    $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_WORD_FOR)} {(buying ? shopItem.Buy : shopItem.Sell) * amount} gold?";
                var dlg = _messageBoxFactory.CreateMessageBox(message, _localizedStringFinder.GetString(buying ? EOResourceID.DIALOG_SHOP_BUY_ITEMS : EOResourceID.DIALOG_SHOP_SELL_ITEMS), EODialogButtons.OkCancel);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.Cancel)
                        return;

                    if (buying)
                        _shopActions.BuyItem(shopItem.ID, amount);
                    else
                        _shopActions.SellItem(shopItem.ID, amount);
                };
                dlg.ShowDialog();
            }
        }

        private void CraftItem(IShopCraftItem craftItem)
        {
            var data = _eifFileProvider.EIFFile[craftItem.ID];

            // Check ingredients
            foreach (var ingredient in craftItem.Ingredients)
            {
                if (!_characterInventoryProvider.ItemInventory.Any(x => x.ItemID == ingredient.ID && x.Amount >= ingredient.Amount))
                {
                    var message = BuildMessage(EOResourceID.DIALOG_SHOP_CRAFT_MISSING_INGREDIENTS);
                    var caption = BuildCaption(EOResourceID.DIALOG_SHOP_CRAFT_INGREDIENTS);

                    var dlg = _messageBoxFactory.CreateMessageBox(message, caption, EODialogButtons.Cancel, EOMessageBoxStyle.LargeDialogSmallHeader);
                    dlg.ShowDialog();
                    return;
                }
            }

            if (!_inventorySpaceValidator.ItemFits(data.ID))
            {
                var msg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_SPACE, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                msg.ShowDialog();
                return;
            }

            var message2 = BuildMessage(EOResourceID.DIALOG_SHOP_CRAFT_PUT_INGREDIENTS_TOGETHER);
            var caption2 = BuildCaption(EOResourceID.DIALOG_SHOP_CRAFT_INGREDIENTS);

            var dlg2 = _messageBoxFactory.CreateMessageBox(message2, caption2, EODialogButtons.OkCancel, EOMessageBoxStyle.LargeDialogSmallHeader);
            dlg2.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.Cancel)
                    return;

                _shopActions.CraftItem(craftItem.ID);
            };
            dlg2.ShowDialog();

            string BuildMessage(EOResourceID resource)
            {
                var message = _localizedStringFinder.GetString(resource) + "\n\n";

                foreach (var ingred in craftItem.Ingredients)
                {
                    var ingredData = _eifFileProvider.EIFFile[ingred.ID];
                    message += $"+  {ingred.Amount}  {ingredData.Name}\n";
                }

                return message;
            }

            string BuildCaption(EOResourceID resource)
            {
                return $"{_localizedStringFinder.GetString(resource)} {_localizedStringFinder.GetString(EOResourceID.DIALOG_WORD_FOR)} {data.Name}";
            }
        }

        /// <summary>
        /// Called when an inventory item is dragged and dropped onto this shop dialog.
        /// Auto-switches to the Sell tab and initiates the sell flow if the item is sellable.
        /// </summary>
        public void AcceptItemDrop(int itemId)
        {
            // Switch to sell tab
            ActiveTabIndex = 1;

            // Find the matching sell item
            var sellItem = _sellItems.FirstOrDefault(x => x.ID == itemId);
            if (sellItem != null)
            {
                TradeItem(sellItem, buying: false);
            }
        }
    }
}
