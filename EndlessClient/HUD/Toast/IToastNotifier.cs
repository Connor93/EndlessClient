namespace EndlessClient.HUD.Toast
{
    /// <summary>
    /// Interface for showing toast notifications for important game events.
    /// Only used in scaled mode - non-scaled mode uses StatusBarLabel.
    /// </summary>
    public interface IToastNotifier
    {
        /// <summary>Show a toast for gaining experience</summary>
        void NotifyExpGained(int amount);

        /// <summary>Show a toast for picking up an item</summary>
        void NotifyItemPickup(string itemName, int amount);

        /// <summary>Show a toast for an item dropping on the map</summary>
        void NotifyItemDrop(string itemName, int amount);

        /// <summary>Show a toast for an NPC dropping an item for the player</summary>
        void NotifyNPCDrop(string playerName, string itemName, int amount);
    }
}
