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
            _userInputRepository.CurrentKeyState = Keyboard.GetState();

            var rawMouseState = Mouse.GetState();

            if (_windowSizeProvider.IsScaledMode)
            {
                // Transform mouse coordinates from window space to game space
                var offset = _windowSizeProvider.RenderOffset;
                var scale = _windowSizeProvider.ScaleFactor;

                // Remove offset and divide by scale to get game-space coordinates
                int gameX = (int)((rawMouseState.X - offset.X) / scale);
                int gameY = (int)((rawMouseState.Y - offset.Y) / scale);

                // Clamp to game bounds
                gameX = Math.Clamp(gameX, 0, ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH - 1);
                gameY = Math.Clamp(gameY, 0, ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT - 1);

                _userInputRepository.CurrentMouseState = new MouseState(
                    gameX,
                    gameY,
                    rawMouseState.ScrollWheelValue,
                    rawMouseState.LeftButton,
                    rawMouseState.MiddleButton,
                    rawMouseState.RightButton,
                    rawMouseState.XButton1,
                    rawMouseState.XButton2,
                    rawMouseState.HorizontalScrollWheelValue);
            }
            else
            {
                _userInputRepository.CurrentMouseState = rawMouseState;
            }

            base.Update(gameTime);
        }
    }
}

