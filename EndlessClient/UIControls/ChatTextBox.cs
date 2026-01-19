using System;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input.InputListeners;
using Optional;

namespace EndlessClient.UIControls
{
    /// <summary>
    /// Special instance of an XNATextBox that should ignore input from the number pad (which is used for Emotes)
    /// </summary>
    public class ChatTextBox : ClearableTextBox
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IConfigurationProvider _configurationProvider;

        private bool _ignoreAllInput;
        private Option<DateTime> _endMuteTime;

        private readonly Rectangle? _leftSide, _background, _rightSide;

        public ChatTextBox(INativeGraphicsManager nativeGraphicsManager,
                           IClientWindowSizeProvider clientWindowSizeProvider,
                           IContentProvider contentManagerProvider,
                           IConfigurationProvider configurationProvider)
            : base(Rectangle.Empty, // (124, 308, 440, 19)
                Constants.FontSize08,
                caretTexture: contentManagerProvider.Textures[ContentProvider.Cursor])
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _configurationProvider = configurationProvider;

            if (_clientWindowSizeProvider.Resizable)
            {
                _leftSide = new Rectangle(10, 308, 12, 20);
                _background = new Rectangle(22, 308, 560, 20);
                _rightSide = new Rectangle(578, 308, 11, 20);

                DrawArea = new Rectangle(124, _clientWindowSizeProvider.Height - 40, _clientWindowSizeProvider.Width - 116, 20);

                // original X coordinate accounting for the width of chat mode picture
                LeftPadding = 124;

                _clientWindowSizeProvider.GameWindowSizeChanged += (o, e) =>
                {
                    // status icons:      124 width
                    // friend/ignore:     40 width
                    DrawArea = new Rectangle(124, _clientWindowSizeProvider.Height - 40, _clientWindowSizeProvider.Width - 116, 20);
                    MaxWidth = DrawArea.Width - 184;
                };
            }
            else
            {
                DrawArea = new Rectangle(124, 308, 440, 20);
            }

            MaxChars = 140;
            _endMuteTime = Option.None<DateTime>();
        }

        public override void Initialize()
        {
            base.Initialize();

            // This must be done after Initialize because MaxWidth uses the sprite font.
            // SpriteFont is loaded in LoadContent which is called by Initialize.
            MaxWidth = _clientWindowSizeProvider.Resizable ? DrawArea.Width - 184 : 440;
        }

        public void SetMuted(DateTime endMuteTime)
        {
            _ignoreAllInput = true;
            _endMuteTime = Option.Some(endMuteTime);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _endMuteTime.MatchSome(endTime =>
            {
                if (DateTime.Now > endTime)
                {
                    _endMuteTime = Option.None<DateTime>();
                    _ignoreAllInput = false;
                }
            });

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (_clientWindowSizeProvider.Resizable && _background != null)
            {
                _spriteBatch.Begin(samplerState: SamplerState.LinearWrap);
                _spriteBatch.Draw(_nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 1, true), DrawArea, _background, Color.White);
                _spriteBatch.End();

                _spriteBatch.Begin();
                _spriteBatch.Draw(_nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 1, false), DrawPosition, _leftSide, Color.White);
                _spriteBatch.Draw(_nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 1, false), new Vector2(DrawArea.X + DrawArea.Width - _rightSide.Value.Width, DrawPosition.Y), _rightSide, Color.White);
                _spriteBatch.End();
            }

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleTextInput(KeyboardEventArgs eventArgs)
        {
            if (_ignoreAllInput)
                return false;

            if (!IsSpecialInput(eventArgs.Key, eventArgs.Modifiers))
                base.HandleTextInput(eventArgs);

            return true;
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
