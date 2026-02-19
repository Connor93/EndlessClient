using Amadevus.RecordGenerator;

namespace EOLib.Domain.Achievement
{
    [Record]
    public sealed partial class AchievementTierData
    {
        public int Threshold { get; }
        public int ExpReward { get; }
        public int ItemId { get; }
        public int ItemAmount { get; }
        public int PlayersCompleted { get; }
    }

    [Record]
    public sealed partial class AchievementDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Type { get; }
        public int Target { get; }
        public AchievementTierData[] Tiers { get; }
        public int CurrentProgress { get; }
        public int CurrentTier { get; }
        public int UniqueCount { get; }
    }

    [Record]
    public sealed partial class LeaderboardEntry
    {
        public string Name { get; }
        public int TierReached { get; }
        public int Progress { get; }
    }
}
