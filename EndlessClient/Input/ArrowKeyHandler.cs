using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Controls;
using EndlessClient.UIControls;
using EOLib.Config;
using EOLib.Domain.Map;
using Microsoft.Xna.Framework.Input;
using Optional;

namespace EndlessClient.Input
{
    public class ArrowKeyHandler : InputHandlerBase
    {
        private readonly IArrowKeyController _arrowKeyController;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IHudControlProvider _hudControlProvider;

        public ArrowKeyHandler(IEndlessGameProvider endlessGameProvider,
                               IUserInputProvider userInputProvider,
                               IUserInputTimeRepository userInputTimeRepository,
                               IArrowKeyController arrowKeyController,
                               ICurrentMapStateRepository currentMapStateRepository,
                               IConfigurationProvider configurationProvider,
                               IHudControlProvider hudControlProvider)
            : base(endlessGameProvider, userInputProvider, userInputTimeRepository, currentMapStateRepository)
        {
            _arrowKeyController = arrowKeyController;
            _configurationProvider = configurationProvider;
            _hudControlProvider = hudControlProvider;
        }

        protected override Option<Keys> HandleInput()
        {
            // Arrow keys always work
            if (IsKeyHeld(Keys.Left) && _arrowKeyController.MoveLeft())
                return Option.Some(Keys.Left);
            if (IsKeyHeld(Keys.Right) && _arrowKeyController.MoveRight())
                return Option.Some(Keys.Right);
            if (IsKeyHeld(Keys.Up) && _arrowKeyController.MoveUp())
                return Option.Some(Keys.Up);
            if (IsKeyHeld(Keys.Down) && _arrowKeyController.MoveDown())
                return Option.Some(Keys.Down);

            // WASD keys only work if enabled, shift is NOT held, and chat box is empty
            // (so players can still type capitals or continue typing when they've started)
            if (_configurationProvider.WASDMovement && !IsShiftHeld() && !IsChatActive())
            {
                if (IsKeyHeld(Keys.A) && _arrowKeyController.MoveLeft())
                    return Option.Some(Keys.A);
                if (IsKeyHeld(Keys.D) && _arrowKeyController.MoveRight())
                    return Option.Some(Keys.D);
                if (IsKeyHeld(Keys.W) && _arrowKeyController.MoveUp())
                    return Option.Some(Keys.W);
                if (IsKeyHeld(Keys.S) && _arrowKeyController.MoveDown())
                    return Option.Some(Keys.S);
            }

            // Reset ghosting state when all movement keys are released
            var arrowKeysUp = KeysAreUp(Keys.Left, Keys.Right, Keys.Up, Keys.Down);
            var wasdKeysUp = !_configurationProvider.WASDMovement || KeysAreUp(Keys.W, Keys.A, Keys.S, Keys.D);
            if (arrowKeysUp && wasdKeysUp)
                _arrowKeyController.KeysUp();

            return Option.None<Keys>();
        }

        private bool IsShiftHeld()
        {
            return IsKeyHeld(Keys.LeftShift) || IsKeyHeld(Keys.RightShift);
        }

        private bool IsChatActive()
        {
            return _hudControlProvider.IsInGame &&
                   _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox).Text.Length > 0;
        }
    }
}
