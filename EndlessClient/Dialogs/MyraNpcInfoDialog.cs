using System;
using System.Linq;
using EndlessClient.Rendering;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based NPC info dialog. Displays NPC properties, combat stats,
    /// and related info (drops, shop items, crafts, spawns) in a scrollable list.
    /// The NPC graphic is drawn via PostRenderOverlay on the Myra window.
    /// </summary>
    public class MyraNpcInfoDialog : MyraDialogAdapter
    {
        private readonly ENFRecord _npc;
        private readonly Texture2D _npcGraphic;
        private readonly INpcSourceProvider _npcSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IClientWindowSizeProvider _windowSizeProvider;
        private readonly IMyraUIManager _uiManager;
        private readonly SpriteBatch _spriteBatch;

        private readonly VerticalStackPanel _contentPanel;
        private bool _sourcesChecked;
        private int _lastDataHash;
        private bool _disposed;

        public MyraNpcInfoDialog(
            IMyraUIManager uiManager,
            IClientWindowSizeProvider windowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager nativeGraphicsManager,
            INpcSourceProvider npcSourceProvider,
            IEIFFileProvider eifFileProvider,
            ENFRecord npc)
            : base(uiManager, $"{npc.Name} (ID: {npc.ID})")
        {
            _npc = npc;
            _npcSourceProvider = npcSourceProvider;
            _eifFileProvider = eifFileProvider;
            _windowSizeProvider = windowSizeProvider;
            _uiManager = uiManager;
            _spriteBatch = new SpriteBatch(graphicsDeviceProvider.GraphicsDevice);

            // Load NPC graphic: (graphic - 1) * 40 + 1 = standing south frame
            _npcGraphic = npc.Graphic > 0
                ? nativeGraphicsManager.TextureFromResource(GFXTypes.NPC, (npc.Graphic - 1) * 40 + 1, transparent: true)
                : null;

            Window.Width = 320;
            Window.Height = _npcGraphic != null ? 380 : 300;

            _contentPanel = new VerticalStackPanel { Spacing = 2 };

            AddNpcInfoToList();

            var scrollViewer = new ScrollViewer
            {
                Content = _contentPanel
            };

            var okButton = new TextButton { Text = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Center };
            okButton.Click += (_, _) => Close(XNAControls.XNADialogResult.OK);

            var grid = new Grid();

            if (_npcGraphic != null)
            {
                // Row 0: fixed-height spacer for the NPC graphic overlay
                grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 100));
                Grid.SetRow(scrollViewer, 1);
                Grid.SetRow(okButton, 2);
            }
            else
            {
                Grid.SetRow(scrollViewer, 0);
                Grid.SetRow(okButton, 1);
            }

            grid.RowsProportions.Add(new Proportion(ProportionType.Fill));  // scroll area
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // button

            grid.Widgets.Add(scrollViewer);
            grid.Widgets.Add(okButton);

            Window.Content = grid;

            // Register overlay for NPC graphic, and clean up on close
            if (_npcGraphic != null)
            {
                _uiManager.PostRenderOverlay += DrawNpcGraphicOverlay;
                DialogClosed += (_, _) => _uiManager.PostRenderOverlay -= DrawNpcGraphicOverlay;
            }
        }

        private void AddNpcInfoToList()
        {
            // Type
            AddLine($"Type: {GetNpcTypeName(_npc.Type)}");

            // Combat stats
            if (_npc.HP > 0) AddLine($"HP: {_npc.HP}");
            if (_npc.MinDam > 0 || _npc.MaxDam > 0) AddLine($"Damage: {_npc.MinDam} - {_npc.MaxDam}");
            if (_npc.Accuracy > 0) AddLine($"Accuracy: {_npc.Accuracy}");
            if (_npc.Evade > 0) AddLine($"Evade: {_npc.Evade}");
            if (_npc.Armor > 0) AddLine($"Armor: {_npc.Armor}");
            if (_npc.Exp > 0) AddLine($"EXP: {_npc.Exp}");

            // Boss/Child info
            if (_npc.Boss > 0) AddLine("Boss: Yes");
            if (_npc.Child > 0) AddLine($"Child NPC: {_npc.Child}");

            // Element weakness
            if (_npc.ElementWeak > 0)
            {
                var elementName = GetElementName(_npc.ElementWeak);
                AddLine($"Weak to: {elementName} ({_npc.ElementWeakPower}%)");
            }
        }

        private void CheckForSources()
        {
            if (_sourcesChecked)
                return;

            var currentHash = _npcSourceProvider.Drops.Count +
                              _npcSourceProvider.ShopItems.Count +
                              _npcSourceProvider.CraftRecipes.Count +
                              _npcSourceProvider.SpawnMaps.Count;

            if (_npcSourceProvider.NpcId == _npc.ID &&
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

            AddLine(" ");

            // Drops
            if (provider.Drops.Count > 0)
            {
                AddLine("--- Drops ---");
                foreach (var drop in provider.Drops)
                {
                    var itemName = GetItemName(drop.ItemId);
                    var amountStr = drop.MinAmount == drop.MaxAmount
                        ? $"{drop.MinAmount}"
                        : $"{drop.MinAmount}-{drop.MaxAmount}";
                    AddLine($"  {itemName} x{amountStr} ({drop.DropRate:F1}%)");
                }
                AddLine(" ");
            }

            // Shop items
            if (provider.ShopItems.Count > 0)
            {
                var sellItems = provider.ShopItems.Where(s => s.BuyPrice > 0).ToList();
                if (sellItems.Any())
                {
                    AddLine("--- Sells ---");
                    foreach (var item in sellItems)
                    {
                        var itemName = GetItemName(item.ItemId);
                        AddLine($"  {itemName} - {item.BuyPrice}g");
                    }
                }

                var buyItems = provider.ShopItems.Where(s => s.SellPrice > 0).ToList();
                if (buyItems.Any())
                {
                    AddLine(" ");
                    AddLine("--- Buys ---");
                    foreach (var item in buyItems)
                    {
                        var itemName = GetItemName(item.ItemId);
                        AddLine($"  {itemName} - {item.SellPrice}g");
                    }
                }
                AddLine(" ");
            }

            // Craft recipes
            if (provider.CraftRecipes.Count > 0)
            {
                AddLine("--- Crafts ---");
                foreach (var craft in provider.CraftRecipes)
                {
                    var craftedName = GetItemName(craft.ItemId);
                    var ingredients = string.Join(", ",
                        craft.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                    AddLine($"  {craftedName}");
                    if (!string.IsNullOrEmpty(ingredients))
                        AddLine($"    Needs: {ingredients}");
                }
                AddLine(" ");
            }

            // Spawn locations
            if (provider.SpawnMaps.Count > 0)
            {
                AddLine("--- Spawns On ---");
                foreach (var mapId in provider.SpawnMaps)
                {
                    AddLine($"  Map {mapId}");
                }
            }
        }

        private void AddLine(string text)
        {
            _contentPanel.Widgets.Add(new Label { Text = text });
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

        private void DrawNpcGraphicOverlay()
        {
            if (_npcGraphic == null || !Window.Visible)
                return;

            // Poll for async source data during the overlay draw
            CheckForSources();

            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            int winX = (int)(Window.Left * scale) + offset.X;
            int winY = (int)(Window.Top * scale) + offset.Y;
            int windowWidth = (int)(Window.Bounds.Width * scale);

            if (windowWidth <= 0)
                return;

            // Scale graphic to fit within max bounds while preserving aspect ratio
            const int maxWidth = 120;
            const int maxHeight = 90;

            var graphicScale = 1.0f;
            if (_npcGraphic.Width > maxWidth || _npcGraphic.Height > maxHeight)
            {
                var scaleX = (float)maxWidth / _npcGraphic.Width;
                var scaleY = (float)maxHeight / _npcGraphic.Height;
                graphicScale = Math.Min(scaleX, scaleY);
            }

            var scaledWidth = (int)(_npcGraphic.Width * graphicScale * scale);
            var scaledHeight = (int)(_npcGraphic.Height * graphicScale * scale);

            // Center horizontally within the window, positioned below title bar
            var titleBarHeight = 25;
            var npcX = winX + (windowWidth - scaledWidth) / 2;
            var npcY = winY + (int)((titleBarHeight + 10) * scale);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_npcGraphic, new Rectangle(npcX, npcY, scaledWidth, scaledHeight), Color.White);
            _spriteBatch.End();
        }

        public new void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_npcGraphic != null)
            {
                _uiManager.PostRenderOverlay -= DrawNpcGraphicOverlay;
            }

            _spriteBatch?.Dispose();
            base.Dispose();
        }
    }
}
