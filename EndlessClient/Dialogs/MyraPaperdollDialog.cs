using System;
using System.Collections.Generic;
using EndlessClient.Audio;
using EndlessClient.Controllers;
using EndlessClient.Dialogs.Extensions;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Item;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using Optional;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based paperdoll dialog — displays character info and equipped items.
    /// Left side: equipment slot grid. Right side: character info labels.
    /// </summary>
    public class MyraPaperdollDialog : MyraDialogAdapter
    {
        private readonly IMyraUIManager _uiManager;
        private readonly IMyraFontProvider _fontProvider;
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly IInventoryController _inventoryController;
        private readonly IPaperdollProvider _paperdollProvider;
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IItemStringService _itemStringService;
        private readonly bool _isMainCharacter;
        private readonly Character _character;

        // Info labels (right side)
        private readonly Label _nameValue, _classValue, _titleValue, _partnerValue, _homeValue, _guildValue, _rankValue;

        // Equipment grid (left side) — container for slot widgets
        private readonly Panel _equipPanel;
        private readonly List<Widget> _equipmentSlots = new();

        private Option<PaperdollData> _cachedPaperdollData = Option.None<PaperdollData>();

        // Floating tooltip for equipment items
        private readonly Panel _tooltipPanel;
        private readonly Label _tooltipLabel;
        private bool _isHoveringSlot;

        public MyraPaperdollDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            INativeGraphicsManager graphicsManager,
            IInventoryController inventoryController,
            IPaperdollProvider paperdollProvider,
            IPubFileProvider pubFileProvider,
            IInventorySpaceValidator inventorySpaceValidator,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ISfxPlayer sfxPlayer,
            IItemStringService itemStringService,
            Character character,
            bool isMainCharacter)
            : base(uiManager, "Paperdoll")
        {
            _uiManager = uiManager;
            _fontProvider = fontProvider;
            _graphicsManager = graphicsManager;
            _inventoryController = inventoryController;
            _paperdollProvider = paperdollProvider;
            _pubFileProvider = pubFileProvider;
            _inventorySpaceValidator = inventorySpaceValidator;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _sfxPlayer = sfxPlayer;
            _itemStringService = itemStringService;
            _character = character;
            _isMainCharacter = isMainCharacter;

            Window.Width = 420;
            Window.Height = 360;
            Window.TitleFont = fontProvider.Header;

            // --- Equipment area (left side) ---
            _equipPanel = new Panel
            {
                Width = 220,
                Height = 280
            };

            // --- Info labels (right side) ---
            var infoPanel = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(4)
            };

            _nameValue = CreateInfoRow(infoPanel, "Name:");
            _classValue = CreateInfoRow(infoPanel, "Class:");
            _titleValue = CreateInfoRow(infoPanel, "Title:");
            _partnerValue = CreateInfoRow(infoPanel, "Partner:");
            _homeValue = CreateInfoRow(infoPanel, "Home:");
            _guildValue = CreateInfoRow(infoPanel, "Guild:");
            _rankValue = CreateInfoRow(infoPanel, "Rank:");

            // --- Two-column layout ---
            var columnsPanel = new HorizontalStackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            columnsPanel.Widgets.Add(_equipPanel);
            columnsPanel.Widgets.Add(infoPanel);
            columnsPanel.Proportions.Add(new Proportion(ProportionType.Auto));
            columnsPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // --- OK button ---
            var okButton = new Button
            {
                Content = new Label { Text = "OK", Font = fontProvider.Normal },
                Width = 72,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okButton.Click += (_, _) => Close(XNADialogResult.OK);

            // --- Main layout ---
            var mainPanel = new VerticalStackPanel
            {
                Spacing = 4
            };
            mainPanel.Widgets.Add(columnsPanel);
            mainPanel.Widgets.Add(okButton);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;

            // Floating tooltip — sits on the Desktop, positioned per frame
            _tooltipLabel = new Label
            {
                Font = fontProvider.Normal,
                TextColor = new Color(220, 220, 230),
            };
            _tooltipPanel = new Panel
            {
                Visible = false,
                Background = new SolidBrush(new Color(20, 20, 30, 230)),
                Border = new SolidBrush(new Color(80, 80, 100)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ZIndex = 10000,
            };
            _tooltipPanel.Widgets.Add(_tooltipLabel);

            // Poll paperdoll data and tooltip position every frame
            Window.BeforeRender += _ =>
            {
                PollPaperdollData();
                UpdateTooltipPosition();
            };

            // Clean up tooltip when dialog closes
            DialogClosed += (_, _) =>
            {
                if (uiManager.Desktop.Widgets.Contains(_tooltipPanel))
                    uiManager.Desktop.Widgets.Remove(_tooltipPanel);
            };
        }

        private Label CreateInfoRow(VerticalStackPanel parent, string labelText)
        {
            var valueLabel = new Label { Text = "", Font = _fontProvider.Normal };

            var row = new HorizontalStackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.Widgets.Add(new Label
            {
                Text = labelText,
                Font = _fontProvider.Normal,
                Width = 58,
                TextColor = new Color(180, 180, 180)
            });
            row.Widgets.Add(valueLabel);

            parent.Widgets.Add(row);
            return valueLabel;
        }

        private void PollPaperdollData()
        {
            var characterId = _character.ID;
            if (!_paperdollProvider.VisibleCharacterPaperdolls.TryGetValue(characterId, out var paperdollData))
                return;

            var needsUpdate = false;
            _cachedPaperdollData.Match(
                some: cached =>
                {
                    if (!cached.Equals(paperdollData))
                        needsUpdate = true;
                },
                none: () => needsUpdate = true
            );

            if (needsUpdate)
            {
                UpdateDisplayedData(paperdollData);
                _cachedPaperdollData = Option.Some(paperdollData);
            }
        }

        private void UpdateDisplayedData(PaperdollData paperdollData)
        {
            // Update info labels
            _nameValue.Text = paperdollData.Name ?? "";
            _homeValue.Text = paperdollData.Home ?? "";
            _partnerValue.Text = paperdollData.Partner ?? "";
            _titleValue.Text = paperdollData.Title ?? "";
            _guildValue.Text = paperdollData.Guild ?? "";
            _rankValue.Text = paperdollData.Rank ?? "";

            // Get class name from pub file
            if (paperdollData.Class > 0)
            {
                var classRec = _pubFileProvider.ECFFile[paperdollData.Class];
                _classValue.Text = classRec?.Name ?? "";
            }
            else
            {
                _classValue.Text = "";
            }

            // Rebuild equipment slots
            foreach (var slot in _equipmentSlots)
                _equipPanel.Widgets.Remove(slot);
            _equipmentSlots.Clear();

            foreach (EquipLocation equipLocation in Enum.GetValues(typeof(EquipLocation)))
            {
                if (equipLocation == EquipLocation.PAPERDOLL_MAX) break;

                var slotRect = equipLocation.GetEquipLocationRectangle();

                // Create empty slot background
                var slotPanel = new Panel
                {
                    Left = slotRect.X,
                    Top = slotRect.Y,
                    Width = slotRect.Width,
                    Height = slotRect.Height,
                    Background = new SolidBrush(new Color(40, 40, 50, 120)),
                    Border = new SolidBrush(new Color(80, 80, 100)),
                    BorderThickness = new Thickness(1)
                };

                if (!paperdollData.Paperdoll.ContainsKey(equipLocation))
                {
                    _equipPanel.Widgets.Add(slotPanel);
                    _equipmentSlots.Add(slotPanel);
                    continue;
                }

                var itemId = paperdollData.Paperdoll[equipLocation];
                if (itemId <= 0)
                {
                    _equipPanel.Widgets.Add(slotPanel);
                    _equipmentSlots.Add(slotPanel);
                    continue;
                }

                var eifRecord = _pubFileProvider.EIFFile[itemId];
                if (eifRecord == null)
                {
                    _equipPanel.Widgets.Add(slotPanel);
                    _equipmentSlots.Add(slotPanel);
                    continue;
                }

                // Load item icon texture
                try
                {
                    var itemTexture = _graphicsManager.TextureFromResource(GFXTypes.Items, eifRecord.Graphic * 2, transparent: true);
                    if (itemTexture != null)
                    {
                        var img = new Image
                        {
                            Renderable = new TextureRegion(itemTexture),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        slotPanel.Widgets.Add(img);
                    }
                }
                catch { /* gracefully handle missing graphics */ }

                // Hover effect
                var eqLoc = equipLocation;
                var rec = eifRecord;
                slotPanel.MouseEntered += (_, _) =>
                {
                    slotPanel.Background = new SolidBrush(new Color(60, 60, 80, 180));
                    _tooltipLabel.Text = BuildItemTooltipText(rec);
                    _tooltipPanel.Visible = true;
                    _isHoveringSlot = true;
                };
                slotPanel.MouseLeft += (_, _) =>
                {
                    slotPanel.Background = new SolidBrush(new Color(40, 40, 50, 120));
                    _tooltipPanel.Visible = false;
                    _isHoveringSlot = false;
                };

                // Right-click to unequip (only for main character)
                if (_isMainCharacter)
                {
                    slotPanel.TouchDown += (_, _) =>
                    {
                        HandleUnequip(eqLoc, rec);
                    };
                }

                _equipPanel.Widgets.Add(slotPanel);
                _equipmentSlots.Add(slotPanel);
            }
        }

        private void HandleUnequip(EquipLocation location, EOLib.IO.Pub.EIFRecord record)
        {
            if (record.Special == ItemSpecial.Cursed)
            {
                var msgBox = _messageBoxFactory.CreateMessageBox(DialogResourceID.ITEM_IS_CURSED_ITEM, EODialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
                msgBox.ShowDialog();
            }
            else
            {
                if (!_inventorySpaceValidator.ItemFits(record.ID))
                {
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.STATUS_LABEL_ITEM_UNEQUIP_NO_SPACE_LEFT);
                }
                else
                {
                    _inventoryController.UnequipItem(location);
                    _sfxPlayer.PlaySfx(SoundEffectID.InventoryPlace);
                }
            }
        }

        private void UpdateTooltipPosition()
        {
            if (!_tooltipPanel.Visible || !_isHoveringSlot)
                return;

            var desktop = _uiManager.Desktop;
            if (!desktop.Widgets.Contains(_tooltipPanel))
                desktop.Widgets.Add(_tooltipPanel);

            var mousePos = _uiManager.GetLogicalMousePosition();
            _tooltipPanel.Left = (int)(mousePos.X + 16);
            _tooltipPanel.Top = (int)(mousePos.Y + 16);
        }

        private static string BuildItemTooltipText(EIFRecord data)
        {
            var lines = new List<string> { data.Name, $"Type: {data.Type}  Wt: {data.Weight}" };

            if (data.MinDam > 0 || data.MaxDam > 0) lines.Add($"Damage: {data.MinDam}-{data.MaxDam}");
            if (data.Accuracy > 0) lines.Add($"Accuracy: {data.Accuracy}");
            if (data.Evade > 0) lines.Add($"Evade: {data.Evade}");
            if (data.Armor > 0) lines.Add($"Armor: {data.Armor}");
            if (data.HP > 0) lines.Add($"HP+{data.HP}");
            if (data.TP > 0) lines.Add($"TP+{data.TP}");

            var stats = new List<string>();
            if (data.Str > 0) stats.Add($"Str+{data.Str}");
            if (data.Int > 0) stats.Add($"Int+{data.Int}");
            if (data.Wis > 0) stats.Add($"Wis+{data.Wis}");
            if (data.Agi > 0) stats.Add($"Agi+{data.Agi}");
            if (data.Con > 0) stats.Add($"Con+{data.Con}");
            if (data.Cha > 0) stats.Add($"Cha+{data.Cha}");
            if (stats.Count > 0) lines.Add(string.Join(" ", stats));

            if (data.LevelReq > 0) lines.Add($"Lvl Req: {data.LevelReq}");

            return string.Join("\n", lines);
        }
    }
}
