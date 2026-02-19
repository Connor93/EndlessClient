using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Achievement
{
    public interface IAchievementRepository : IResettable
    {
        List<AchievementDefinition> Achievements { get; set; }

        List<LeaderboardEntry> LeaderboardEntries { get; set; }

        int LeaderboardAchievementId { get; set; }

        List<int> MaxedAchievementIds { get; set; }

        List<int> SelectedBadgeIds { get; set; }
    }

    public interface IAchievementProvider : IResettable
    {
        IReadOnlyList<AchievementDefinition> Achievements { get; }

        IReadOnlyList<LeaderboardEntry> LeaderboardEntries { get; }

        int LeaderboardAchievementId { get; }

        IReadOnlyList<int> MaxedAchievementIds { get; }

        IReadOnlyList<int> SelectedBadgeIds { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class AchievementRepository : IAchievementProvider, IAchievementRepository
    {
        public List<AchievementDefinition> Achievements { get; set; }

        public List<LeaderboardEntry> LeaderboardEntries { get; set; }

        public int LeaderboardAchievementId { get; set; }

        public List<int> MaxedAchievementIds { get; set; }

        public List<int> SelectedBadgeIds { get; set; }

        IReadOnlyList<AchievementDefinition> IAchievementProvider.Achievements => Achievements;

        IReadOnlyList<LeaderboardEntry> IAchievementProvider.LeaderboardEntries => LeaderboardEntries;

        IReadOnlyList<int> IAchievementProvider.MaxedAchievementIds => MaxedAchievementIds;

        IReadOnlyList<int> IAchievementProvider.SelectedBadgeIds => SelectedBadgeIds;

        public AchievementRepository()
        {
            ResetState();
        }

        public void ResetState()
        {
            Achievements = new List<AchievementDefinition>();
            LeaderboardEntries = new List<LeaderboardEntry>();
            LeaderboardAchievementId = 0;
            MaxedAchievementIds = new List<int>();
            SelectedBadgeIds = new List<int>();
        }
    }
}
