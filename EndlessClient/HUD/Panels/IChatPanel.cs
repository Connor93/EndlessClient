using EOLib.Domain.Chat;
using Microsoft.Xna.Framework;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Common interface for chat panels (both GFX-based and code-drawn)
    /// </summary>
    public interface IChatPanel : IGameComponent
    {
        ChatTab CurrentTab { get; }

        void TryStartNewPrivateChat(string targetCharacter);

        void SelectTab(ChatTab clickedTab);

        void ClosePMTab(ChatTab whichTab);
    }
}
