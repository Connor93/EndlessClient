using System;
using EndlessClient.Audio;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Panels;
using EOLib.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.HUD.Macros
{
    public class MacroPanelItem : DraggablePanelItem<MacroSlot>
    {
        private const int ICON_AREA_WIDTH = 42, ICON_AREA_HEIGHT = 36;

        private readonly MacroPanel _macroPanel;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly Texture2D _whitePixel;
        private Texture2D _iconGraphic;

        public int Slot { get; set; }

        private int _displaySlot;
        public int DisplaySlot
        {
            get => _displaySlot;
            set
            {
                _displaySlot = value;
                DrawPosition = _macroPanel.GetSlotPosition(_displaySlot);
            }
        }

        public override Rectangle EventArea => IsDragging ? DrawArea : DrawAreaWithParentOffset;

        // uses absolute coordinates - covers both panels
        protected override Rectangle GridArea => new Rectangle(
            _parentContainer.DrawPositionWithParentOffset.ToPoint() + new Point(3, 6),
            new Point(420, 120));

        public event EventHandler<MouseEventArgs> Click;

        public MacroPanelItem(MacroPanel macroPanel,
                              INativeGraphicsManager nativeGraphicsManager,
                              ISfxPlayer sfxPlayer,
                              int slot,
                              MacroSlot data)
            : base(macroPanel)
        {
            _macroPanel = macroPanel;
            _nativeGraphicsManager = nativeGraphicsManager;
            _sfxPlayer = sfxPlayer;

            Slot = DisplaySlot = slot;
            Data = data;

            LoadIconGraphic();

            _whitePixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            SetSize(ICON_AREA_WIDTH, ICON_AREA_HEIGHT);
        }

        public void UpdateData(MacroSlot newData)
        {
            Data = newData;
            LoadIconGraphic();
        }

        private void LoadIconGraphic()
        {
            if (Data.Type == MacroSlotType.Spell)
            {
                // Load spell icon - icon ID comes from ESF data, caller should have set Data.Id to spell ID
                // The actual rendering will look up the spell icon from ESF
                _iconGraphic = null; // Will be set by panel when rendering
            }
            else if (Data.Type == MacroSlotType.Item)
            {
                // Load item icon - icon ID comes from EIF data, caller should have set Data.Id to item ID
                // The actual rendering will look up the item icon from EIF
                _iconGraphic = null; // Will be set by panel when rendering
            }
            else
            {
                _iconGraphic = null;
            }
        }

        public void SetIconGraphic(Texture2D graphic)
        {
            _iconGraphic = graphic;
        }

        public int GetCurrentSlotBasedOnPosition()
        {
            if (!IsDragging)
                return Slot;

            var mousePos = MouseExtended.GetState().Position.ToVector2();
            return _macroPanel.GetSlotFromPosition(mousePos);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            DrawHighlight();
            DrawIcon();

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (_parentContainer.NoItemsDragging())
                _sfxPlayer.PlaySfx(SoundEffectID.InventoryPickup);

            Click?.Invoke(control, eventArgs);
            return base.HandleMouseDown(control, eventArgs);
        }

        protected override bool HandleDragStart(IXNAControl control, MouseEventArgs eventArgs)
        {
            _sfxPlayer.PlaySfx(SoundEffectID.InventoryPickup);
            return base.HandleDragStart(control, eventArgs);
        }

        protected override void OnDraggingFinished(DragCompletedEventArgs<MacroSlot> args)
        {
            base.OnDraggingFinished(args);

            _sfxPlayer.PlaySfx(SoundEffectID.InventoryPlace);
            DrawPosition = _macroPanel.GetSlotPosition(DisplaySlot);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _whitePixel.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DrawHighlight()
        {
            if (MouseOver)
            {
                if (!IsDragging)
                {
                    _spriteBatch.Draw(_whitePixel, DrawAreaWithParentOffset, Color.FromNonPremultiplied(200, 200, 200, 60));
                }
                else
                {
                    var highlightPosition = _macroPanel.GetSlotPosition(DisplaySlot);
                    var highlightRectangle = new Rectangle((_parentContainer.DrawPositionWithParentOffset + highlightPosition).ToPoint(), DrawArea.Size);

                    if (highlightRectangle.Contains(Mouse.GetState().Position))
                        _spriteBatch.Draw(_whitePixel, highlightRectangle, Color.FromNonPremultiplied(200, 200, 200, 60));
                }
            }
        }

        private void DrawIcon()
        {
            if (_iconGraphic == null)
                return;

            var halfWidth = _iconGraphic.Width / 2;
            var srcRect = new Rectangle(MouseOver ? halfWidth : 0, 0, halfWidth, _iconGraphic.Height);

            Rectangle targetDrawArea;
            if (!IsDragging)
            {
                targetDrawArea = new Rectangle(
                    DrawAreaWithParentOffset.X + (DrawAreaWithParentOffset.Width - srcRect.Width) / 2,
                    DrawAreaWithParentOffset.Y + (DrawAreaWithParentOffset.Height - srcRect.Height) / 2,
                    srcRect.Width,
                    srcRect.Height);
            }
            else
            {
                targetDrawArea = new Rectangle(
                    Mouse.GetState().X - srcRect.Width / 2,
                    Mouse.GetState().Y - srcRect.Height / 2,
                    srcRect.Width,
                    srcRect.Height);
            }

            _spriteBatch.Draw(_iconGraphic,
                targetDrawArea,
                srcRect,
                Color.FromNonPremultiplied(255, 255, 255, IsDragging ? 127 : 255));
        }
    }
}

