using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Controls;
using EndlessClient.UIControls;
using EOLib.Config;
using EOLib.Domain.Map;
using Microsoft.Xna.Framework.Input;
using Optional;

namespace EndlessClient.Input
{
    public class ControlKeyHandler : InputHandlerBase
    {
        private readonly IControlKeyController _controlKeyController;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IHudControlProvider _hudControlProvider;

        public ControlKeyHandler(IEndlessGameProvider endlessGameProvider,
                                 IUserInputProvider userInputProvider,
                                 IUserInputTimeRepository userInputTimeRepository,
                                 IControlKeyController controlKeyController,
                                 ICurrentMapStateRepository currentMapStateRepository,
                                 IConfigurationProvider configurationProvider,
                                 IHudControlProvider hudControlProvider)
            : base(endlessGameProvider, userInputProvider, userInputTimeRepository, currentMapStateRepository)
        {
            _controlKeyController = controlKeyController;
            _configurationProvider = configurationProvider;
            _hudControlProvider = hudControlProvider;
        }

        protected override Option<Keys> HandleInput()
        {
            // Ctrl always triggers attack
            if (IsKeyHeld(Keys.LeftControl) && _controlKeyController.Attack())
                return Option.Some(Keys.LeftControl);
            if (IsKeyHeld(Keys.RightControl) && _controlKeyController.Attack())
                return Option.Some(Keys.RightControl);

            // Spacebar triggers attack when WASD mode is enabled and chat is empty
            // (same rules as WASD movement - shift bypasses, typing in chat bypasses)
            if (_configurationProvider.WASDMovement && !IsShiftHeld() && !IsChatActive())
            {
                if (IsKeyHeld(Keys.Space) && _controlKeyController.Attack())
                    return Option.Some(Keys.Space);
            }

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
