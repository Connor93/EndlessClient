using System;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Chat;
using EndlessClient.HUD.Controls;
using EndlessClient.Rendering;
using EOLib.Graphics;
using Microsoft.Xna.Framework;
using Optional;
using XNAControls;

namespace EndlessClient.UIControls
{
    public class ChatModePictureBox : XNAPictureBox
    {
        private readonly IChatModeCalculator _chatModeCalculator;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;

        public enum ChatMode
        {
            NoText,
            Public,
            Private,
            Global,
            Group,
            Admin,
            Muted,
            Guild
        }

        private string _lastChat;
        private Option<DateTime> _endMuteTime;

        public ChatModePictureBox(INativeGraphicsManager nativeGraphicsManager,
                                  IClientWindowSizeProvider clientWindowSizeProvider,
                                  IChatModeCalculator chatModeCalculator,
                                  IHudControlProvider hudControlProvider)
        {
            _chatModeCalculator = chatModeCalculator;
            _hudControlProvider = hudControlProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;

            _lastChat = "";
            _endMuteTime = Option.None<DateTime>();

            Texture = nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 31);

            DrawArea = new Rectangle(16, 309, Texture.Width, Texture.Height / 8 - 4);
            SourceRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height / 8 - 4);

            if (clientWindowSizeProvider.Resizable)
            {
                DrawPosition = new Vector2(122, clientWindowSizeProvider.Height - 39);
                clientWindowSizeProvider.GameWindowSizeChanged += (_, _) => DrawPosition = new Vector2(122, clientWindowSizeProvider.Height - 39);
            }
        }

        public void SetMuted(DateTime endMuteTime)
        {
            _lastChat = "";
            _endMuteTime = Option.Some(endMuteTime);
            UpdateSourceRectangleForMode(ChatMode.Muted);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _endMuteTime.Match(
                some: endTime =>
                {
                    if (DateTime.Now >= endTime)
                    {
                        _endMuteTime = Option.None<DateTime>();
                        UpdateSourceRectangleForMode(ChatMode.NoText);
                    }
                },
                none: () =>
                {
                    if (AtLeastOneCharTypedOrDeleted())
                    {
                        UpdateSourceRectangleForMode(_chatModeCalculator.CalculateMode(ChatTextBox.Text));
                        _lastChat = ChatTextBox.Text;
                    }
                });

            base.OnUpdateControl(gameTime);
        }

        private bool AtLeastOneCharTypedOrDeleted()
        {
            return _hudControlProvider.IsInGame &&
                   ((_lastChat.Length == 0 && ChatTextBox.Text.Length > 0) ||
                    (_lastChat.Length > 0 && ChatTextBox.Text.Length == 0));
        }

        private void UpdateSourceRectangleForMode(ChatMode mode)
        {
            if (!SourceRectangle.HasValue)
                throw new InvalidOperationException("SourceRectangle is expected to have a value.");

            SourceRectangle = new Rectangle(0, (int)((int)mode * (Texture.Height / 8f)), Texture.Width, Texture.Height / 8 - 4);

            var yOffset = mode == ChatMode.NoText ? 0 : 2;

            if (_clientWindowSizeProvider.Resizable)
                DrawPosition = new Vector2(122, _clientWindowSizeProvider.Height - 39 + yOffset);
            else
                DrawArea = new Rectangle(16, 309 + yOffset, Texture.Width, Texture.Height / 8 - 4);
        }

        private ChatTextBox ChatTextBox => _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox);
    }
}
