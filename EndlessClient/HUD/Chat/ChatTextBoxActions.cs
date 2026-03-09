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
                var panel = GetIntegratedChatPanel();
                if (panel.codeDrawn != null)
                    panel.codeDrawn.InputText = "";
                else if (panel.myra != null)
                    panel.myra.InputText = "";
                else
                    GetChatTextBox().Text = "";
            }
            else
            {
                GetChatTextBox().Text = "";
            }
        }

        public void FocusChatTextBox()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var panel = GetIntegratedChatPanel();
                if (panel.codeDrawn != null)
                    panel.codeDrawn.InputSelected = true;
                else if (panel.myra != null)
                    panel.myra.InputSelected = true;
                else
                    GetChatTextBox().Selected = true;
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
                var panel = GetIntegratedChatPanel();
                if (panel.codeDrawn != null)
                    return panel.codeDrawn.InputText ?? "";
                else if (panel.myra != null)
                    return panel.myra.InputText ?? "";
                else
                    return GetChatTextBox()?.Text ?? "";
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

        /// <summary>
        /// Returns whichever integrated chat panel is active (CodeDrawn or Myra), or both null if neither.
        /// </summary>
        private (CodeDrawnChatPanel codeDrawn, MyraChatPanel myra) GetIntegratedChatPanel()
        {
            var panel = _hudControlProvider.GetComponent<IChatPanel>(HudControlIdentifier.ChatPanel);
            return (panel as CodeDrawnChatPanel, panel as MyraChatPanel);
        }
    }
}
