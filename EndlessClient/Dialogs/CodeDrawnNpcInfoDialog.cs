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
    /// A procedurally-drawn NPC info dialog that displays NPC properties and related info (drops, shop, crafts, spawns).
    /// </summary>
    public class CodeDrawnNpcInfoDialog : CodeDrawnScrollingListDialog
    {
        private readonly ENFRecord _npc;
        private readonly Texture2D _npcGraphic;
        private readonly INpcSourceProvider _npcSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IUIStyleProvider _styleProvider;
        private bool _sourcesChecked;
        private int _lastDataHash;

        public CodeDrawnNpcInfoDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IContentProvider contentProvider,
            INpcSourceProvider npcSourceProvider,
            IEIFFileProvider eifFileProvider,
            INativeGraphicsManager nativeGraphicsManager,
            ENFRecord npc)
            : base(styleProvider, gameStateProvider, contentProvider.Fonts[Constants.FontSize08])
        {
            _styleProvider = styleProvider;
            _npc = npc;
            _npcSourceProvider = npcSourceProvider;
            _eifFileProvider = eifFileProvider;

            // Load NPC graphic
            // Formula: (graphic - 1) * 40 + frame_offset (1 = standing south)
            _npcGraphic = npc.Graphic > 0
                ? nativeGraphicsManager.TextureFromResource(GFXTypes.NPC, (npc.Graphic - 1) * 40 + 1, transparent: true)
                : null;

            // Configure dialog - use larger height for NPC graphics
            DialogWidth = 320;
            DialogHeight = 380;
            ListAreaTop = _npcGraphic != null ? 120 : 45;
            ListAreaHeight = DialogHeight - ListAreaTop - 50;

            Title = $"{_npc.Name} (ID: {_npc.ID})";
            SetupButtons(showOk: true, showCancel: false);

            AddNpcInfoToList();
            UpdateScrollBarLayout();
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

        private void AddNpcInfoToList()
        {
            // Type
            AddItem($"Type: {GetNpcTypeName(_npc.Type)}");

            // Combat stats
            if (_npc.HP > 0) AddItem($"HP: {_npc.HP}");
            if (_npc.MinDam > 0 || _npc.MaxDam > 0) AddItem($"Damage: {_npc.MinDam} - {_npc.MaxDam}");
            if (_npc.Accuracy > 0) AddItem($"Accuracy: {_npc.Accuracy}");
            if (_npc.Evade > 0) AddItem($"Evade: {_npc.Evade}");
            if (_npc.Armor > 0) AddItem($"Armor: {_npc.Armor}");
            if (_npc.Exp > 0) AddItem($"EXP: {_npc.Exp}");

            // Boss/Child info
            if (_npc.Boss > 0) AddItem("Boss: Yes");
            if (_npc.Child > 0) AddItem($"Child NPC: {_npc.Child}");

            // Element weakness
            if (_npc.ElementWeak > 0)
            {
                var elementName = GetElementName(_npc.ElementWeak);
                AddItem($"Weak to: {elementName} ({_npc.ElementWeakPower}%)");
            }
        }

        private void AddSourcesSection()
        {
            var provider = _npcSourceProvider;

            // Add spacing
            AddItem(" ");

            // Drops section
            if (provider.Drops.Count > 0)
            {
                AddItem("--- Drops ---");
                foreach (var drop in provider.Drops)
                {
                    var itemName = GetItemName(drop.ItemId);
                    var amountStr = drop.MinAmount == drop.MaxAmount
                        ? $"{drop.MinAmount}"
                        : $"{drop.MinAmount}-{drop.MaxAmount}";
                    AddItem($"  {itemName} x{amountStr} ({drop.DropRate:F1}%)");
                }
                AddItem(" ");
            }

            // Shop items section
            if (provider.ShopItems.Count > 0)
            {
                var sellItems = provider.ShopItems.Where(s => s.BuyPrice > 0).ToList();
                if (sellItems.Any())
                {
                    AddItem("--- Sells ---");
                    foreach (var item in sellItems)
                    {
                        var itemName = GetItemName(item.ItemId);
                        AddItem($"  {itemName} - {item.BuyPrice}g");
                    }
                }

                var buyItems = provider.ShopItems.Where(s => s.SellPrice > 0).ToList();
                if (buyItems.Any())
                {
                    AddItem(" ");
                    AddItem("--- Buys ---");
                    foreach (var item in buyItems)
                    {
                        var itemName = GetItemName(item.ItemId);
                        AddItem($"  {itemName} - {item.SellPrice}g");
                    }
                }
                AddItem(" ");
            }

            // Craft recipes section
            if (provider.CraftRecipes.Count > 0)
            {
                AddItem("--- Crafts ---");
                foreach (var craft in provider.CraftRecipes)
                {
                    var craftedName = GetItemName(craft.ItemId);
                    var ingredients = string.Join(", ",
                        craft.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                    AddItem($"  {craftedName}");
                    if (!string.IsNullOrEmpty(ingredients))
                        AddItem($"    Needs: {ingredients}");
                }
                AddItem(" ");
            }

            // Spawn locations section
            if (provider.SpawnMaps.Count > 0)
            {
                AddItem("--- Spawns On ---");
                foreach (var mapId in provider.SpawnMaps)
                {
                    AddItem($"  Map {mapId}");
                }
            }
        }

        private string GetItemName(int itemId)
        {
            if (itemId > 0 && itemId < _eifFileProvider.EIFFile.Length)
                return _eifFileProvider.EIFFile[itemId].Name;
            return $"Item #{itemId}";
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
            // Let base class draw all standard dialog elements (background, title, list, buttons)
            base.OnDrawControl(gameTime);

            // Draw NPC graphic on top of the dialog background
            if (_npcGraphic != null)
            {
                var drawPos = DrawAreaWithParentOffset;
                var titleBarHeight = _styleProvider.TitleBarHeight;

                // Scale to fit within max bounds while preserving aspect ratio
                const int maxWidth = 120;
                const int maxHeight = 90;

                var scale = 1.0f;
                if (_npcGraphic.Width > maxWidth || _npcGraphic.Height > maxHeight)
                {
                    var scaleX = (float)maxWidth / _npcGraphic.Width;
                    var scaleY = (float)maxHeight / _npcGraphic.Height;
                    scale = System.Math.Min(scaleX, scaleY);
                }

                var scaledWidth = (int)(_npcGraphic.Width * scale);
                var scaledHeight = (int)(_npcGraphic.Height * scale);

                _spriteBatch.Begin();
                var npcX = drawPos.X + (DialogWidth - scaledWidth) / 2;
                var npcY = drawPos.Y + titleBarHeight + 10 + (maxHeight - scaledHeight) / 2; // Center vertically in header area
                var destRect = new Rectangle((int)npcX, (int)npcY, scaledWidth, scaledHeight);
                _spriteBatch.Draw(_npcGraphic, destRect, Color.White);
                _spriteBatch.End();
            }
        }
    }
}
