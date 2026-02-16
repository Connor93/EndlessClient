using System;
using System.Collections.Generic;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Input;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Inventory
{
    public class InventoryItemContextMenu : XNAControl
    {
        public event Action<EIFRecord> UseEquipClicked;
        public event Action<EIFRecord> DropClicked;
        public event Action<EIFRecord> JunkClicked;
        public event Action<EIFRecord> StoreClicked;
        public event Action<EIFRecord> GlamorClicked;

        private readonly IUIStyleProvider _styleProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly BitmapFont _font;
        private readonly EIFRecord _itemData;

        private const int MenuItemWidth = 80;
        private const int MenuItemHeight = 22;
        private const int MenuPadding = 4;
        private const int BorderWidth = 1;

        private readonly List<(Rectangle Rect, string Label, Action Action)> _menuItems;
        private readonly Rectangle _menuBounds;
        private int _hoveredIndex = -1;
        private bool _justOpened = true;

        private static readonly HashSet<ItemType> GlamorEligibleTypes = new HashSet<ItemType>
        {
            ItemType.Armor, ItemType.Boots, ItemType.Hat, ItemType.Shield, ItemType.Weapon
        };

        public InventoryItemContextMenu(
            IUIStyleProvider styleProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            IContentProvider contentProvider,
            IUserInputProvider userInputProvider,
            ISfxPlayer sfxPlayer,
            EIFRecord itemData,
            Vector2 position,
            bool hasPortableLocker = false,
            bool hasGlamorGem = false)
        {
            _styleProvider = styleProvider;
            _userInputProvider = userInputProvider;
            _sfxPlayer = sfxPlayer;
            _itemData = itemData;
            _font = contentProvider.Fonts[Constants.FontSize08pt5];

            _menuItems = new List<(Rectangle, string, Action)>();

            // Determine if equippable or usable
            var isEquippable = itemData.Type >= ItemType.Weapon && itemData.Type <= ItemType.Bracer;
            var primaryLabel = isEquippable ? "Equip" : "Use";

            var yOffset = (int)position.Y + MenuPadding;
            var xOffset = (int)position.X + MenuPadding;

            _menuItems.Add((
                new Rectangle(xOffset, yOffset, MenuItemWidth, MenuItemHeight),
                primaryLabel,
                () => UseEquipClicked?.Invoke(_itemData)));
            yOffset += MenuItemHeight;

            _menuItems.Add((
                new Rectangle(xOffset, yOffset, MenuItemWidth, MenuItemHeight),
                "Drop",
                () => DropClicked?.Invoke(_itemData)));
            yOffset += MenuItemHeight;

            _menuItems.Add((
                new Rectangle(xOffset, yOffset, MenuItemWidth, MenuItemHeight),
                "Junk",
                () => JunkClicked?.Invoke(_itemData)));
            yOffset += MenuItemHeight;

            if (hasPortableLocker)
            {
                _menuItems.Add((
                    new Rectangle(xOffset, yOffset, MenuItemWidth, MenuItemHeight),
                    "Store",
                    () => StoreClicked?.Invoke(_itemData)));
                yOffset += MenuItemHeight;
            }

            if (hasGlamorGem && GlamorEligibleTypes.Contains(itemData.Type))
            {
                _menuItems.Add((
                    new Rectangle(xOffset, yOffset, MenuItemWidth, MenuItemHeight),
                    "Glamor",
                    () => GlamorClicked?.Invoke(_itemData)));
                yOffset += MenuItemHeight;
            }

            var totalHeight = MenuPadding * 2 + MenuItemHeight * _menuItems.Count;
            var totalWidth = MenuPadding * 2 + MenuItemWidth;

            _menuBounds = new Rectangle((int)position.X, (int)position.Y, totalWidth, totalHeight);

            DrawPosition = position;
            SetSize(totalWidth, totalHeight);

            // Ensure we draw and update on top of everything
            DrawOrder = 1000;
            UpdateOrder = -20;
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            if (!Game.Components.Contains(this))
                Game.Components.Add(this);

            base.Initialize();
        }

        protected override void OnUnconditionalUpdateControl(GameTime gameTime)
        {
            base.OnUnconditionalUpdateControl(gameTime);

            var currentMouse = _userInputProvider.CurrentMouseState;
            var previousMouse = _userInputProvider.PreviousMouseState;
            var mousePos = currentMouse.Position;

            // Update hover state
            _hoveredIndex = -1;
            for (int i = 0; i < _menuItems.Count; i++)
            {
                if (_menuItems[i].Rect.Contains(mousePos))
                {
                    _hoveredIndex = i;
                    break;
                }
            }

            // Skip the first frame — the right-click that opened the menu is still down
            if (_justOpened)
            {
                if (currentMouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released
                    && currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
                    _justOpened = false;
                return;
            }

            // Dismiss on click outside menu bounds (detect press transition)
            var leftJustPressed = currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                && previousMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released;
            var rightJustPressed = currentMouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                && previousMouse.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released;

            if (leftJustPressed || rightJustPressed)
            {
                if (!_menuBounds.Contains(mousePos))
                {
                    Dismiss();
                }
            }
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (_justOpened)
                return true;

            if (_hoveredIndex >= 0)
            {
                _menuItems[_hoveredIndex].Action?.Invoke();
                _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);
            }

            Dismiss();
            return true;
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            // Draw background
            var bgRect = DrawAreaWithParentOffset;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, BorderWidth);

            // Draw menu items
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var (rect, label, _) = _menuItems[i];

                if (i == _hoveredIndex)
                {
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, rect, _styleProvider.ButtonHover);
                }

                var textSize = _font.MeasureString(label);
                var textPos = new Vector2(
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2);
                _spriteBatch.DrawString(_font, label, textPos,
                    i == _hoveredIndex ? Color.White : _styleProvider.TextPrimary);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        private void Dismiss()
        {
            Game.Components.Remove(this);
            Dispose();
        }
    }
}
