using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Shop;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of ShopDialog with procedural rendering.
    /// </summary>
    public class CodeDrawnShopDialog : CodeDrawnScrollingListDialog
    {
        private enum ShopState
        {
            None,
            Initial,
            Buying,
            Selling,
            Crafting
        }

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
        private readonly IContentProvider _contentProvider;

        private IReadOnlyList<IShopItem> _buyItems, _sellItems;
        private IReadOnlyList<IShopCraftItem> _craftItems;
        private ShopState _state;
        private Option<int> _cachedShopId;
        private HashSet<InventoryItem> _cachedInventory;
        private ulong _tick;

        private CodeDrawnButton _backButton;
        private CodeDrawnButton _cancelButton;

        public CodeDrawnShopDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager graphicsManager,
            IShopActions shopActions,
            IEOMessageBoxFactory messageBoxFactory,
            IItemTransferDialogFactory itemTransferDialogFactory,
            ILocalizedStringFinder localizedStringFinder,
            IShopDataProvider shopDataProvider,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider,
            ICharacterProvider characterProvider,
            IInventorySpaceValidator inventorySpaceValidator,
            IContentProvider contentProvider)
            : base(styleProvider, gameStateProvider, clientWindowSizeProvider, graphicsDeviceProvider,
                   contentProvider.Fonts[Constants.FontSize08pt5], contentProvider.Fonts[Constants.FontSize10])
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
            _contentProvider = contentProvider;

            // Configure for shop dialog size
            DialogWidth = 320;
            DialogHeight = 340;
            ListAreaTop = 45;
            ListAreaHeight = 240;
            ItemHeight = 36; // Large items with icons

            UpdateScrollBarLayout();

            _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);
            _buyItems = new List<IShopItem>();
            _sellItems = new List<IShopItem>();
            _craftItems = new List<IShopCraftItem>();

            CreateButtons(styleProvider);
            CenterInGameView();
        }

        private void CreateButtons(IUIStyleProvider styleProvider)
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var buttonWidth = 72;
            var buttonHeight = 26;
            var buttonY = DialogHeight - 36;

            _backButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Back",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 8, buttonY, buttonWidth, buttonHeight),
                Visible = false
            };
            _backButton.OnClick += (_, _) => SetState(ShopState.Initial);
            _backButton.SetParentControl(this);

            _cancelButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 + 8, buttonY, buttonWidth, buttonHeight),
                Visible = true
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);
        }

        public override void Initialize()
        {
            _backButton?.Initialize();
            _cancelButton?.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _cachedShopId.MatchNone(() =>
            {
                _shopDataProvider.SessionID.SomeWhen(x => x > 0)
                    .MatchSome(x =>
                    {
                        _cachedShopId = Option.Some(_shopDataProvider.SessionID);

                        Title = _shopDataProvider.ShopName;

                        _buyItems = _shopDataProvider.TradeItems.Where(x => x.Buy > 0).ToList();
                        _sellItems = _shopDataProvider.TradeItems.Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0)).ToList();
                        _craftItems = _shopDataProvider.CraftItems;

                        SetState(ShopState.Initial);
                    });
            });

            if (++_tick % 8 == 0 && !_cachedInventory.SetEquals(_characterInventoryProvider.ItemInventory))
            {
                _sellItems = _shopDataProvider.TradeItems.Where(x => x.Sell > 0 && _characterInventoryProvider.ItemInventory.Any(inv => inv.ItemID == x.ID && inv.Amount > 0)).ToList();
                _cachedInventory = new HashSet<InventoryItem>(_characterInventoryProvider.ItemInventory);

                if (_state == ShopState.Selling)
                    SetState(ShopState.Selling);
            }

            base.OnUpdateControl(gameTime);
        }

        private void SetState(ShopState state)
        {
            if (state == ShopState.None)
                return;

            if (state == ShopState.Buying && _buyItems.Count == 0)
            {
                var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SHOP_NOTHING_IS_FOR_SALE);
                msg.ShowDialog();

                if (_state != ShopState.Initial)
                    SetState(ShopState.Initial);
                return;
            }
            else if (state == ShopState.Selling && _sellItems.Count == 0)
            {
                var msg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SHOP_NOT_BUYING_YOUR_ITEMS);
                msg.ShowDialog();

                if (_state != ShopState.Initial)
                    SetState(ShopState.Initial);
                return;
            }

            ClearItems();

            switch (state)
            {
                case ShopState.Initial:
                    AddItem($"🛒 {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_BUY_ITEMS)}",
                            subText: $"{_buyItems.Count} {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_ITEMS_IN_STORE)}",
                            onClick: _ => SetState(ShopState.Buying),
                            isLink: true);

                    AddItem($"💰 {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_SELL_ITEMS)}",
                            subText: $"{_sellItems.Count} {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_ITEMS_ACCEPTED)}",
                            onClick: _ => SetState(ShopState.Selling),
                            isLink: true);

                    if (_craftItems.Count > 0)
                    {
                        AddItem($"🔨 {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_CRAFT_ITEMS)}",
                                subText: $"{_craftItems.Count} {_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_ITEMS_ACCEPTED)}",
                                onClick: _ => SetState(ShopState.Crafting),
                                isLink: true);
                    }

                    _backButton.Visible = false;
                    _cancelButton.DrawArea = new Rectangle((DialogWidth - 72) / 2, DialogHeight - 36, 72, 26);
                    break;

                case ShopState.Buying:
                case ShopState.Selling:
                    var buying = state == ShopState.Buying;
                    foreach (var item in buying ? _buyItems : _sellItems)
                    {
                        var data = _eifFileProvider.EIFFile[item.ID];
                        var genderExtra = data.Type == EOLib.IO.ItemType.Armor ? $"({_localizedStringFinder.GetString(EOResourceID.FEMALE - data.Gender)})" : string.Empty;
                        var subText = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_PRICE)}: {(buying ? item.Buy : item.Sell)} {genderExtra}";
                        var itemIcon = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * data.Graphic - 1, transparent: true);

                        var shopItem = item;
                        AddItem(data.Name, subText: subText, data: shopItem, onClick: _ => TradeItem(shopItem, buying), isLink: true, icon: itemIcon);
                    }

                    _backButton.Visible = true;
                    _cancelButton.DrawArea = new Rectangle(DialogWidth / 2 + 8, DialogHeight - 36, 72, 26);
                    break;

                case ShopState.Crafting:
                    foreach (var item in _craftItems)
                    {
                        var data = _eifFileProvider.EIFFile[item.ID];
                        var genderExtra = data.Type == EOLib.IO.ItemType.Armor ? $"({_localizedStringFinder.GetString(EOResourceID.FEMALE - data.Gender)})" : string.Empty;
                        var subText = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_SHOP_CRAFT_INGREDIENTS)}: {item.Ingredients.Count} {genderExtra}";
                        var itemIcon = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * data.Graphic - 1, transparent: true);

                        var craftItem = item;
                        AddItem(data.Name, subText: subText, data: craftItem, onClick: _ => CraftItem(craftItem), isLink: true, icon: itemIcon);
                    }

                    _backButton.Visible = true;
                    _cancelButton.DrawArea = new Rectangle(DialogWidth / 2 + 8, DialogHeight - 36, 72, 26);
                    break;
            }

            _state = state;
        }

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
                    ItemTransferDialog.TransferType.ShopTransfer,
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
            dlg2.DialogClosing += (o, e) =>
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

        protected override void DrawButtonTextPostScale(Vector2 scaledPos, float scale, MonoGame.Extended.BitmapFonts.BitmapFont font)
        {
            var buttonWidth = (int)(72 * scale);
            var buttonHeight = (int)(26 * scale);
            var buttonY = (int)(scaledPos.Y + (DialogHeight - 36) * scale);

            // Back button
            if (_backButton != null && _backButton.Visible)
            {
                var backX = (int)(scaledPos.X + (DialogWidth / 2 - 72 - 8) * scale);
                DrawButtonPostScale("Back", backX, buttonY, buttonWidth, buttonHeight, scale, font, _backButton.MouseOver);
            }

            // Cancel button
            if (_cancelButton != null && _cancelButton.Visible)
            {
                int cancelX;
                if (!_backButton.Visible)
                {
                    // Center single cancel button
                    cancelX = (int)(scaledPos.X + ((DialogWidth - 72) / 2) * scale);
                }
                else
                {
                    cancelX = (int)(scaledPos.X + (DialogWidth / 2 + 8) * scale);
                }
                DrawButtonPostScale("Cancel", cancelX, buttonY, buttonWidth, buttonHeight, scale, font, _cancelButton.MouseOver);
            }
        }
    }
}
