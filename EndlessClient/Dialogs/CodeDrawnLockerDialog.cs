using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of LockerDialog with procedural rendering.
    /// </summary>
    public class CodeDrawnLockerDialog : CodeDrawnScrollingListDialog
    {
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly ILockerActions _lockerActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ICharacterProvider _characterProvider;
        private readonly ILockerDataProvider _lockerDataProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IContentProvider _contentProvider;

        private HashSet<InventoryItem> _cachedItems;
        private CodeDrawnButton _cancelButton;

        public CodeDrawnLockerDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            INativeGraphicsManager graphicsManager,
            ILockerActions lockerActions,
            ILocalizedStringFinder localizedStringFinder,
            IInventorySpaceValidator inventorySpaceValidator,
            IStatusLabelSetter statusLabelSetter,
            IEOMessageBoxFactory messageBoxFactory,
            ICharacterProvider characterProvider,
            ILockerDataProvider lockerDataProvider,
            IEIFFileProvider eifFileProvider,
            IContentProvider contentProvider)
            : base(styleProvider, gameStateProvider, clientWindowSizeProvider, graphicsDeviceProvider,
                   contentProvider.Fonts[Constants.FontSize08pt5], contentProvider.Fonts[Constants.FontSize10])
        {
            _graphicsManager = graphicsManager;
            _lockerActions = lockerActions;
            _localizedStringFinder = localizedStringFinder;
            _inventorySpaceValidator = inventorySpaceValidator;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _characterProvider = characterProvider;
            _lockerDataProvider = lockerDataProvider;
            _eifFileProvider = eifFileProvider;
            _contentProvider = contentProvider;

            _cachedItems = new HashSet<InventoryItem>();

            // Configure for locker dialog size
            DialogWidth = 320;
            DialogHeight = 340;
            ListAreaTop = 45;
            ListAreaHeight = 240;
            ItemHeight = 36;

            UpdateScrollBarLayout();

            Title = GetDialogTitle();

            CreateButtons(styleProvider);
            CenterInGameView();
        }

        private void CreateButtons(IUIStyleProvider styleProvider)
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var buttonWidth = 72;
            var buttonHeight = 26;
            var buttonY = DialogHeight - 36;

            _cancelButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle((DialogWidth - buttonWidth) / 2, buttonY, buttonWidth, buttonHeight),
                Visible = true
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);
        }

        public override void Initialize()
        {
            _cancelButton?.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (!_cachedItems.SetEquals(_lockerDataProvider.Items))
            {
                _cachedItems = _lockerDataProvider.Items.ToHashSet();
                UpdateItemList();
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdateItemList()
        {
            ClearItems();
            Title = GetDialogTitle();

            foreach (var item in _cachedItems)
            {
                var itemData = _eifFileProvider.EIFFile[item.ItemID];
                var subText = $"x{item.Amount}  {(itemData.Type == ItemType.Armor ? $"({_localizedStringFinder.GetString(EOResourceID.FEMALE - itemData.Gender)})" : string.Empty)}";
                var itemIcon = _graphicsManager.TextureFromResource(GFXTypes.Items, 2 * itemData.Graphic - 1, transparent: true);

                var lockerItem = item;
                AddItem(itemData.Name, subText: subText, data: lockerItem,
                    onRightClick: _ => TakeItem(itemData, lockerItem),
                    isLink: true,
                    icon: itemIcon);
            }
        }

        private void TakeItem(EOLib.IO.Pub.EIFRecord itemData, InventoryItem item)
        {
            if (!_inventorySpaceValidator.ItemFits(item.ItemID))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();

                _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_INFORMATION, EOResourceID.STATUS_LABEL_ITEM_PICKUP_NO_SPACE_LEFT);
            }
            else if (itemData.Weight * item.Amount + _characterProvider.MainCharacter.Stats[CharacterStat.Weight] > _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_ITS_TOO_HEAVY_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
            }
            else
            {
                _lockerActions.TakeItemFromLocker(item.ItemID);
            }
        }

        private string GetDialogTitle()
        {
            return _characterProvider.MainCharacter.Name + "'s " + _localizedStringFinder.GetString(EOResourceID.DIALOG_TITLE_PRIVATE_LOCKER) + $" [{_lockerDataProvider.Items.Count}]";
        }

        protected override void DrawButtonTextPostScale(Vector2 scaledPos, float scale, MonoGame.Extended.BitmapFonts.BitmapFont font)
        {
            var buttonWidth = (int)(72 * scale);
            var buttonHeight = (int)(26 * scale);
            var buttonY = (int)(scaledPos.Y + (DialogHeight - 36) * scale);

            // Cancel button (centered)
            if (_cancelButton != null && _cancelButton.Visible)
            {
                var cancelX = (int)(scaledPos.X + ((DialogWidth - 72) / 2) * scale);
                DrawButtonPostScale("Cancel", cancelX, buttonY, buttonWidth, buttonHeight, scale, font, _cancelButton.MouseOver);
            }
        }
    }
}
