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
    /// Myra-based item info dialog. Displays item properties, stats, requirements,
    /// and acquisition sources (shops, crafts, drops) in a scrollable list.
    /// The item graphic is drawn via PostRenderOverlay on the Myra window.
    /// </summary>
    public class MyraItemInfoDialog : MyraDialogAdapter
    {
        private readonly EIFRecord _item;
        private readonly Texture2D _itemGraphic;
        private readonly IItemSourceProvider _itemSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IClientWindowSizeProvider _windowSizeProvider;
        private readonly IMyraUIManager _uiManager;
        private readonly SpriteBatch _spriteBatch;

        private readonly VerticalStackPanel _contentPanel;
        private bool _sourcesChecked;
        private int _lastSourceCount;
        private bool _disposed;

        public MyraItemInfoDialog(
            IMyraUIManager uiManager,
            IClientWindowSizeProvider windowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager nativeGraphicsManager,
            IItemSourceProvider itemSourceProvider,
            IEIFFileProvider eifFileProvider,
            IENFFileProvider enfFileProvider,
            EIFRecord item)
            : base(uiManager, $"{item.Name} (ID: {item.ID})")
        {
            _item = item;
            _itemSourceProvider = itemSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
            _windowSizeProvider = windowSizeProvider;
            _uiManager = uiManager;
            _spriteBatch = new SpriteBatch(graphicsDeviceProvider.GraphicsDevice);

            // Load item graphic - inventory icon (even index = 2*Graphic)
            _itemGraphic = item.Graphic > 0
                ? nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Graphic, transparent: true)
                : null;

            Window.Width = 320;
            Window.Height = _itemGraphic != null ? 360 : 300;

            _contentPanel = new VerticalStackPanel { Spacing = 2 };

            AddItemInfoToList();

            var scrollViewer = new ScrollViewer
            {
                Content = _contentPanel
            };

            var okButton = new TextButton { Text = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Center };
            okButton.Click += (_, _) => Close(XNAControls.XNADialogResult.OK);

            var grid = new Grid();

            if (_itemGraphic != null)
            {
                // Row 0: fixed-height spacer for the item graphic overlay
                grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 80));
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

            // Register overlay for item graphic, and clean up on close
            if (_itemGraphic != null)
            {
                _uiManager.PostRenderOverlay += DrawItemGraphicOverlay;
                DialogClosed += (_, _) => _uiManager.PostRenderOverlay -= DrawItemGraphicOverlay;
            }
        }

        private void AddItemInfoToList()
        {
            // Type
            AddLine($"Type: {GetItemTypeName(_item.Type)}");
            if (_item.SubType != ItemSubType.None)
                AddLine($"Subtype: {_item.SubType}");

            // Stats section
            if (_item.HP > 0) AddLine($"HP: +{_item.HP}");
            if (_item.TP > 0) AddLine($"TP: +{_item.TP}");
            if (_item.MinDam > 0 || _item.MaxDam > 0) AddLine($"Damage: {_item.MinDam} - {_item.MaxDam}");
            if (_item.Accuracy > 0) AddLine($"Accuracy: +{_item.Accuracy}");
            if (_item.Evade > 0) AddLine($"Evade: +{_item.Evade}");
            if (_item.Armor > 0) AddLine($"Armor: +{_item.Armor}");

            // Stat bonuses
            if (_item.Str > 0) AddLine($"STR: +{_item.Str}");
            if (_item.Int > 0) AddLine($"INT: +{_item.Int}");
            if (_item.Wis > 0) AddLine($"WIS: +{_item.Wis}");
            if (_item.Agi > 0) AddLine($"AGI: +{_item.Agi}");
            if (_item.Con > 0) AddLine($"CON: +{_item.Con}");
            if (_item.Cha > 0) AddLine($"CHA: +{_item.Cha}");

            // Element bonuses
            if (_item.Light > 0) AddLine($"Light: +{_item.Light}");
            if (_item.Dark > 0) AddLine($"Dark: +{_item.Dark}");
            if (_item.Earth > 0) AddLine($"Earth: +{_item.Earth}");
            if (_item.Air > 0) AddLine($"Air: +{_item.Air}");
            if (_item.Water > 0) AddLine($"Water: +{_item.Water}");
            if (_item.Fire > 0) AddLine($"Fire: +{_item.Fire}");

            // Requirements section
            if (_item.LevelReq > 0) AddLine($"Level Req: {_item.LevelReq}");
            if (_item.ClassReq > 0) AddLine($"Class Req: Class {_item.ClassReq}");
            if (_item.StrReq > 0) AddLine($"STR Req: {_item.StrReq}");
            if (_item.IntReq > 0) AddLine($"INT Req: {_item.IntReq}");
            if (_item.WisReq > 0) AddLine($"WIS Req: {_item.WisReq}");
            if (_item.AgiReq > 0) AddLine($"AGI Req: {_item.AgiReq}");
            if (_item.ConReq > 0) AddLine($"CON Req: {_item.ConReq}");
            if (_item.ChaReq > 0) AddLine($"CHA Req: {_item.ChaReq}");

            // Special properties
            if (_item.Special != ItemSpecial.Normal)
                AddLine($"Special: {_item.Special}");
        }

        private void CheckForSources()
        {
            if (_sourcesChecked)
                return;

            if (_itemSourceProvider.ItemId == _item.ID &&
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

            var shops = sources.Where(s => s.Type == ItemSourceType.Shop).ToList();
            var crafts = sources.Where(s => s.Type == ItemSourceType.Craft).ToList();
            var drops = sources.Where(s => s.Type == ItemSourceType.Drop).ToList();

            AddLine(" ");

            if (shops.Any())
            {
                AddLine("--- Purchase From ---");
                foreach (var source in shops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddLine($"  {npcName} - {source.Price}g");
                }
                AddLine(" ");
            }

            if (crafts.Any())
            {
                AddLine("--- Craft At ---");
                foreach (var source in crafts)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddLine($"  {npcName}");
                    if (source.Ingredients.Any())
                    {
                        var ingredientList = string.Join(", ",
                            source.Ingredients.Select(i => $"{i.Amount}x {GetItemName(i.ItemId)}"));
                        AddLine($"    Needs: {ingredientList}");
                    }
                }
                AddLine(" ");
            }

            if (drops.Any())
            {
                AddLine("--- Dropped By ---");
                foreach (var source in drops)
                {
                    var npcName = GetNpcName(source.NpcId);
                    AddLine($"  {npcName} ({source.DropRate:F1}%)");
                }
            }
        }

        private void AddLine(string text)
        {
            _contentPanel.Widgets.Add(new Label { Text = text });
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

        private void DrawItemGraphicOverlay()
        {
            if (_itemGraphic == null || !Window.Visible)
                return;

            // Poll for async source data during the overlay draw (since we don't have an Update loop)
            CheckForSources();

            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            int winX = (int)(Window.Left * scale) + offset.X;
            int winY = (int)(Window.Top * scale) + offset.Y;
            int windowWidth = (int)(Window.Bounds.Width * scale);

            if (windowWidth <= 0)
                return;

            // Scale graphic to fit within max bounds while preserving aspect ratio
            const int maxWidth = 80;
            const int maxHeight = 60;

            var graphicScale = 1.0f;
            if (_itemGraphic.Width > maxWidth || _itemGraphic.Height > maxHeight)
            {
                var scaleX = (float)maxWidth / _itemGraphic.Width;
                var scaleY = (float)maxHeight / _itemGraphic.Height;
                graphicScale = Math.Min(scaleX, scaleY);
            }

            var scaledWidth = (int)(_itemGraphic.Width * graphicScale * scale);
            var scaledHeight = (int)(_itemGraphic.Height * graphicScale * scale);

            // Center horizontally within the window, positioned below title bar
            var titleBarHeight = 25; // Myra window title bar height
            var itemX = winX + (windowWidth - scaledWidth) / 2;
            var itemY = winY + (int)((titleBarHeight + 10) * scale);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_itemGraphic, new Rectangle(itemX, itemY, scaledWidth, scaledHeight), Color.White);
            _spriteBatch.End();
        }

        public new void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_itemGraphic != null)
            {
                _uiManager.PostRenderOverlay -= DrawItemGraphicOverlay;
            }

            _spriteBatch?.Dispose();
            base.Dispose();
        }
    }
}
