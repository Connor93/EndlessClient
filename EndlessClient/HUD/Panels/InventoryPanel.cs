using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EndlessClient.Audio;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Inventory;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Map;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Item;
using EOLib.Domain.Login;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Extensions;
using EOLib.IO.Map;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Input;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class InventoryPanel : DraggableHudPanel, IHudPanel, IDraggableItemContainer
    {
        public const int InventoryRows = 10;
        public const int InventoryRowSlots = 7;

        private readonly IInventoryController _inventoryController;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IItemStringService _itemStringService;
        private readonly IItemNameColorService _itemNameColorService;
        private readonly IInventoryService _inventoryService;
        private readonly IInventorySlotRepository _inventorySlotRepository;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly ICharacterProvider _characterProvider;
        protected readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IPubFileProvider _pubFileProvider; // todo: this can probably become EIFFileProvider
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IActiveDialogProvider _activeDialogProvider;
        protected readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigurationProvider _configProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        protected readonly IUserInputProvider _userInputProvider;
        private readonly List<InventoryPanelItem> _childItems = new List<InventoryPanelItem>();

        //private readonly IXNALabel _weightLabel;
        //private readonly ScrollBar _scrollBar;

        private Option<CharacterStats> _cachedStats;
        private HashSet<InventoryItem> _cachedInventory;

        public INativeGraphicsManager NativeGraphicsManager { get; }

        public InventoryPanel(INativeGraphicsManager nativeGraphicsManager,
                              IInventoryController inventoryController,
                              IStatusLabelSetter statusLabelSetter,
                              IItemStringService itemStringService,
                              IItemNameColorService itemNameColorService,
                              IInventoryService inventoryService,
                              IInventorySlotRepository inventorySlotRepository,
                              IPlayerInfoProvider playerInfoProvider,
                              ICharacterProvider characterProvider,
                              ICharacterInventoryProvider characterInventoryProvider,
                              IPubFileProvider pubFileProvider,
                              IHudControlProvider hudControlProvider,
                              IActiveDialogProvider activeDialogProvider,
                              ISfxPlayer sfxPlayer,
                              IConfigurationProvider configProvider,
                              IClientWindowSizeProvider clientWindowSizeProvider,
                              IUserInputProvider userInputProvider)
            : base(clientWindowSizeProvider.Resizable)
        {
            NativeGraphicsManager = nativeGraphicsManager;
            _inventoryController = inventoryController;
            _statusLabelSetter = statusLabelSetter;
            _itemStringService = itemStringService;
            _itemNameColorService = itemNameColorService;
            _inventoryService = inventoryService;
            _inventorySlotRepository = inventorySlotRepository;
            _playerInfoProvider = playerInfoProvider;
            _characterProvider = characterProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _pubFileProvider = pubFileProvider;
            _hudControlProvider = hudControlProvider;
            _activeDialogProvider = activeDialogProvider;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _userInputProvider = userInputProvider;

            /*_weightLabel = new XNALabel(Constants.FontSize08pt5)
            {
                DrawArea = new Rectangle(395, 37, 88, 18),
                ForeColor = ColorConstants.LightGrayText,
                TextAlign = LabelAlignment.MiddleCenter,
                Visible = true,
                AutoSize = false
            };*/



            _cachedStats = Option.None<CharacterStats>();
            _cachedInventory = new HashSet<InventoryItem>();

            BackgroundImage = NativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 44);
            DrawArea = new Rectangle(102, 330, BackgroundImage.Width, BackgroundImage.Height);

            Game.Exiting += SaveInventoryFile;
        }

        public bool NoItemsDragging() => _childItems.All(x => !x.IsDragging);

        protected void SortInventory()
        {
            if (_childItems.Any(x => x.IsDragging))
                return;

            var itemData = _childItems.Select(x =>
            {
                var data = _pubFileProvider.EIFFile[x.InventoryItem.ItemID];
                return (x.InventoryItem.ItemID, x.Slot, data.Size, data.Type);
            });

            var newSlots = _inventoryService.SortItems(_inventorySlotRepository.FilledSlots, itemData);

            foreach (var childItem in _childItems)
            {
                if (newSlots.TryGetValue(childItem.InventoryItem.ItemID, out var newSlot))
                {
                    childItem.Slot = newSlot;
                }
            }
        }

        public override void Initialize()
        {
            //_weightLabel.Initialize();
            //_weightLabel.SetParentControl(this);



            _inventorySlotRepository.SlotMap = GetItemSlotMap(_playerInfoProvider.LoggedInAccountName, _characterProvider.MainCharacter.Name, _configProvider.Host);
            OnUpdateControl(new GameTime());

            base.Initialize();
        }

        protected override void OnDrawOrderChanged(object sender, EventArgs args)
        {
            base.OnDrawOrderChanged(sender, args);

            if (_clientWindowSizeProvider.Resizable)
            {
                // ensure labels have a high enough draw order when in resizable mode
                // this is because draw order is updated when panels are opened in resizable mode
                foreach (var label in ChildControls.OfType<XNALabel>())
                    label.DrawOrder = DrawOrder + 70;
            }
        }

        protected override void OnUnconditionalUpdateControl(GameTime gameTime)
        {
            _cachedStats.Match(
                some: stats =>
                {
                    stats.SomeWhen(s => s != _characterProvider.MainCharacter.Stats)
                        .MatchSome(_ =>
                        {
                            var newStats = _characterProvider.MainCharacter.Stats;
                            _cachedStats = Option.Some(newStats);
                            //_weightLabel.Text = $"{newStats[CharacterStat.Weight]} / {newStats[CharacterStat.MaxWeight]}";
                        });
                },
                none: () =>
                {
                    var stats = _characterProvider.MainCharacter.Stats;
                    _cachedStats = Option.Some(stats);
                    //_weightLabel.Text = $"{stats[CharacterStat.Weight]} / {stats[CharacterStat.MaxWeight]}";
                });

            if (!_cachedInventory.SetEquals(_characterInventoryProvider.ItemInventory))
            {
                var added = _characterInventoryProvider.ItemInventory.Where(i => !_cachedInventory.Any(j => i.ItemID == j.ItemID));
                var removed = _cachedInventory.Where(i => !_characterInventoryProvider.ItemInventory.Any(j => i.ItemID == j.ItemID));
                var updated = _characterInventoryProvider.ItemInventory.Except(added)
                    .Where(i => _cachedInventory.Any(j => i.ItemID == j.ItemID && i.Amount != j.Amount));

                foreach (var item in removed)
                {
                    var matchedItem = _childItems.SingleOrNone(x => x.InventoryItem.ItemID == item.ItemID);
                    matchedItem.MatchSome(childItem =>
                    {
                        childItem.SetControlUnparented();
                        childItem.Dispose();
                        _childItems.Remove(childItem);

                        try
                        {
                            var itemData = _pubFileProvider.EIFFile[item.ItemID];
                            _inventoryService.ClearSlots(_inventorySlotRepository.FilledSlots, childItem.Slot, itemData.Size);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            Console.WriteLine($"[InventoryPanel] EIF lookup failed for removed item ID {item.ItemID} (EIF length: {_pubFileProvider.EIFFile.Length})");
                        }
                    });
                }

                foreach (var item in updated)
                {
                    EIFRecord itemData;
                    try
                    {
                        itemData = _pubFileProvider.EIFFile[item.ItemID];
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Console.WriteLine($"[InventoryPanel] EIF lookup failed for updated item ID {item.ItemID} (EIF length: {_pubFileProvider.EIFFile.Length})");
                        continue;
                    }

                    var matchedItem = _childItems.SingleOrNone(x => x.InventoryItem.ItemID == item.ItemID);
                    matchedItem.MatchSome(childItem =>
                    {
                        childItem.InventoryItem = item;
                        childItem.Text = _itemStringService.GetStringForInventoryDisplay(itemData, item.Amount);
                    });
                }

                foreach (var item in added)
                {
                    EIFRecord itemData;
                    try
                    {
                        itemData = _pubFileProvider.EIFFile[item.ItemID];
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Console.WriteLine($"[InventoryPanel] EIF lookup failed for added item ID {item.ItemID} (EIF length: {_pubFileProvider.EIFFile.Length})");
                        continue;
                    }

                    var preferredSlot = _inventorySlotRepository.SlotMap.SingleOrNone(x => x.Value == item.ItemID).Map(x => x.Key);
                    var actualSlot = _inventoryService.GetNextOpenSlot(_inventorySlotRepository.FilledSlots, itemData.Size, preferredSlot);

                    actualSlot.MatchSome(slot =>
                    {
                        _inventoryService.SetSlots(_inventorySlotRepository.FilledSlots, slot, itemData.Size);

                        var newItem = new InventoryPanelItem(_itemNameColorService, this, _activeDialogProvider, _sfxPlayer, _userInputProvider, _clientWindowSizeProvider, slot, item, itemData);
                        newItem.Initialize();
                        newItem.SetParentControl(this);
                        newItem.Text = _itemStringService.GetStringForInventoryDisplay(itemData, item.Amount);

                        newItem.OnMouseEnter += (_, _) => _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ITEM, newItem.Text);
                        newItem.DoubleClick += HandleItemDoubleClick;
                        newItem.RightClick += HandleItemRightClick;
                        newItem.DraggingFinishing += HandleItemDoneDragging;
                        newItem.DraggingFinished += (_, _) => ResetSlotMap(_childItems.Where(x => !x.IsDragging));

                        // side-effect of calling newItem.SetParentControl(this) is that the draw order gets reset
                        // setting the slot manually here resets it so the item labels render appropriately
                        newItem.Slot = slot;

                        _childItems.Add(newItem);
                    });
                }

                _cachedInventory = _characterInventoryProvider.ItemInventory.ToHashSet();

                if (removed.Any())
                {
                    RemoveHiddenItemsFromCachedInventory();
                }
            }

            base.OnUpdateControl(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

                Game.Exiting -= SaveInventoryFile;

                // todo: IResettable should work but it doesn't
                _inventorySlotRepository.FilledSlots = new Matrix<bool>(InventoryRows, InventoryRowSlots, false);

                SaveInventoryFile(null, new ExitingEventArgs());
            }

            base.Dispose(disposing);
        }



        private static Dictionary<int, int> GetItemSlotMap(string accountName, string characterName, string host)
        {
            var map = new Dictionary<int, int>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !File.Exists(Constants.InventoryFile))
            {
                using var registryInventoryKey = TryGetCharacterRegistryKey(accountName, characterName);
                if (registryInventoryKey != null)
                {
                    for (int i = 0; i < InventoryRowSlots * 4; ++i)
                    {
                        if (int.TryParse(registryInventoryKey.GetValue($"item{i}")?.ToString() ?? string.Empty, out var id))
                            map[i] = id;
                    }
                }
            }

            var inventory = new IniReader(Constants.InventoryFile);
            var inventoryKey = $"{host}:{accountName}";
            if (inventory.Load() && inventory.Sections.ContainsKey(inventoryKey))
            {
                var section = inventory.Sections[inventoryKey];
                foreach (var key in section.Keys.Where(x => x.Contains(characterName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!key.Contains("."))
                        continue;

                    var slot = key.Split(".")[1];
                    if (!int.TryParse(slot, out var slotIndex))
                        continue;

                    if (int.TryParse(section[key], out var id))
                        map[slotIndex] = id;
                }
            }

            return map;
        }

        [SupportedOSPlatform("Windows")]
        private static RegistryKey TryGetCharacterRegistryKey(string accountName, string characterName)
        {
            using RegistryKey currentUser = Registry.CurrentUser;

            var pathSegments = $"Software\\EndlessClient\\{accountName}\\{characterName}\\inventory".Split('\\');
            var currentPath = string.Empty;

            RegistryKey retKey = null;
            foreach (var segment in pathSegments)
            {
                retKey?.Dispose();

                currentPath = Path.Combine(currentPath, segment);
                retKey = currentUser.CreateSubKey(currentPath, RegistryKeyPermissionCheck.ReadSubTree);
            }

            return retKey;
        }

        private void SaveInventoryFile(object sender, ExitingEventArgs e)
        {
            var inventory = new IniReader(Constants.InventoryFile);
            var inventoryKey = $"{_configProvider.Host}:{_playerInfoProvider.LoggedInAccountName}";

            var section = inventory.Load() && inventory.Sections.ContainsKey(inventoryKey)
                ? inventory.Sections[inventoryKey]
                : new SortedList<string, string>();

            var existing = section.Where(x => x.Key.Contains(_characterProvider.MainCharacter.Name)).Select(x => x.Key).ToList();
            foreach (var key in existing)
                section.Remove(key);

            foreach (var item in _childItems)
                section[$"{_characterProvider.MainCharacter.Name}.{item.Slot}"] = $"{item.InventoryItem.ItemID}";

            inventory.Sections[inventoryKey] = section;

            inventory.Save();
        }

        private void HandleItemDoubleClick(object sender, EIFRecord itemData)
        {
            UseOrEquipItem(itemData);
        }

        protected void UseOrEquipItem(EIFRecord itemData)
        {
            if (itemData.Type >= ItemType.Weapon && itemData.Type <= ItemType.Bracer)
            {
                _inventoryController.EquipItem(itemData);
            }
            else
            {
                _inventoryController.UseItem(itemData);
            }

            _sfxPlayer.PlaySfx(SoundEffectID.InventoryPlace);
        }

        protected void DropItem(EIFRecord itemData)
        {
            var inventoryItem = _characterInventoryProvider.ItemInventory.FirstOrDefault(x => x.ItemID == itemData.ID);
            if (inventoryItem != null)
            {
                var rp = _characterProvider.MainCharacter.RenderProperties;
                var coords = new EOLib.Domain.Map.MapCoordinate(rp.MapX, rp.MapY);
                _inventoryController.DropItem(itemData, inventoryItem, coords);
            }
        }

        protected void JunkItem(EIFRecord itemData)
        {
            var inventoryItem = _characterInventoryProvider.ItemInventory.FirstOrDefault(x => x.ItemID == itemData.ID);
            if (inventoryItem != null)
            {
                _inventoryController.JunkItem(itemData, inventoryItem);
            }
        }

        protected void StoreItem(EIFRecord itemData)
        {
            _inventoryController.StoreItem(itemData);
        }

        protected void GlamorItem(EIFRecord itemData)
        {
            _inventoryController.GlamorItem(itemData);
        }

        protected virtual void HandleItemRightClick(object sender, EIFRecord itemData)
        {
            // Base class: fallback to use/equip
            UseOrEquipItem(itemData);
        }

        private void HandleItemDoneDragging(object sender, DragCompletedEventArgs<EIFRecord> e)
        {
            var item = sender as InventoryPanelItem;
            if (item == null)
                return;

            ResetSlotMap(_childItems.Where(x => !x.IsDragging));

            // Check if item was dropped onto the MacroPanel (check first, before other handlers)
            var macroPanel = _hudControlProvider.GetComponent<MacroPanel>(HudControlIdentifier.MacroPanel);
            if (macroPanel.Visible && macroPanel.MouseOver)
            {
                var mousePos = MonoGame.Extended.Input.MouseExtended.GetState().Position;
                var transformedPos = macroPanel.TransformMousePosition(mousePos).ToVector2();
                var targetSlot = macroPanel.GetSlotFromPosition(transformedPos);
                if (targetSlot >= 0)
                {
                    macroPanel.AcceptItemDrop(item.InventoryItem.ItemID, targetSlot);
                    e.RestoreOriginalSlot = true;  // Keep item in inventory
                    return;
                }
            }

            var oldSlot = item.Slot;
            var fitsInOldSlot = _inventoryService.FitsInSlot(_inventorySlotRepository.FilledSlots, oldSlot, e.Data.Size);

            if (_activeDialogProvider.ActiveDialogs.All(x => !x.HasValue))
            {
                var mapRenderer = _hudControlProvider.GetComponent<IMapRenderer>(HudControlIdentifier.MapRenderer);
                if (mapRenderer.MouseOver && !MouseOver)
                {
                    e.ContinueDrag = !fitsInOldSlot;
                    e.RestoreOriginalSlot = fitsInOldSlot;

                    _inventoryController.DropItem(item.Data, item.InventoryItem, mapRenderer.GridCoordinates);
                    return;
                }
            }



            var dialogDrop = false;
            foreach (var dlg in _activeDialogProvider.ActiveDialogs)
            {
                dialogDrop |= dlg.Match(
                    activeDialog =>
                    {
                        if (!activeDialog.MouseOver && !activeDialog.MouseOverPreviously)
                            return false;

                        switch (activeDialog)
                        {
                            case PaperdollDialog:
                            case CodeDrawnPaperdollDialog:
                            case MyraPaperdollDialog:
                                if (item.Data.GetEquipLocation() != EquipLocation.PAPERDOLL_MAX)
                                    _inventoryController.EquipItem(item.Data);
                                break;
                            case ChestDialog:
                            case CodeDrawnChestDialog:
                            case MyraChestDialog:
                                _inventoryController.DropItemInChest(item.Data, item.InventoryItem); break;
                            case LockerDialog:
                            case CodeDrawnLockerDialog:
                            case CodeDrawnGridLockerDialog:
                            case MyraLockerDialog:
                            case MyraGridLockerDialog:
                                _inventoryController.DropItemInLocker(item.Data, item.InventoryItem); break;
                            case BankAccountDialog:
                                if (item.Data.ID == 1)
                                    _inventoryController.DropItemInBank(item.Data, item.InventoryItem);
                                break;
                            case TradeDialog:
                            case CodeDrawnTradeDialog:
                            case MyraTradeDialog: _inventoryController.TradeItem(item.Data, item.InventoryItem); break;
                            case MyraShopDialog shopDialog:
                                shopDialog.AcceptItemDrop(item.Data.ID); break;
                            default: return false;
                        }
                        ;

                        return true;
                    },
                    () => false);
            }

            if (e.DragOutOfBounds || dialogDrop)
            {
                e.ContinueDrag = !fitsInOldSlot;
                e.RestoreOriginalSlot = fitsInOldSlot;
                return;
            }

            var newSlot = item.GetCurrentSlotBasedOnPosition();

            // check overlapping items:
            //   1. If there's multiple items under it, snap it back to the original slot if iti fits, otherwise continue dragging
            //   2. If there's only one item under it, "chain" the drag operation to that item
            //   3. If there's nothing under it, make sure it fits in the inventory, otherwise snap back to original slot

            var overlapped = GetOverlappingTakenSlots(newSlot, e.Data.Size, _childItems.Except(new[] { item }).Select(x => (x.Slot, x.Data.Size)))
                .ToList();

            if (overlapped.Count > 1)
            {
                e.RestoreOriginalSlot = true;

                if (!fitsInOldSlot)
                    e.ContinueDrag = true;
            }
            else if (overlapped.Count == 1)
            {
                var nextItem = _childItems.Single(x => x.Slot == overlapped[0] && !x.IsDragging);
                nextItem.Slot = oldSlot;
                nextItem.StartDragging(isChainedDrag: true);
            }
            else if (oldSlot != newSlot)
            {
                if (!_inventoryService.FitsInSlot(_inventorySlotRepository.FilledSlots, oldSlot, newSlot, e.Data.Size))
                {
                    // if the original slot no longer fits (because this is a chained drag), don't stop dragging this item
                    if (!fitsInOldSlot)
                    {
                        e.ContinueDrag = true;
                    }
                    else
                    {
                        e.RestoreOriginalSlot = true;
                    }
                }
                else
                {
                    RemoveHiddenItemsFromCachedInventory();
                }
            }
        }

        private void ResetSlotMap(IEnumerable<InventoryPanelItem> childItems)
        {
            // reset the slot map based on the current state of the inventory
            // avoids issues due to chained drags + variable item sizes
            _inventorySlotRepository.FilledSlots.Fill(false);
            foreach (var childItem in childItems)
                _inventoryService.SetSlots(_inventorySlotRepository.FilledSlots, childItem.Slot, childItem.Data.Size);
        }

        private void RemoveHiddenItemsFromCachedInventory()
        {
            // the item fits in the new slot, and there is no chained drag, snapback, or continued drag
            // under these conditions, check if there are any items that don't have a matching childItem and remove them from the cached list
            // the next update loop will detect these items as 'added' and attempt to show them in the empty space

            var notDisplayedItems = _cachedInventory.Where(x => _childItems.All(ci => !x.Equals(ci.InventoryItem))).ToList();
            _cachedInventory.RemoveWhere(notDisplayedItems.Contains);
        }

        private static IEnumerable<int> GetOverlappingTakenSlots(int newSlot, ItemSize size, IEnumerable<(int Slot, ItemSize Size)> items)
        {
            var slotX = newSlot % InventoryRowSlots;
            var slotY = newSlot / InventoryRowSlots;
            var slotItemDim = size.GetDimensions();

            var newSlotCoords = new List<(int X, int Y)>();
            for (int r = slotY; r < slotY + slotItemDim.Height; r++)
                for (int c = slotX; c < slotX + slotItemDim.Width; c++)
                    newSlotCoords.Add((c, r));

            foreach (var item in items)
            {
                var itemX = item.Slot % InventoryRowSlots;
                var itemY = item.Slot / InventoryRowSlots;
                var itemDim = item.Size.GetDimensions();

                var @break = false;
                for (int r = itemY; r < itemY + itemDim.Height; r++)
                {
                    if (@break)
                        break;

                    for (int c = itemX; c < itemX + itemDim.Width; c++)
                    {
                        if (newSlotCoords.Contains((c, r)))
                        {
                            yield return item.Slot;
                            @break = true;
                            break;
                        }
                    }
                }
            }
        }
        public Point TransformMousePosition(Point position)
        {
            var offset = _clientWindowSizeProvider.RenderOffset;
            var scale = _clientWindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Clamp(gameX, 0, _clientWindowSizeProvider.GameWidth - 1),
                Math.Clamp(gameY, 0, _clientWindowSizeProvider.GameHeight - 1));
        }
    }
}
