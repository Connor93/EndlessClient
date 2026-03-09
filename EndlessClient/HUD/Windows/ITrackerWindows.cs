using System.Collections.Generic;
using EOLib.Domain.Interact.Quest;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Shared interface for bounty tracker windows (CodeDrawn and Myra implementations).
    /// </summary>
    public interface IBountyTrackerWindow
    {
        void Toggle();
    }

    /// <summary>
    /// Shared interface for quest tracker windows (CodeDrawn and Myra implementations).
    /// </summary>
    public interface IQuestTrackerWindow
    {
        void SetTrackedQuests(HashSet<string> trackedNames);

        void UpdateQuestProgress(IReadOnlyList<QuestProgressData> progress);

        bool Visible { get; set; }
    }
}
