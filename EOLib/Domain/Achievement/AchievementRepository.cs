using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Achievement
{
    public interface IAchievementRepository : IResettable
    {
        List<AchievementDefinition> Achievements { get; set; }

        List<LeaderboardEntry> LeaderboardEntries { get; set; }

        int LeaderboardAchievementId { get; set; }
    }

    public interface IAchievementProvider : IResettable
    {
        IReadOnlyList<AchievementDefinition> Achievements { get; }

        IReadOnlyList<LeaderboardEntry> LeaderboardEntries { get; }

        int LeaderboardAchievementId { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class AchievementRepository : IAchievementProvider, IAchievementRepository
    {
        public List<AchievementDefinition> Achievements { get; set; }

        public List<LeaderboardEntry> LeaderboardEntries { get; set; }

        public int LeaderboardAchievementId { get; set; }

        IReadOnlyList<AchievementDefinition> IAchievementProvider.Achievements => Achievements;

        IReadOnlyList<LeaderboardEntry> IAchievementProvider.LeaderboardEntries => LeaderboardEntries;

        public AchievementRepository()
        {
            ResetState();
        }

        public void ResetState()
        {
            Achievements = new List<AchievementDefinition>();
            LeaderboardEntries = new List<LeaderboardEntry>();
            LeaderboardAchievementId = 0;
        }
    }
}
