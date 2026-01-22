using System.Collections.Generic;
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
    public class ItemInfoDialog : ScrollingListDialog
    {
        private readonly EIFRecord _item;
        private readonly Texture2D _itemGraphic;
        private readonly IItemSourceProvider _itemSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private const int ItemImageY = 45;
        private bool _sourcesChecked;
        private int _lastSourceCount;

        public ItemInfoDialog(INativeGraphicsManager nativeGraphicsManager,
                              IEODialogButtonService dialogButtonService,
                              IItemSourceProvider itemSourceProvider,
                              IEIFFileProvider eifFileProvider,
                              IENFFileProvider enfFileProvider,
                              EIFRecord item,
                              Texture2D itemGraphic)
            : base(nativeGraphicsManager, dialogButtonService, DialogType.QuestProgressHistory) // Medium size dialog
        {
            _item = item;
            _itemGraphic = itemGraphic;
            _itemSourceProvider = itemSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;

            Title = $"{_item.Name} (ID: {_item.ID})";
            Buttons = ScrollingListDialogButtons.Ok;
            ListItemType = ListDialogItem.ListItemStyle.Small;

            AddItemInfoToList();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            base.OnUpdateControl(gameTime);

            // Poll for source data (server responds asynchronously)
            if (!_sourcesChecked &&
                _itemSourceProvider.ItemId == _item.ID &&
                _itemSourceProvider.Sources.Count > 0 &&
                _itemSourceProvider.Sources.Count != _lastSourceCount)
            {
                _sourcesChecked = true;
                _lastSourceCount = _itemSourceProvider.Sources.Count;
                AddSourcesSection();
            }
        }

        private void AddSourcesSection()
        {
            if (_itemSourceProvider.Sources.Count == 0)
                return;

            var sources = _itemSourceProvider.Sources;

            // Group sources by type
            var shops = sources.Where(s => s.Type == ItemSourceType.Shop).ToList();
            var crafts = sources.Where(s => s.Type == ItemSourceType.Craft).ToList();
            var drops = sources.Where(s => s.Type == ItemSourceType.Drop).ToList();

            // Add spacing before acquisition section
            AddEmptyLine();

            // Places to Purchase
            if (shops.Any())
            {
                AddSectionHeader("--- Purchase From ---");
                foreach (var source in shops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddInfoLine("", $"{npcName} - {source.Price}g");
                }
                AddEmptyLine();
            }

            // Places to Craft
            if (crafts.Any())
            {
                AddSectionHeader("--- Craft At ---");
                foreach (var source in crafts)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddInfoLine("", npcName);
                    if (source.Ingredients.Any())
                    {
                        var ingredientList = string.Join(", ",
                            source.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                        AddInfoLine("  Needs", ingredientList);
                    }
                }
                AddEmptyLine();
            }

            // Dropped By
            if (drops.Any())
            {
                AddSectionHeader("--- Dropped By ---");
                foreach (var source in drops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddInfoLine("", $"{npcName} ({source.DropRate:F1}%)");
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

        private string GetNpcName(int npcId)
        {
            if (npcId > 0 && npcId < _enfFileProvider.ENFFile.Length)
                return _enfFileProvider.ENFFile[npcId].Name;
            return $"NPC #{npcId}";
        }

        private string GetItemName(int itemId)
        {
            if (itemId > 0 && itemId < _eifFileProvider.EIFFile.Length)
                return _eifFileProvider.EIFFile[itemId].Name;
            return $"Item #{itemId}";
        }

        private void AddItemInfoToList()
        {
            // Add empty line if we have an item graphic (for spacing)
            if (_itemGraphic != null)
            {
                AddEmptyLine();
            }

            // Type
            AddInfoLine("Type", GetItemTypeName(_item.Type));
            if (_item.SubType != ItemSubType.None)
                AddInfoLine("Subtype", _item.SubType.ToString());

            // Stats section
            if (_item.HP > 0) AddInfoLine("HP", $"+{_item.HP}");
            if (_item.TP > 0) AddInfoLine("TP", $"+{_item.TP}");
            if (_item.MinDam > 0 || _item.MaxDam > 0) AddInfoLine("Damage", $"{_item.MinDam} - {_item.MaxDam}");
            if (_item.Accuracy > 0) AddInfoLine("Accuracy", $"+{_item.Accuracy}");
            if (_item.Evade > 0) AddInfoLine("Evade", $"+{_item.Evade}");
            if (_item.Armor > 0) AddInfoLine("Armor", $"+{_item.Armor}");

            // Stat bonuses
            if (_item.Str > 0) AddInfoLine("STR", $"+{_item.Str}");
            if (_item.Int > 0) AddInfoLine("INT", $"+{_item.Int}");
            if (_item.Wis > 0) AddInfoLine("WIS", $"+{_item.Wis}");
            if (_item.Agi > 0) AddInfoLine("AGI", $"+{_item.Agi}");
            if (_item.Con > 0) AddInfoLine("CON", $"+{_item.Con}");
            if (_item.Cha > 0) AddInfoLine("CHA", $"+{_item.Cha}");

            // Element bonuses
            if (_item.Light > 0) AddInfoLine("Light", $"+{_item.Light}");
            if (_item.Dark > 0) AddInfoLine("Dark", $"+{_item.Dark}");
            if (_item.Earth > 0) AddInfoLine("Earth", $"+{_item.Earth}");
            if (_item.Air > 0) AddInfoLine("Air", $"+{_item.Air}");
            if (_item.Water > 0) AddInfoLine("Water", $"+{_item.Water}");
            if (_item.Fire > 0) AddInfoLine("Fire", $"+{_item.Fire}");

            // Requirements section
            if (_item.LevelReq > 0) AddInfoLine("Level Req", _item.LevelReq.ToString());
            if (_item.ClassReq > 0) AddInfoLine("Class Req", $"Class {_item.ClassReq}");
            if (_item.StrReq > 0) AddInfoLine("STR Req", _item.StrReq.ToString());
            if (_item.IntReq > 0) AddInfoLine("INT Req", _item.IntReq.ToString());
            if (_item.WisReq > 0) AddInfoLine("WIS Req", _item.WisReq.ToString());
            if (_item.AgiReq > 0) AddInfoLine("AGI Req", _item.AgiReq.ToString());
            if (_item.ConReq > 0) AddInfoLine("CON Req", _item.ConReq.ToString());
            if (_item.ChaReq > 0) AddInfoLine("CHA Req", _item.ChaReq.ToString());

            // Special properties
            if (_item.Special != ItemSpecial.Normal)
                AddInfoLine("Special", _item.Special.ToString());
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

        private static string GetItemTypeName(ItemType type)
        {
            return type switch
            {
                ItemType.Static => "Static",
                ItemType.Money => "Money",
                ItemType.Heal => "Healing",
                ItemType.Teleport => "Teleport Scroll",
                ItemType.Spell => "Spell Scroll",
                ItemType.EXPReward => "EXP Reward",
                ItemType.StatReward => "Stat Reward",
                ItemType.SkillReward => "Skill Reward",
                ItemType.Key => "Key",
                ItemType.Weapon => "Weapon",
                ItemType.Shield => "Shield",
                ItemType.Armor => "Armor",
                ItemType.Hat => "Hat",
                ItemType.Boots => "Boots",
                ItemType.Gloves => "Gloves",
                ItemType.Accessory => "Accessory",
                ItemType.Belt => "Belt",
                ItemType.Necklace => "Necklace",
                ItemType.Ring => "Ring",
                ItemType.Armlet => "Armlet",
                ItemType.Bracer => "Bracer",
                ItemType.Beer => "Beer",
                ItemType.EffectPotion => "Effect Potion",
                ItemType.HairDye => "Hair Dye",
                ItemType.CureCurse => "Cure Curse",
                _ => type.ToString()
            };
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            base.OnDrawControl(gameTime);

            // Draw item graphic on top of the dialog
            if (_itemGraphic != null)
            {
                _spriteBatch.Begin();
                var itemX = DrawAreaWithParentOffset.X + (DrawArea.Width - _itemGraphic.Width) / 2;
                var itemY = DrawAreaWithParentOffset.Y + ItemImageY;
                _spriteBatch.Draw(_itemGraphic, new Vector2(itemX, itemY), Color.White);
                _spriteBatch.End();
            }
        }
    }
}
