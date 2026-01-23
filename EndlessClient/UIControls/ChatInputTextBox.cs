using EndlessClient.Rendering;
using EOLib.Config;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input.InputListeners;

namespace EndlessClient.UIControls
{
    /// <summary>
    /// Text box for chat input that filters WASD keys for movement when appropriate
    /// </summary>
    public class ChatInputTextBox : ClearableTextBox
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;

        public ChatInputTextBox(IConfigurationProvider configurationProvider,
                                Rectangle drawArea,
                                string fontContentName,
                                Texture2D caretTexture = null,
                                IClientWindowSizeProvider clientWindowSizeProvider = null)
            : base(drawArea, fontContentName, caretTexture: caretTexture)
        {
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            // In scaled mode, skip drawing text (it will be drawn post-scale by the parent panel)
            if (_clientWindowSizeProvider?.IsScaledMode == true)
            {
                // Don't call base - this skips drawing the text
                // The parent panel's DrawInputTextScaled handles crisp text rendering
                return;
            }

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleTextInput(KeyboardEventArgs eventArgs)
        {
            // Filter out special inputs (WASD for movement, numpad for emotes, etc.)
            if (IsSpecialInput(eventArgs.Key, eventArgs.Modifiers))
                return false;

            return base.HandleTextInput(eventArgs);
        }

        private bool IsSpecialInput(Keys k, KeyboardModifiers modifiers)
        {
            // NumPad keys are used for emotes, Alt modifier is for other functions
            if (k == Keys.Decimal || (k >= Keys.NumPad0 && k <= Keys.NumPad9) || modifiers == KeyboardModifiers.Alt)
                return true;

            // WASD keys and spacebar are used for movement/attack when enabled, unless:
            // - Shift is held (for typing capitals)
            // - There's already text in the chat box (player is typing)
            if (_configurationProvider.WASDMovement && modifiers != KeyboardModifiers.Shift && Text.Length == 0)
            {
                if (k == Keys.W || k == Keys.A || k == Keys.S || k == Keys.D || k == Keys.Space)
                    return true;
            }

            return false;
        }
    }
}
