using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Services;
using EndlessClient.HUD.Controls;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Login;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class MacroPanel : DraggableHudPanel, IHudPanel, IDraggableItemContainer
    {
        // Layout constants for resource 72 background
        // Left panel: F1-F8 (slots 0-7), Right panel: Shift+F1-F8 (slots 8-15)
        private const int LeftPanelX = 16;
        private const int RightPanelX = 230;
        private const int GridStartY = 26;
        private const int SlotWidth = 52;
        private const int SlotHeight = 45;
        private const int SlotsPerRow = 4;
        private const int RowsPerPanel = 2;

        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IESFFileProvider _esfFileProvider;
        private readonly HUD.Macros.IMacroSlotDataRepository _macroSlotDataRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigurationProvider _configProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;

        private readonly List<HUD.Macros.MacroPanelItem> _childItems;
        protected readonly IXNAButton _closeButton;

        public INativeGraphicsManager NativeGraphicsManager => _nativeGraphicsManager;

        public MacroPanel(INativeGraphicsManager nativeGraphicsManager,
                         IStatusLabelSetter statusLabelSetter,
                         IPlayerInfoProvider playerInfoProvider,
                         ICharacterProvider characterProvider,
                         IEIFFileProvider eifFileProvider,
                         IESFFileProvider esfFileProvider,
                         HUD.Macros.IMacroSlotDataRepository macroSlotDataRepository,
                         ISfxPlayer sfxPlayer,
                         IConfigurationProvider configProvider,
                         IClientWindowSizeProvider clientWindowSizeProvider,
                         IUserInputProvider userInputProvider,
                         IEODialogButtonService dialogButtonService)
            : base(clientWindowSizeProvider.Resizable)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _statusLabelSetter = statusLabelSetter;
            _playerInfoProvider = playerInfoProvider;
            _characterProvider = characterProvider;
            _eifFileProvider = eifFileProvider;
            _esfFileProvider = esfFileProvider;
            _macroSlotDataRepository = macroSlotDataRepository;
            _sfxPlayer = sfxPlayer;
            _configProvider = configProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _userInputProvider = userInputProvider;

            _childItems = new List<HUD.Macros.MacroPanelItem>();

            // Use resource 72 for the macro panel background
            BackgroundImage = nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 72);
            DrawArea = new Rectangle(102, 330, BackgroundImage.Width, BackgroundImage.Height);

            // Create OK button at bottom of panel (same style as paperdoll dialog)
            _closeButton = new XNAButton(dialogButtonService.SmallButtonSheet,
                new Vector2(BackgroundImage.Width / 2 - 40, BackgroundImage.Height - 42),
                dialogButtonService.GetSmallDialogButtonOutSource(SmallButton.Ok),
                dialogButtonService.GetSmallDialogButtonOverSource(SmallButton.Ok));

            LoadMacrosFromFile();

            Game.Exiting += SaveMacrosFile;
        }

        public bool NoItemsDragging() => _childItems.All(x => !x.IsDragging);

        public override void Initialize()
        {
            _closeButton.Initialize();
            _closeButton.SetParentControl(this);
            _closeButton.OnMouseDown += (_, _) =>
            {
                _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
                Visible = false;
            };

            base.Initialize();
            RefreshMacroItems();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Update icon graphics for child items
            foreach (var item in _childItems)
            {
                if (item.Data.Type == HUD.Macros.MacroSlotType.Spell)
                {
                    var spellData = _esfFileProvider.ESFFile[item.Data.Id];
                    var iconTexture = _nativeGraphicsManager.TextureFromResource(GFXTypes.SpellIcons, spellData.Icon);
                    item.SetIconGraphic(iconTexture);
                }
                else if (item.Data.Type == HUD.Macros.MacroSlotType.Item)
                {
                    var itemData = _eifFileProvider.EIFFile[item.Data.Id];
                    var iconTexture = _nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * itemData.Graphic - 1, true);
                    item.SetIconGraphic(iconTexture);
                }
            }

            base.OnUpdateControl(gameTime);
        }

        public void AcceptSpellDrop(int spellId, int targetSlot)
        {
            if (targetSlot < 0 || targetSlot >= HUD.Macros.MacroSlotDataRepository.TotalMacroSlots)
                return;

            var macroSlot = HUD.Macros.MacroSlot.ForSpell(spellId);
            _macroSlotDataRepository.MacroSlots[targetSlot] = Option.Some(macroSlot);
            RefreshMacroItems();
        }

        public void AcceptItemDrop(int itemId, int targetSlot)
        {
            if (targetSlot < 0 || targetSlot >= HUD.Macros.MacroSlotDataRepository.TotalMacroSlots)
                return;

            var macroSlot = HUD.Macros.MacroSlot.ForItem(itemId);
            _macroSlotDataRepository.MacroSlots[targetSlot] = Option.Some(macroSlot);
            RefreshMacroItems();
        }

        /// <summary>
        /// Returns slot index (0-15) for a screen position, or -1 if not over a valid slot.
        /// Slots 0-7 are in the left panel (F1-F8), slots 8-15 are in the right panel (Shift+F1-F8).
        /// Layout: each panel has 4 columns x 2 rows.
        /// </summary>
        public int GetSlotFromPosition(Vector2 position)
        {
            var relativePos = position - DrawPositionWithParentOffset;

            // Check left panel (F1-F8, slots 0-7)
            if (relativePos.X >= LeftPanelX && relativePos.X < LeftPanelX + SlotsPerRow * SlotWidth &&
                relativePos.Y >= GridStartY && relativePos.Y < GridStartY + RowsPerPanel * SlotHeight)
            {
                var col = (int)((relativePos.X - LeftPanelX) / SlotWidth);
                var row = (int)((relativePos.Y - GridStartY) / SlotHeight);
                return row * SlotsPerRow + col;
            }

            // Check right panel (Shift+F1-F8, slots 8-15)
            if (relativePos.X >= RightPanelX && relativePos.X < RightPanelX + SlotsPerRow * SlotWidth &&
                relativePos.Y >= GridStartY && relativePos.Y < GridStartY + RowsPerPanel * SlotHeight)
            {
                var col = (int)((relativePos.X - RightPanelX) / SlotWidth);
                var row = (int)((relativePos.Y - GridStartY) / SlotHeight);
                return 8 + row * SlotsPerRow + col;  // Offset by 8 for right panel
            }

            return -1;
        }

        /// <summary>
        /// Returns the draw position for a given slot index.
        /// </summary>
        public Vector2 GetSlotPosition(int slot)
        {
            if (slot < 0 || slot >= 16)
                return Vector2.Zero;

            int panelX = slot < 8 ? LeftPanelX : RightPanelX;
            int localSlot = slot < 8 ? slot : slot - 8;
            int col = localSlot % SlotsPerRow;
            int row = localSlot / SlotsPerRow;

            // Add small offset to center items within their grid cells
            const int cellPaddingX = 5;
            const int cellPaddingY = 5;

            return new Vector2(panelX + col * SlotWidth + cellPaddingX, GridStartY + row * SlotHeight + cellPaddingY);
        }

        private void RefreshMacroItems()
        {
            // Clear existing items
            foreach (var item in _childItems)
            {
                item.SetControlUnparented();
                item.Dispose();
            }
            _childItems.Clear();

            // Create items for non-empty slots
            for (int i = 0; i < HUD.Macros.MacroSlotDataRepository.TotalMacroSlots; i++)
            {
                _macroSlotDataRepository.MacroSlots[i].MatchSome(macroSlot =>
                {
                    var newItem = new HUD.Macros.MacroPanelItem(this, _nativeGraphicsManager, _sfxPlayer, _userInputProvider, _clientWindowSizeProvider, i, macroSlot);
                    newItem.Initialize();
                    newItem.SetParentControl(this);

                    newItem.Click += (sender, _) =>
                    {
                        var clickedItem = sender as HUD.Macros.MacroPanelItem;
                        if (clickedItem != null)
                            _macroSlotDataRepository.SelectedMacroSlot = Option.Some(clickedItem.Slot);
                    };

                    newItem.OnMouseOver += (sender, _) =>
                    {
                        var hoveredItem = sender as HUD.Macros.MacroPanelItem;
                        if (hoveredItem == null || hoveredItem.IsDragging)
                            return;

                        if (hoveredItem.Data.Type == HUD.Macros.MacroSlotType.Spell)
                        {
                            var spellData = _esfFileProvider.ESFFile[hoveredItem.Data.Id];
                            _statusLabelSetter.SetStatusLabel(EOResourceID.SKILLMASTER_WORD_SPELL, spellData.Name);
                        }
                        else if (hoveredItem.Data.Type == HUD.Macros.MacroSlotType.Item)
                        {
                            var itemData = _eifFileProvider.EIFFile[hoveredItem.Data.Id];
                            _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ITEM, itemData.Name);
                        }
                    };

                    newItem.DraggingFinishing += HandleItemDoneDragging;

                    _childItems.Add(newItem);
                });
            }
        }

        private void HandleItemDoneDragging(object sender, DragCompletedEventArgs<HUD.Macros.MacroSlot> e)
        {
            var item = sender as HUD.Macros.MacroPanelItem;
            if (item == null)
                return;

            if (e.DragOutOfBounds)
            {
                // Remove the macro from this slot
                _macroSlotDataRepository.MacroSlots[item.Slot] = Option.None<HUD.Macros.MacroSlot>();
                RefreshMacroItems();
                return;
            }

            var oldSlot = item.Slot;
            var newSlot = item.GetCurrentSlotBasedOnPosition();

            if (oldSlot != newSlot && newSlot >= 0 && newSlot < HUD.Macros.MacroSlotDataRepository.TotalMacroSlots)
            {
                // Swap slots
                var oldMacro = _macroSlotDataRepository.MacroSlots[oldSlot];
                var newMacro = _macroSlotDataRepository.MacroSlots[newSlot];

                _macroSlotDataRepository.MacroSlots[oldSlot] = newMacro;
                _macroSlotDataRepository.MacroSlots[newSlot] = oldMacro;

                RefreshMacroItems();
            }
        }

        private void LoadMacrosFromFile()
        {
            var macros = new IniReader(Constants.MacrosFile);
            var macrosKey = $"{_configProvider.Host}:{_playerInfoProvider.LoggedInAccountName}";

            if (macros.Load() && macros.Sections.ContainsKey(macrosKey))
            {
                var section = macros.Sections[macrosKey];
                foreach (var key in section.Keys.Where(x => x.Contains(_characterProvider.MainCharacter.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!key.Contains("."))
                        continue;

                    var parts = key.Split(".");
                    if (parts.Length < 2)
                        continue;

                    var slot = parts[1];
                    if (!int.TryParse(slot, out var slotIndex) || slotIndex < 0 || slotIndex >= HUD.Macros.MacroSlotDataRepository.TotalMacroSlots)
                        continue;

                    var value = section[key];
                    var valueParts = value.Split(':');
                    if (valueParts.Length != 2)
                        continue;

                    var type = valueParts[0];
                    if (!int.TryParse(valueParts[1], out var id))
                        continue;

                    HUD.Macros.MacroSlot macroSlot = type switch
                    {
                        "S" => HUD.Macros.MacroSlot.ForSpell(id),
                        "I" => HUD.Macros.MacroSlot.ForItem(id),
                        _ => null
                    };

                    if (macroSlot != null)
                        _macroSlotDataRepository.MacroSlots[slotIndex] = Option.Some(macroSlot);
                }
            }
        }

        private void SaveMacrosFile(object sender, ExitingEventArgs e)
        {
            var macros = new IniReader(Constants.MacrosFile);
            var macrosKey = $"{_configProvider.Host}:{_playerInfoProvider.LoggedInAccountName}";

            var section = macros.Load() && macros.Sections.ContainsKey(macrosKey)
                ? macros.Sections[macrosKey]
                : new SortedList<string, string>();

            // Remove existing entries for this character
            var existing = section.Where(x => x.Key.Contains(_characterProvider.MainCharacter.Name)).Select(x => x.Key).ToList();
            foreach (var key in existing)
                section.Remove(key);

            // Save current macro slots
            for (int i = 0; i < HUD.Macros.MacroSlotDataRepository.TotalMacroSlots; i++)
            {
                _macroSlotDataRepository.MacroSlots[i].MatchSome(macroSlot =>
                {
                    var typePrefix = macroSlot.Type switch
                    {
                        HUD.Macros.MacroSlotType.Spell => "S",
                        HUD.Macros.MacroSlotType.Item => "I",
                        _ => null
                    };

                    if (typePrefix != null)
                        section[$"{_characterProvider.MainCharacter.Name}.{i}"] = $"{typePrefix}:{macroSlot.Id}";
                });
            }

            macros.Sections[macrosKey] = section;
            macros.Save();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Game.Exiting -= SaveMacrosFile;
                SaveMacrosFile(null, new ExitingEventArgs());
            }

            base.Dispose(disposing);
        }
        public Point TransformMousePosition(Point position)
        {
            if (!_clientWindowSizeProvider.IsScaledMode)
                return position;

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
