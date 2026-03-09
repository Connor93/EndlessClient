using System;
using EndlessClient.Rendering.Character;
using EndlessClient.Rendering.Factories;
using EOLib;
using EOLib.Domain.Character;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.UIControls
{
    public class CreateCharacterControl : CharacterControl
    {
        private Vector2 _lastPosition;

        /// <summary>
        /// Maximum hairstyle ID for cycling. Character creation uses 20 (default),
        /// while the barber NPC can set this higher to expose all available styles.
        /// </summary>
        public int MaxHairStyle { get; set; } = 20;


        public event EventHandler Clicked;

        // default properties
        public CreateCharacterControl(ICharacterRendererFactory characterRendererFactory)
            : this(GetDefaultProperties(), characterRendererFactory) { }

        // custom render properties
        public CreateCharacterControl(CharacterRenderProperties renderProperties, ICharacterRendererFactory characterRendererFactory)
            : base(Character.Default.WithRenderProperties(renderProperties.WithDirection(EODirection.Down)), characterRendererFactory)
        {
            SetSize(99, 123);
            _lastPosition = Vector2.Zero;
        }

        /// <summary>
        /// Expose the underlying renderer for direct drawing (e.g. PostRenderOverlay).
        /// </summary>
        public ICharacterRenderer GetRenderer() => _characterRenderer;

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (!ShouldUpdate())
                return;

            var actualDrawPosition = new Vector2(DrawPositionWithParentOffset.X + 40,
                                                 DrawPositionWithParentOffset.Y + 36);

            if (_lastPosition != actualDrawPosition)
                _characterRenderer.SetAbsoluteScreenPosition((int)actualDrawPosition.X, (int)actualDrawPosition.Y);

            base.OnUpdateControl(gameTime);

            _lastPosition = actualDrawPosition;
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            var nextDirectionInt = (int)RenderProperties.Direction + 1;
            var nextDirection = (EODirection)(nextDirectionInt % 4);
            RenderProperties = RenderProperties.WithDirection(nextDirection);

            Clicked?.Invoke(this, EventArgs.Empty);

            return true;
        }

        public void NextGender()
        {
            RenderProperties = RenderProperties.WithGender((RenderProperties.Gender + 1) % 2);
        }

        public void NextRace()
        {
            RenderProperties = RenderProperties.WithRace((RenderProperties.Race + 1) % 6);
        }

        public void NextHairStyle()
        {
            RenderProperties = RenderProperties.WithHairStyle((RenderProperties.HairStyle + 1) % (MaxHairStyle + 1));
        }

        public void NextHairColor()
        {
            RenderProperties = RenderProperties.WithHairColor((RenderProperties.HairColor + 1) % 10);
        }

        public void PrevHairStyle()
        {
            var max = MaxHairStyle + 1;
            RenderProperties = RenderProperties.WithHairStyle((RenderProperties.HairStyle - 1 + max) % max);
        }

        public void PrevHairColor()
        {
            RenderProperties = RenderProperties.WithHairColor((RenderProperties.HairColor - 1 + 10) % 10);
        }

        private static CharacterRenderProperties GetDefaultProperties()
        {
            return new CharacterRenderProperties.Builder { HairStyle = 1 }.ToImmutable();
        }
    }
}
