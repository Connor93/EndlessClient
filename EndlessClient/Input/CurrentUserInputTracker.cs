using System;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace EndlessClient.Input
{
    public class CurrentUserInputTracker : GameComponent
    {
        private readonly IUserInputRepository _userInputRepository;
        private readonly IClientWindowSizeProvider _windowSizeProvider;

        public CurrentUserInputTracker(
            IEndlessGameProvider endlessGameProvider,
            IUserInputRepository userInputRepository,
            IClientWindowSizeProvider windowSizeProvider)
            : base((Game)endlessGameProvider.Game)
        {
            _userInputRepository = userInputRepository;
            _windowSizeProvider = windowSizeProvider;

            UpdateOrder = int.MinValue;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Game.IsActive)
            {
                base.Update(gameTime);
                return;
            }

            // Reset scroll wheel consumed flag at the start of each frame
            _userInputRepository.ScrollWheelConsumed = false;

            _userInputRepository.CurrentKeyState = Keyboard.GetState();

            var rawMouseState = Mouse.GetState();

            // Check if mouse is within actual window bounds (not game bounds, since raw coords are in window space)
            var windowBounds = Game.Window.ClientBounds;
            bool mouseInBounds = rawMouseState.X >= 0 && rawMouseState.X < windowBounds.Width &&
                                 rawMouseState.Y >= 0 && rawMouseState.Y < windowBounds.Height;

            // Transform mouse coordinates from window space to game space
            var offset = _windowSizeProvider.RenderOffset;
            var scale = _windowSizeProvider.ScaleFactor;

            // Remove offset and divide by scale to get game-space coordinates
            int gameX = (int)((rawMouseState.X - offset.X) / scale);
            int gameY = (int)((rawMouseState.Y - offset.Y) / scale);

            // Clamp to game bounds (use configured dimensions, not hardcoded defaults)
            gameX = Math.Clamp(gameX, 0, _windowSizeProvider.GameWidth - 1);
            gameY = Math.Clamp(gameY, 0, _windowSizeProvider.GameHeight - 1);

            // If mouse is outside window, release all button states to prevent accidental clicks
            _userInputRepository.CurrentMouseState = new MouseState(
                gameX,
                gameY,
                rawMouseState.ScrollWheelValue,
                mouseInBounds ? rawMouseState.LeftButton : ButtonState.Released,
                mouseInBounds ? rawMouseState.MiddleButton : ButtonState.Released,
                mouseInBounds ? rawMouseState.RightButton : ButtonState.Released,
                mouseInBounds ? rawMouseState.XButton1 : ButtonState.Released,
                mouseInBounds ? rawMouseState.XButton2 : ButtonState.Released,
                rawMouseState.HorizontalScrollWheelValue);

            base.Update(gameTime);
        }
    }
}
