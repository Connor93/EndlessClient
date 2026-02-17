using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EndlessClient.Rendering.NPC
{
    public class BossBarState
    {
        public int NpcIndex { get; set; }
        public int NpcId { get; set; }
        public string Name { get; set; }
        public int PercentHealth { get; set; }
    }

    public interface IBossHealthBarRepository
    {
        Dictionary<int, BossBarState> ActiveBosses { get; }
    }

    public interface IBossHealthBarProvider
    {
        IReadOnlyDictionary<int, BossBarState> ActiveBosses { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class BossHealthBarRepository : IBossHealthBarRepository, IBossHealthBarProvider
    {
        public Dictionary<int, BossBarState> ActiveBosses { get; set; }

        IReadOnlyDictionary<int, BossBarState> IBossHealthBarProvider.ActiveBosses => ActiveBosses;

        public BossHealthBarRepository()
        {
            ActiveBosses = new Dictionary<int, BossBarState>();
        }
    }
}
