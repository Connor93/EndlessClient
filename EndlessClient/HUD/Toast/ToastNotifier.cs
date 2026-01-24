using AutomaticTypeMapper;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Controls;

namespace EndlessClient.HUD.Toast
{
    /// <summary>
    /// DI-friendly adapter for toast notifications.
    /// Looks up the CodeDrawnToastManager from the HUD control provider.
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class ToastNotifier : IToastNotifier
    {
        private readonly IHudControlProvider _hudControlProvider;

        public ToastNotifier(IHudControlProvider hudControlProvider)
        {
            _hudControlProvider = hudControlProvider;
        }

        public void NotifyExpGained(int amount)
        {
            if (!_hudControlProvider.IsInGame)
                return;

            GetToastManager()?.NotifyExpGained(amount);
        }

        public void NotifyItemPickup(string itemName, int amount)
        {
            if (!_hudControlProvider.IsInGame)
                return;

            GetToastManager()?.NotifyItemPickup(itemName, amount);
        }

        public void NotifyItemDrop(string itemName, int amount)
        {
            if (!_hudControlProvider.IsInGame)
                return;

            GetToastManager()?.NotifyItemDrop(itemName, amount);
        }

        public void NotifyNPCDrop(string playerName, string itemName, int amount)
        {
            if (!_hudControlProvider.IsInGame)
                return;

            GetToastManager()?.NotifyNPCDrop(playerName, itemName, amount);
        }

        private CodeDrawnToastManager GetToastManager()
        {
            try
            {
                return _hudControlProvider.GetComponent<CodeDrawnToastManager>(HudControlIdentifier.ToastManager);
            }
            catch
            {
                return null;
            }
        }
    }
}
