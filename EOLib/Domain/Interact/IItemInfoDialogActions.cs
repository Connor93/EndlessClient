using System;

namespace EOLib.Domain.Interact
{
    /// <summary>
    /// Interface for triggering item info dialog display from EOLib.
    /// Implemented in EndlessClient with access to UI components.
    /// </summary>
    public interface IItemInfoDialogActions
    {
        /// <summary>
        /// Show item information dialog for the specified item ID.
        /// </summary>
        /// <param name="itemId">The item ID to display info for.</param>
        void ShowItemInfo(int itemId);

        /// <summary>
        /// Show a message in the chat when no items are found.
        /// </summary>
        /// <param name="searchTerm">The search term that yielded no results.</param>
        void ShowNoItemsFound(string searchTerm);

        /// <summary>
        /// Show item search results when multiple items match.
        /// </summary>
        /// <param name="matchingItemIds">The list of matching item IDs.</param>
        void ShowItemSearchResults(int[] matchingItemIds);
    }
}
