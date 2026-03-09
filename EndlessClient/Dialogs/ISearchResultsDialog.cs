using System;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Shared interface for search results dialogs (XNA CodeDrawn and Myra implementations).
    /// Used by #item and #npc commands when multiple matches are found.
    /// </summary>
    public interface ISearchResultsDialog : IXNADialog
    {
        string Title { get; set; }

        void AddItem(string text, Action onClick);

        void ClearItems();

        new void Close();
    }
}
