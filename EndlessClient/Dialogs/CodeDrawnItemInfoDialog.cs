using System.Linq;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A procedurally-drawn item info dialog that displays item properties and acquisition sources.
    /// </summary>
    public class CodeDrawnItemInfoDialog : CodeDrawnScrollingListDialog
    {
        private readonly EIFRecord _item;
        private readonly Texture2D _itemGraphic;
        private readonly IItemSourceProvider _itemSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IUIStyleProvider _styleProvider;
        private bool _sourcesChecked;
        private int _lastSourceCount;

        public CodeDrawnItemInfoDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IContentProvider contentProvider,
            IItemSourceProvider itemSourceProvider,
            IEIFFileProvider eifFileProvider,
            IENFFileProvider enfFileProvider,
            INativeGraphicsManager nativeGraphicsManager,
            EIFRecord item)
            : base(styleProvider, gameStateProvider, contentProvider.Fonts[Constants.FontSize08])
        {
            _styleProvider = styleProvider;
            _item = item;
            _itemSourceProvider = itemSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;

            // Load item graphic - use inventory icon (even index = 2*Graphic), not ground graphic (odd = 2*Graphic-1)
            _itemGraphic = item.Graphic > 0
                ? nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Graphic, transparent: true)
                : null;

            // Configure dialog
            DialogWidth = 320;
            DialogHeight = 360;
            ListAreaTop = _itemGraphic != null ? 90 : 45;
            ListAreaHeight = DialogHeight - ListAreaTop - 50;

            Title = $"{_item.Name} (ID: {_item.ID})";
            SetupButtons(showOk: true, showCancel: false);

            AddItemInfoToList();
            UpdateScrollBarLayout();
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

        private void AddItemInfoToList()
        {
            // Type
            AddItem($"Type: {GetItemTypeName(_item.Type)}");
            if (_item.SubType != ItemSubType.None)
                AddItem($"Subtype: {_item.SubType}");

            // Stats section
            if (_item.HP > 0) AddItem($"HP: +{_item.HP}");
            if (_item.TP > 0) AddItem($"TP: +{_item.TP}");
            if (_item.MinDam > 0 || _item.MaxDam > 0) AddItem($"Damage: {_item.MinDam} - {_item.MaxDam}");
            if (_item.Accuracy > 0) AddItem($"Accuracy: +{_item.Accuracy}");
            if (_item.Evade > 0) AddItem($"Evade: +{_item.Evade}");
            if (_item.Armor > 0) AddItem($"Armor: +{_item.Armor}");

            // Stat bonuses
            if (_item.Str > 0) AddItem($"STR: +{_item.Str}");
            if (_item.Int > 0) AddItem($"INT: +{_item.Int}");
            if (_item.Wis > 0) AddItem($"WIS: +{_item.Wis}");
            if (_item.Agi > 0) AddItem($"AGI: +{_item.Agi}");
            if (_item.Con > 0) AddItem($"CON: +{_item.Con}");
            if (_item.Cha > 0) AddItem($"CHA: +{_item.Cha}");

            // Element bonuses
            if (_item.Light > 0) AddItem($"Light: +{_item.Light}");
            if (_item.Dark > 0) AddItem($"Dark: +{_item.Dark}");
            if (_item.Earth > 0) AddItem($"Earth: +{_item.Earth}");
            if (_item.Air > 0) AddItem($"Air: +{_item.Air}");
            if (_item.Water > 0) AddItem($"Water: +{_item.Water}");
            if (_item.Fire > 0) AddItem($"Fire: +{_item.Fire}");

            // Requirements section
            if (_item.LevelReq > 0) AddItem($"Level Req: {_item.LevelReq}");
            if (_item.ClassReq > 0) AddItem($"Class Req: Class {_item.ClassReq}");
            if (_item.StrReq > 0) AddItem($"STR Req: {_item.StrReq}");
            if (_item.IntReq > 0) AddItem($"INT Req: {_item.IntReq}");
            if (_item.WisReq > 0) AddItem($"WIS Req: {_item.WisReq}");
            if (_item.AgiReq > 0) AddItem($"AGI Req: {_item.AgiReq}");
            if (_item.ConReq > 0) AddItem($"CON Req: {_item.ConReq}");
            if (_item.ChaReq > 0) AddItem($"CHA Req: {_item.ChaReq}");

            // Special properties
            if (_item.Special != ItemSpecial.Normal)
                AddItem($"Special: {_item.Special}");
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

            // Add spacing
            AddItem(" ");

            // Places to Purchase
            if (shops.Any())
            {
                AddItem("--- Purchase From ---");
                foreach (var source in shops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddItem($"  {npcName} - {source.Price}g");
                }
                AddItem(" ");
            }

            // Places to Craft
            if (crafts.Any())
            {
                AddItem("--- Craft At ---");
                foreach (var source in crafts)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddItem($"  {npcName}");
                    if (source.Ingredients.Any())
                    {
                        var ingredientList = string.Join(", ",
                            source.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                        AddItem($"    Needs: {ingredientList}");
                    }
                }
                AddItem(" ");
            }

            // Dropped By
            if (drops.Any())
            {
                AddItem("--- Dropped By ---");
                foreach (var source in drops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddItem($"  {npcName} ({source.DropRate:F1}%)");
                }
            }
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
            // Let base class draw all standard dialog elements (background, title, list, buttons)
            base.OnDrawControl(gameTime);

            // Draw item graphic on top of the dialog background, but below list area
            if (_itemGraphic != null)
            {
                var drawPos = DrawAreaWithParentOffset;
                var titleBarHeight = _styleProvider.TitleBarHeight;

                // Scale to fit within max bounds while preserving aspect ratio
                const int maxWidth = 80;
                const int maxHeight = 60;

                var scale = 1.0f;
                if (_itemGraphic.Width > maxWidth || _itemGraphic.Height > maxHeight)
                {
                    var scaleX = (float)maxWidth / _itemGraphic.Width;
                    var scaleY = (float)maxHeight / _itemGraphic.Height;
                    scale = System.Math.Min(scaleX, scaleY);
                }

                var scaledWidth = (int)(_itemGraphic.Width * scale);
                var scaledHeight = (int)(_itemGraphic.Height * scale);

                _spriteBatch.Begin();
                var itemX = drawPos.X + (DialogWidth - scaledWidth) / 2;
                var itemY = drawPos.Y + titleBarHeight + 10 + (maxHeight - scaledHeight) / 2; // Center vertically in header area
                var destRect = new Rectangle((int)itemX, (int)itemY, scaledWidth, scaledHeight);
                _spriteBatch.Draw(_itemGraphic, destRect, Color.White);
                _spriteBatch.End();
            }
        }
    }
}
