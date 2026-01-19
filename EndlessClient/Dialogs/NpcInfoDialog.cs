using System.Linq;
using EndlessClient.Dialogs.Services;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.Dialogs
{
    public class NpcInfoDialog : ScrollingListDialog
    {
        private readonly ENFRecord _npc;
        private readonly Texture2D _npcGraphic;
        private readonly INpcSourceProvider _npcSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private const int NpcImageY = 45;
        private bool _sourcesChecked;
        private int _lastDataHash;

        public NpcInfoDialog(INativeGraphicsManager nativeGraphicsManager,
                             IEODialogButtonService dialogButtonService,
                             INpcSourceProvider npcSourceProvider,
                             IEIFFileProvider eifFileProvider,
                             IENFFileProvider enfFileProvider,
                             ENFRecord npc,
                             Texture2D npcGraphic)
            : base(nativeGraphicsManager, dialogButtonService, DialogType.QuestProgressHistory)
        {
            _npc = npc;
            _npcGraphic = npcGraphic;
            _npcSourceProvider = npcSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;

            Title = $"{_npc.Name} (ID: {_npc.ID})";
            Buttons = ScrollingListDialogButtons.Ok;
            ListItemType = ListDialogItem.ListItemStyle.Small;

            AddNpcInfoToList();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            base.OnUpdateControl(gameTime);

            // Poll for source data (server responds asynchronously)
            var currentHash = _npcSourceProvider.Drops.Count +
                              _npcSourceProvider.ShopItems.Count +
                              _npcSourceProvider.CraftRecipes.Count +
                              _npcSourceProvider.SpawnMaps.Count;

            if (!_sourcesChecked &&
                _npcSourceProvider.NpcId == _npc.ID &&
                currentHash > 0 &&
                currentHash != _lastDataHash)
            {
                _sourcesChecked = true;
                _lastDataHash = currentHash;
                AddSourcesSection();
            }
        }

        private void AddSourcesSection()
        {
            var provider = _npcSourceProvider;

            // Add spacing before acquisition section
            AddEmptyLine();

            // Drops section
            if (provider.Drops.Count > 0)
            {
                AddSectionHeader("--- Drops ---");
                foreach (var drop in provider.Drops)
                {
                    var itemName = GetItemName(drop.ItemId);
                    var amountStr = drop.MinAmount == drop.MaxAmount
                        ? $"{drop.MinAmount}"
                        : $"{drop.MinAmount}-{drop.MaxAmount}";
                    AddInfoLine("", $"{itemName} x{amountStr} ({drop.DropRate:F1}%)");
                }
                AddEmptyLine();
            }

            // Shop items section
            if (provider.ShopItems.Count > 0)
            {
                AddSectionHeader("--- Sells ---");
                foreach (var item in provider.ShopItems.Where(s => s.BuyPrice > 0))
                {
                    var itemName = GetItemName(item.ItemId);
                    AddInfoLine("", $"{itemName} - {item.BuyPrice}g");
                }
                if (provider.ShopItems.Any(s => s.SellPrice > 0))
                {
                    AddEmptyLine();
                    AddSectionHeader("--- Buys ---");
                    foreach (var item in provider.ShopItems.Where(s => s.SellPrice > 0))
                    {
                        var itemName = GetItemName(item.ItemId);
                        AddInfoLine("", $"{itemName} - {item.SellPrice}g");
                    }
                }
                AddEmptyLine();
            }

            // Craft recipes section
            if (provider.CraftRecipes.Count > 0)
            {
                AddSectionHeader("--- Crafts ---");
                foreach (var craft in provider.CraftRecipes)
                {
                    var craftedName = GetItemName(craft.ItemId);
                    var ingredients = string.Join(", ",
                        craft.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                    AddInfoLine("", craftedName);
                    if (!string.IsNullOrEmpty(ingredients))
                        AddInfoLine("  Needs", ingredients);
                }
                AddEmptyLine();
            }

            // Spawn locations section
            if (provider.SpawnMaps.Count > 0)
            {
                AddSectionHeader("--- Spawns On ---");
                foreach (var mapId in provider.SpawnMaps)
                {
                    AddInfoLine("", $"Map {mapId}");
                }
            }
        }

        private void AddEmptyLine()
        {
            AddItemToList(new ListDialogItem(this, ListDialogItem.ListItemStyle.Small) { PrimaryText = " " }, sortList: false);
        }

        private void AddSectionHeader(string header)
        {
            var item = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small)
            {
                PrimaryText = header
            };
            AddItemToList(item, sortList: false);
        }

        private string GetItemName(int itemId)
        {
            if (itemId > 0 && itemId < _eifFileProvider.EIFFile.Length)
                return _eifFileProvider.EIFFile[itemId].Name;
            return $"Item #{itemId}";
        }

        private void AddNpcInfoToList()
        {
            // Add empty line if we have an NPC graphic (for spacing)
            if (_npcGraphic != null)
            {
                AddEmptyLine();
                AddEmptyLine(); // Extra line for taller NPC graphics
            }

            // Type
            AddInfoLine("Type", GetNpcTypeName(_npc.Type));

            // Combat stats
            if (_npc.HP > 0) AddInfoLine("HP", _npc.HP.ToString());
            if (_npc.MinDam > 0 || _npc.MaxDam > 0) AddInfoLine("Damage", $"{_npc.MinDam} - {_npc.MaxDam}");
            if (_npc.Accuracy > 0) AddInfoLine("Accuracy", _npc.Accuracy.ToString());
            if (_npc.Evade > 0) AddInfoLine("Evade", _npc.Evade.ToString());
            if (_npc.Armor > 0) AddInfoLine("Armor", _npc.Armor.ToString());
            if (_npc.Exp > 0) AddInfoLine("EXP", _npc.Exp.ToString());

            // Boss/Child info
            if (_npc.Boss > 0) AddInfoLine("Boss", "Yes");
            if (_npc.Child > 0) AddInfoLine("Child NPC", _npc.Child.ToString());

            // Element weakness
            if (_npc.ElementWeak > 0)
            {
                var elementName = GetElementName(_npc.ElementWeak);
                AddInfoLine("Weak to", $"{elementName} ({_npc.ElementWeakPower}%)");
            }
        }

        private void AddInfoLine(string key, string value)
        {
            var text = string.IsNullOrEmpty(key) ? value : $"{key}: {value}";
            var item = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small)
            {
                PrimaryText = text
            };
            AddItemToList(item, sortList: false);
        }

        private static string GetNpcTypeName(NPCType type)
        {
            return type switch
            {
                NPCType.NPC => "NPC",
                NPCType.Passive => "Passive",
                NPCType.Aggressive => "Aggressive",
                NPCType.Unknown1 => "Unknown",
                NPCType.Unknown2 => "Unknown",
                NPCType.Unknown3 => "Unknown",
                NPCType.Shop => "Shop",
                NPCType.Inn => "Inn",
                NPCType.Unknown4 => "Unknown",
                NPCType.Bank => "Bank",
                NPCType.Barber => "Barber",
                NPCType.Guild => "Guild",
                NPCType.Priest => "Priest",
                NPCType.Law => "Law",
                NPCType.Skills => "Skill Master",
                NPCType.Quest => "Quest",
                _ => type.ToString()
            };
        }

        private static string GetElementName(int element)
        {
            return element switch
            {
                1 => "Light",
                2 => "Dark",
                3 => "Earth",
                4 => "Air",
                5 => "Water",
                6 => "Fire",
                _ => $"Element {element}"
            };
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            base.OnDrawControl(gameTime);

            // Draw NPC graphic on top of the dialog
            if (_npcGraphic != null)
            {
                _spriteBatch.Begin();
                var npcX = DrawAreaWithParentOffset.X + (DrawArea.Width - _npcGraphic.Width) / 2;
                var npcY = DrawAreaWithParentOffset.Y + NpcImageY;
                _spriteBatch.Draw(_npcGraphic, new Vector2(npcX, npcY), Color.White);
                _spriteBatch.End();
            }
        }
    }
}
