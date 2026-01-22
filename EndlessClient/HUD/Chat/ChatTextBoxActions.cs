using AutomaticTypeMapper;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Panels;
using EndlessClient.UIControls;
using EOLib.Config;

namespace EndlessClient.HUD.Chat
{
    [AutoMappedType]
    public class ChatTextBoxActions : IChatTextBoxActions
    {
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IConfigurationProvider _configurationProvider;

        public ChatTextBoxActions(IHudControlProvider hudControlProvider,
                                  IConfigurationProvider configurationProvider)
        {
            _hudControlProvider = hudControlProvider;
            _configurationProvider = configurationProvider;
        }

        public void ClearChatText()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var chatPanel = GetCodeDrawnChatPanel();
                if (chatPanel != null)
                    chatPanel.InputText = "";
            }
            else
            {
                var chatTextBox = GetChatTextBox();
                chatTextBox.Text = "";
            }
        }

        public void FocusChatTextBox()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var chatPanel = GetCodeDrawnChatPanel();
                if (chatPanel != null)
                    chatPanel.InputSelected = true;
            }
            else
            {
                GetChatTextBox().Selected = true;
            }
        }

        public string GetChatText()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var chatPanel = GetCodeDrawnChatPanel();
                return chatPanel?.InputText ?? "";
            }
            else
            {
                return GetChatTextBox()?.Text ?? "";
            }
        }

        private ChatTextBox GetChatTextBox()
        {
            return _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox);
        }

        private CodeDrawnChatPanel GetCodeDrawnChatPanel()
        {
            return _hudControlProvider.GetComponent<CodeDrawnChatPanel>(HudControlIdentifier.ChatPanel);
        }
    }
}
